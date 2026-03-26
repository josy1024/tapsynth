using NAudio.Wave;
using NAudio.Dsp;
using System;

namespace TapSynth.Audio
{
    public class AnalyzerSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider _source;
        public float[] WaveformBuffer { get; } = new float[2048];
        public float[] SpectrumBuffer { get; } = new float[512];
        private Complex[] _fftBuffer = new Complex[1024];
        
        public float LastPeak { get; private set; }

        private int _waveformPos = 0;
        private int _fftPos = 0;

        public WaveFormat WaveFormat => _source.WaveFormat;

        public AnalyzerSampleProvider(ISampleProvider source)
        {
            _source = source;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int read = _source.Read(buffer, offset, count);
            if (read > 0)
            {
                float max = 0;
                for (int i = 0; i < read; i++)
                {
                    float sample = buffer[offset + i];
                    float abs = Math.Abs(sample);
                    if (abs > max) max = abs;

                    WaveformBuffer[_waveformPos++] = sample;
                    if (_waveformPos >= WaveformBuffer.Length) _waveformPos = 0;

                    if (WaveFormat.Channels == 1 || (i % 2 == 0)) 
                    {
                        _fftBuffer[_fftPos].X = (float)(sample * FastFourierTransform.HannWindow(_fftPos, 1024));
                        _fftBuffer[_fftPos].Y = 0;
                        _fftPos++;

                        if (_fftPos >= 1024)
                        {
                            _fftPos = 0;
                            var fftCopy = new Complex[1024];
                            Array.Copy(_fftBuffer, fftCopy, 1024);
                            FastFourierTransform.FFT(true, 10, fftCopy);

                            for (int f = 0; f < 512; f++)
                            {
                                float mag = (float)Math.Sqrt(fftCopy[f].X * fftCopy[f].X + fftCopy[f].Y * fftCopy[f].Y);
                                SpectrumBuffer[f] = mag;
                            }
                        }
                    }
                }
                LastPeak = max;
            }
            return read;
        }
    }
}
