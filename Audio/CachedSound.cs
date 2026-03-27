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

    if (AudioData == null || AudioData.Length == 0)
    {
        for (int i = 0; i < pieces; i++)
            slices[i] = new CachedSound(new float[channels], WaveFormat, $"{Name} s{i+1}");
        return slices;
    }

    int frames = AudioData.Length / channels;
    var boundariesFrames = new List<int>();

    // --- AGGRESSIVE BASS TUNING ---
    float sensitivity = 1.8f;      // High jump required to trigger
    float noiseFloor = 0.12f;      // Ignore the "hum" of the bass tail
    float emaAlpha = 0.1f;         // Heavy smoothing to ignore bass oscillations
    int lookback = 30;             // Large window to capture the whole bass "thump"
    int minDistance = WaveFormat.SampleRate / 4; // 250ms: Hard block for 1/4 second to prevent sub-splitting

    float env = 0f;
    float[] envPerFrame = new float[frames];
    float globalMax = 0.01f;

    // 1. Calculate smoothed volume envelope and find global peak
    for (int f = 0; f < frames; f++)
    {
        float l = Math.Abs(AudioData[f * channels]);
        float r = channels > 1 ? Math.Abs(AudioData[f * channels + 1]) : 0f;
        float mix = (l + r) / (channels > 1 ? 2f : 1f);

        env = (emaAlpha * mix) + (1f - emaAlpha) * env;
        envPerFrame[f] = env;
        if (env > globalMax) globalMax = env;
    }

    // 2. Detection with Peak-Locking
    float lastPeakInSlice = 0f;

    for (int f = lookback; f < frames; f++)
    {
        float currentVal = envPerFrame[f];
        float pastVal = envPerFrame[f - lookback];

        // Track the peak of the current "active" sound to avoid re-triggering during decay
        if (boundariesFrames.Count > 0 && (f - boundariesFrames.Last()) < minDistance * 2)
        {
            if (currentVal > lastPeakInSlice) lastPeakInSlice = currentVal;
        }

        bool isRapidRise = currentVal > (pastVal * sensitivity);
        bool isAboveNoise = currentVal > noiseFloor;

        // Key Logic: Only trigger if we aren't currently inside a loud sustained peak
        bool isNotInsideActivePeak = currentVal >= lastPeakInSlice * 0.9f;

        if (isRapidRise && isAboveNoise)
        {
            bool timeCheck = (boundariesFrames.Count == 0 || (f - boundariesFrames.Last()) > minDistance);

            if (timeCheck)
            {
                boundariesFrames.Add(f);
                lastPeakInSlice = currentVal; // Reset peak tracking for new slice
                if (boundariesFrames.Count == pieces) break;
            }
        }
    }

    // 3. Fallback distribution
    if (boundariesFrames.Count < pieces)
    {
        if (boundariesFrames.Count == 0) boundariesFrames.Add(0);
        int lastPos = boundariesFrames.Last();
        int step = Math.Max(minDistance, (frames - lastPos) / (pieces - boundariesFrames.Count + 1));

        while (boundariesFrames.Count < pieces)
        {
            int next = boundariesFrames.Last() + step;
            if (next >= frames - 1) next = frames - 1;
            boundariesFrames.Add(next);
        }
    }

    // 4. Slice Creation
    for (int i = 0; i < pieces; i++)
    {
        int startFrame = boundariesFrames[i];
        int endFrame = (i == pieces - 1) ? frames : boundariesFrames[i + 1];

        int startIdx = startFrame * channels;
        int endIdx = endFrame * channels;
        int length = Math.Max(channels, endIdx - startIdx);

        var sliceData = new float[length];
        int actualCopy = Math.Min(length, AudioData.Length - startIdx);
        if (actualCopy > 0) Array.Copy(AudioData, startIdx, sliceData, 0, actualCopy);

        // 15ms Fade out for smoother transitions on heavy bass
        int fadeSamples = Math.Min((int)(WaveFormat.SampleRate * 0.015) * channels, length / 2);
        for (int s = 0; s < fadeSamples; s += channels)
        {
            float mult = (float)(fadeSamples - s) / fadeSamples;
            int tailIdx = length - fadeSamples + s;
            for (int c = 0; c < channels; c++)
            {
                if (tailIdx + c < length) sliceData[tailIdx + c] *= mult;
            }
        }

        slices[i] = new CachedSound(sliceData, WaveFormat, $"{Name} s{i+1}");
    }

    return slices;
}

        private CachedSound() { }
    }
}
