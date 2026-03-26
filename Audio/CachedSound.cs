using NAudio.Wave;
using System.Collections.Generic;
using System;

namespace TapSynth.Audio
{
    public class CachedSound
    {
        public float[] AudioData { get; private set; }
        public WaveFormat WaveFormat { get; private set; }
        public string Name { get; set; }

        public CachedSound(string audioFileName, int targetSampleRate = 44100)
        {
            Name = System.IO.Path.GetFileNameWithoutExtension(audioFileName);
            using (var audioFileReader = new AudioFileReader(audioFileName))
            {
                var targetFormat = WaveFormat.CreateIeeeFloatWaveFormat(targetSampleRate, 2);
                using (var resampler = new MediaFoundationResampler(audioFileReader, targetFormat) { ResamplerQuality = 60 })
                {
                    var sampleProvider = resampler.ToSampleProvider();
                    WaveFormat = sampleProvider.WaveFormat;

                    var wholeFile = new List<float>();
                    var readBuffer = new float[targetSampleRate * 2]; // 1 second chunks
                    int samplesRead;
                    while ((samplesRead = sampleProvider.Read(readBuffer, 0, readBuffer.Length)) > 0)
                    {
                        var span = new ReadOnlySpan<float>(readBuffer, 0, samplesRead);
                        wholeFile.AddRange(span.ToArray());
                    }
                    AudioData = wholeFile.ToArray();
                }
            }
        }

        // Generation for pure test tones (drums/beeps) if no file is provided.
        public static CachedSound CreateTestTone(string name, int frequency, double durationMs, int sampleRate = 44100, bool noise = false)
        {
            var sound = new CachedSound();
            sound.Name = name;
            sound.WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
            int samples = (int)(sampleRate * (durationMs / 1000.0) * 2);
            sound.AudioData = new float[samples];

            Random rng = new Random();

            for (int i = 0; i < samples / 2; i++)
            {
                double time = (double)i / sampleRate;
                float sample;
                if (noise)
                {
                    // 8-bit noise burst (Hihat/Snare)
                    float rnd = (float)(rng.NextDouble() * 2.0 - 1.0);
                    // Decimate noise to sound crunchy
                    if (i % 4 != 0) rnd = sound.AudioData[(i-1)*2];
                    sample = rnd * (float)Math.Exp(-time * 15);
                }
                else
                {
                    // 8-bit Square wave with decay for beep/kick
                    double freqDrop = frequency * Math.Exp(-time * 20);
                    double phase = (freqDrop * time * 2.0 * Math.PI);
                    // Square wave approximation
                    float sqr = Math.Sin(phase) > 0 ? 0.3f : -0.3f;
                    sample = sqr * (float)Math.Exp(-time * 10);
                }

                sound.AudioData[i * 2] = sample;
                sound.AudioData[i * 2 + 1] = sample;
            }
            return sound;
        }

        public CachedSound(float[] audioData, WaveFormat waveFormat, string name)
        {
            AudioData = audioData;
            WaveFormat = waveFormat;
            Name = name;
        }

        public CachedSound[] Slice(int pieces = 16)
        {
            var slices = new CachedSound[pieces];

            int channels = WaveFormat.Channels <= 0 ? 2 : WaveFormat.Channels;
            int frames = AudioData.Length / channels;

            var boundariesFrames = new List<int>();

            // Parameters for improved onset detection
            float highThreshold = 0.10f; // require stronger peaks
            float lowThreshold = 0.06f;  // hysteresis lower bound
            float emaAlpha = 0.05f;      // envelope smoothing
            int minDistanceFrames = Math.Max(1, WaveFormat.SampleRate / 8); // ~125ms at 44100

            float env = 0f;
            float prevEnv = 0f;
            var envPerFrame = new float[frames];

            for (int f = 0; f < frames; f++)
            {
                float l = AudioData[f * channels];
                float r = channels > 1 ? AudioData[f * channels + 1] : 0f;
                float amp = (Math.Abs(l) + Math.Abs(r)) / (channels > 1 ? 2f : 1f);

                env = emaAlpha * amp + (1f - emaAlpha) * env;
                envPerFrame[f] = env;

                // Hysteresis: trigger only when crossing the high threshold from below
                if (env > highThreshold && prevEnv <= highThreshold)
                {
                    if (boundariesFrames.Count == 0 || (f - boundariesFrames[boundariesFrames.Count - 1]) > minDistanceFrames)
                    {
                        boundariesFrames.Add(f);
                        if (boundariesFrames.Count == pieces) break;
                    }
                }

                prevEnv = env;
            }

            // Consolidate nearby detections: keep the strongest in a short cluster
            int minSeparationFrames = Math.Max(1, WaveFormat.SampleRate / 6); // ~166ms
            var consolidated = new List<int>();
            foreach (var b in boundariesFrames)
            {
                if (consolidated.Count == 0) { consolidated.Add(b); continue; }
                int last = consolidated[consolidated.Count - 1];
                if (b - last < minSeparationFrames)
                {
                    // keep the one with higher envelope value
                    if (envPerFrame[b] > envPerFrame[last]) consolidated[consolidated.Count - 1] = b;
                }
                else consolidated.Add(b);
            }

            if (consolidated.Count > 0) boundariesFrames = consolidated;

            // Fallback: If we couldn't find enough transients, distribute evenly
            if (boundariesFrames.Count < pieces)
            {
                if (boundariesFrames.Count == 0) boundariesFrames.Add(0);
                while (boundariesFrames.Count < pieces)
                {
                    int last = boundariesFrames[boundariesFrames.Count - 1];
                    int step = Math.Max(1, frames / pieces);
                    int next = Math.Min(frames, last + step);
                    boundariesFrames.Add(next);
                }
            }

            // Convert frame boundaries to sample indices (interleaved array indices)
            var boundaries = new List<int>(boundariesFrames.Count);
            foreach (var bf in boundariesFrames) boundaries.Add(Math.Min(AudioData.Length, bf * channels));

            for (int i = 0; i < pieces; i++)
            {
                int start = boundaries[i];
                int end = (i == pieces - 1) ? AudioData.Length : boundaries[i + 1];
                if (end > AudioData.Length) end = AudioData.Length;

                int length = end - start;
                if (length <= 0) length = channels; // ensure minimal frame size

                var sliceData = new float[length];
                Array.Copy(AudioData, start, sliceData, 0, length);

                // Add minor fade out to the tail to prevent popping (10ms ~ 441 samples)
                int fadeSamples = Math.Min(441, length / 2);
                for (int f = 0; f < fadeSamples; f++)
                {
                    float multiplier = (float)(fadeSamples - f) / fadeSamples;
                    int fIndex = length - fadeSamples + f;
                    if (fIndex >= 0 && fIndex < length) sliceData[fIndex] *= multiplier;
                }

                slices[i] = new CachedSound(sliceData, WaveFormat, $"{Name} s{i+1}");
            }

            return slices;
        }

        private CachedSound() { }
    }
}
