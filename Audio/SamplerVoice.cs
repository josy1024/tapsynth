using NAudio.Wave;
using System;

namespace TapSynth.Audio
{
    public class SamplerVoice : ISampleProvider
    {
        private CachedSound _cachedSound;
        private double _position;
        private double _playbackRate = 1.0;
        private float _velocity = 1.0f;
        private float _pan = 0.0f; // -1 to 1

        public bool IsPlaying { get; private set; }

        public WaveFormat WaveFormat { get; }

        public SamplerVoice(WaveFormat masterFormat)
        {
            WaveFormat = masterFormat;
        }

        public void Play(CachedSound sound, double pitchRatio, float velocity, float pan)
        {
            _cachedSound = sound;
            _position = 0;
            _playbackRate = Math.Max(0.01, pitchRatio);
            _velocity = Math.Max(0, Math.Min(1, velocity));
            _pan = Math.Max(-1, Math.Min(1, pan));
            IsPlaying = true;
        }

        public void Stop()
        {
            IsPlaying = false;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            if (!IsPlaying || _cachedSound == null)
            {
                Array.Clear(buffer, offset, count);
                return count; // Keep returning 0s so the mixer doesn't unregister this permanently allocated voice
            }

            int framesRequested = count / 2;
            var audioData = _cachedSound.AudioData;
            int outIndex = offset;
            
            float leftVol = _pan < 0 ? 1.0f : 1.0f - _pan;
            float rightVol = _pan > 0 ? 1.0f : 1.0f + _pan;

            for (int i = 0; i < framesRequested; i++)
            {
                int baseIndex = (int)_position * 2; 

                if (baseIndex >= audioData.Length - 3)
                {
                    IsPlaying = false;
                    break;
                }

                // Simple linear interpolation
                double fraction = _position - (int)_position;
                float sampleL1 = audioData[baseIndex];
                float sampleL2 = audioData[baseIndex + 2];
                float sampleR1 = audioData[baseIndex + 1];
                float sampleR2 = audioData[baseIndex + 3];

                float interpL = (float)(sampleL1 + fraction * (sampleL2 - sampleL1));
                float interpR = (float)(sampleR1 + fraction * (sampleR2 - sampleR1));

                buffer[outIndex++] = interpL * _velocity * leftVol;
                buffer[outIndex++] = interpR * _velocity * rightVol;

                _position += _playbackRate;
            }

            while (outIndex < offset + count)
            {
                buffer[outIndex++] = 0;
            }

            return count;
        }
    }
}
