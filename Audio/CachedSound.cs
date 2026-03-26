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
            var boundaries = new List<int>();
            
            // Simple transient amplitude detection
            float threshold = 0.05f; 
            int minDistance = WaveFormat.SampleRate / 15; // At least ~66ms between drum slices
            
            for (int i = 0; i < AudioData.Length; i += 2) // Assume stereo
            {
                float amp = Math.Abs(AudioData[i]) + (i + 1 < AudioData.Length ? Math.Abs(AudioData[i+1]) : 0);
                if (amp > threshold)
                {
                    if (boundaries.Count == 0 || (i - boundaries[boundaries.Count - 1]) > minDistance * 2) // *2 for stereo array
                    {
                        boundaries.Add(i);
                        if (boundaries.Count == pieces) break;
                    }
                }
            }

            // Fallback: If we couldn't find 16 distinct transients, just pad the remainder evenly
            while (boundaries.Count < pieces)
            {
                if (boundaries.Count == 0) boundaries.Add(0);
                else boundaries.Add(Math.Min(AudioData.Length, boundaries[boundaries.Count - 1] + ((AudioData.Length / pieces) & ~1)));
            }

            for (int i = 0; i < pieces; i++)
            {
                int start = boundaries[i];
                int end = (i == pieces - 1) ? AudioData.Length : boundaries[i + 1];
                if (end > AudioData.Length) end = AudioData.Length;
                
                int length = end - start;
                if (length <= 0) length = 2; // Prevent array crash on zero-length slices

                var sliceData = new float[length];
                Array.Copy(AudioData, start, sliceData, 0, length);
                
                // Add minor 10ms fade out to the tail to prevent DC-offset popping
                int fadeSamples = Math.Min(441, length / 2) & ~1; 
                for (int f = 0; f < fadeSamples; f += 2)
                {
                    float multiplier = (float)(fadeSamples - f) / fadeSamples;
                    int fIndex = length - fadeSamples + f;
                    if (fIndex < length) sliceData[fIndex] *= multiplier;
                    if (fIndex + 1 < length) sliceData[fIndex + 1] *= multiplier;
                }

                slices[i] = new CachedSound(sliceData, WaveFormat, $"{Name} s{i+1}");
            }
            return slices;
        }

        private CachedSound() { }
    }
}
