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
    if (AudioData == null || AudioData.Length == 0) return slices;

    int totalFrames = AudioData.Length / channels;
    var boundaries = new List<int>();

    // --- PARAMETERS ---
    float sensitivity = 1.5f;       // Multiplier for a jump to be a "hit"
    int minDistance = WaveFormat.SampleRate / 10; // 100ms cooldown
    float emaAlphaLong = 0.01f;     // Slow average (The "Room" volume)
    float emaAlphaShort = 0.2f;     // Fast average (The "Current" hit)

    float longTermAvg = 0.05f;
    float shortTermAvg = 0f;

    // First, find the absolute peak to normalize our logic
    float globalPeak = 0f;
    for (int i = 0; i < AudioData.Length; i++)
        if (Math.Abs(AudioData[i]) > globalPeak) globalPeak = Math.Abs(AudioData[i]);

    for (int f = 0; f < totalFrames; f++)
    {
        float sample = Math.Abs(AudioData[f * channels]);

        // Update two envelopes: one slow, one fast
        longTermAvg = (emaAlphaLong * sample) + (1 - emaAlphaLong) * longTermAvg;
        shortTermAvg = (emaAlphaShort * sample) + (1 - emaAlphaShort) * shortTermAvg;

        // TRIGGER LOGIC:
        // 1. Short term spike must be significantly higher than the long term average
        // 2. Short term must be above a minimum "silence" floor
        // 3. Must respect the cooldown
        if (shortTermAvg > (longTermAvg * sensitivity) && shortTermAvg > (globalPeak * 0.1f))
        {
            if (boundaries.Count == 0 || (f - boundaries[^1]) > minDistance)
            {
                // Verify this is the "Start" of the peak (Slope is positive)
                if (f + 5 < totalFrames && Math.Abs(AudioData[(f+5)*channels]) >= sample)
                {
                    boundaries.Add(f);
                    if (boundaries.Count >= pieces) break;

                    // After a hit, "jump" the long term average up to prevent
                    // the "tail" of a fat bass from re-triggering
                    longTermAvg = shortTermAvg * 1.2f;
                }
            }
        }
    }

    // --- FALLBACK: If not enough slices found, fill the rest evenly ---
    if (boundaries.Count < pieces)
    {
        int lastPos = boundaries.Count > 0 ? boundaries[^1] : 0;
        int remaining = pieces - boundaries.Count;
        int step = (totalFrames - lastPos) / (remaining + 1);
        for (int i = 0; i < remaining; i++)
        {
            lastPos += step;
            boundaries.Add(Math.Min(lastPos, totalFrames - 1));
        }
    }

    // --- CREATE THE CACHED SOUNDS ---
    for (int i = 0; i < pieces; i++)
    {
        int start = boundaries[i] * channels;
        int end = (i == pieces - 1) ? AudioData.Length : boundaries[i + 1] * channels;
        int len = Math.Max(channels, end - start);

        float[] data = new float[len];
        Array.Copy(AudioData, start, data, 0, Math.Min(len, AudioData.Length - start));

        // Anti-pop fade (5ms)
        int fade = Math.Min((int)(WaveFormat.SampleRate * 0.005) * channels, len / 2);
        for (int j = 0; j < fade; j++) data[len - 1 - j] *= (j / (float)fade);

        slices[i] = new CachedSound(data, WaveFormat, $"{Name}_s{i + 1}");
    }
    return slices;
}


        private CachedSound() { }
    }
}
