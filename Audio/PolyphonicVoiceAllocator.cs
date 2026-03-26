using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System.Collections.Generic;
using System.Linq;

namespace TapSynth.Audio
{
    public class PolyphonicVoiceAllocator
    {
        private readonly List<SamplerVoice> _voices;
        private int _nextVoiceIndex = 0;

        public PolyphonicVoiceAllocator(MixingSampleProvider mixer, WaveFormat format, int maxVoices = 8)
        {
            _voices = new List<SamplerVoice>(maxVoices);
            for (int i = 0; i < maxVoices; i++)
            {
                var voice = new SamplerVoice(format);
                _voices.Add(voice);
                mixer.AddMixerInput(voice);
            }
        }

        public void Play(CachedSound sound, double pitchRatio, float velocity, float pan)
        {
            var voice = _voices.FirstOrDefault(v => !v.IsPlaying) ?? _voices[_nextVoiceIndex];
            voice.Play(sound, pitchRatio, velocity, pan);
            
            _nextVoiceIndex = (_nextVoiceIndex + 1) % _voices.Count;
        }

        public void StopAll()
        {
            foreach (var v in _voices) v.Stop();
        }
    }
}
