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

    // --- FINE TUNED PARAMETERS ---
    float sensitivity = 1.7f;        // Higher = harder to trigger (prevents splitting)
    int minDistance = WaveFormat.SampleRate / 8; // ~125ms (Standard 16th note at 120bpm)

    float emaAlphaLong = 0.015f;     // Increased slightly to "forget" the bass faster
    float emaAlphaShort = 0.25f;     // Fast tracking for sharp transients

    float longTermAvg = 0.02f;
    float shortTermAvg = 0f;
    bool isGateOpen = true;          // Logic gate to prevent double-triggering
    float resetThreshold = 0.4f;     // Must drop below 40% of the last peak to reset

    float globalPeak = 0f;
    for (int i = 0; i < AudioData.Length; i++)
        if (Math.Abs(AudioData[i]) > globalPeak) globalPeak = Math.Abs(AudioData[i]);

    float lastTriggerPeak = 0f;

    for (int f = 0; f < totalFrames; f++)
    {
        float sample = Math.Abs(AudioData[f * channels]);

        longTermAvg = (emaAlphaLong * sample) + (1 - emaAlphaLong) * longTermAvg;
        shortTermAvg = (emaAlphaShort * sample) + (1 - emaAlphaShort) * shortTermAvg;

        // Reset the gate if the volume has dropped significantly
        if (!isGateOpen && shortTermAvg < (lastTriggerPeak * resetThreshold))
        {
            isGateOpen = true;
        }

        // TRIGGER LOGIC
        if (isGateOpen && shortTermAvg > (longTermAvg * sensitivity) && shortTermAvg > (globalPeak * 0.08f))
        {
            if (boundaries.Count == 0 || (f - boundaries[^1]) > minDistance)
            {
                // Look ahead to confirm it's a real peak, not just noise
                float lookAhead = (f + 4 < totalFrames) ? Math.Abs(AudioData[(f + 4) * channels]) : 0;

                if (lookAhead >= sample * 0.9f)
                {
                    boundaries.Add(f);
                    lastTriggerPeak = shortTermAvg;
                    isGateOpen = false; // Lock the gate!

                    if (boundaries.Count >= pieces) break;
                }
            }
        }
    }

    // --- FALLBACK (Evenly distribute remaining slots) ---
    if (boundaries.Count < pieces)
    {
        int lastPos = boundaries.Count > 0 ? boundaries[^1] : 0;
        int remaining = pieces - boundaries.Count;
        int step = Math.Max(minDistance, (totalFrames - lastPos) / (remaining + 1));
        for (int i = 0; i < remaining; i++)
        {
            lastPos += step;
            if (lastPos >= totalFrames) lastPos = totalFrames - 1;
            boundaries.Add(lastPos);
        }
    }

    // --- OUTPUT SLICING ---
    for (int i = 0; i < pieces; i++)
    {
        int startFrame = boundaries[i];
        int endFrame = (i == pieces - 1) ? totalFrames : boundaries[i + 1];

        int startIdx = startFrame * channels;
        int endIdx = endFrame * channels;
        int len = Math.Max(channels, endIdx - startIdx);

        float[] data = new float[len];
        int copyLen = Math.Min(len, AudioData.Length - startIdx);
        if (copyLen > 0) Array.Copy(AudioData, startIdx, data, 0, copyLen);

        // 8ms Fade to smooth the transitions
        int fade = Math.Min((int)(WaveFormat.SampleRate * 0.008) * channels, len / 2);
        for (int j = 0; j < fade; j++)
        {
            float mult = j / (float)fade;
            int idx = len - 1 - j;
            if (idx >= 0) data[idx] *= mult;
        }

        slices[i] = new CachedSound(data, WaveFormat, $"{Name}_s{i + 1}");
    }
    return slices;
}


        private CachedSound() { }
    }
}
