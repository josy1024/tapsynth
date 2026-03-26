using NAudio.Wave;
using System;

namespace TapSynth.Audio.Effects
{
    public class CrusherLimiter : ISampleProvider
    {
        private readonly ISampleProvider _source;
        public WaveFormat WaveFormat => _source.WaveFormat;
        
        public bool EnableCrush { get; set; } = false;
        public int BitDepth { get; set; } = 8;
        public float Drive { get; set; } = 1.0f; // Gain before limiter

        public CrusherLimiter(ISampleProvider source)
        {
            _source = source;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            
            float steps = (float)Math.Pow(2, BitDepth);

            for (int i = 0; i < read; i++)
            {
                float sample = buffer[offset + i] * Drive;

                if (EnableCrush)
                {
                    // Linear quantization for bitcrushing
                    sample = (float)Math.Round(sample * steps) / steps;
                }

                // Tanh soft clipping
                buffer[offset + i] = (float)Math.Tanh(sample);
            }
            return read;
        }
    }
}
