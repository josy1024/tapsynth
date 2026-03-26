using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using TapSynth.Audio.Effects;

namespace TapSynth.Audio
{
    public class AudioEngine : IDisposable
    {
        private readonly WasapiOut _outputDevice;
        private readonly MixingSampleProvider _mixer;
        private readonly CrusherLimiter _masterEffects;
        private readonly VolumeSampleProvider _masterVolume;

        public PolyphonicVoiceAllocator[] Slots { get; private set; }

        public CrusherLimiter MasterEffects => _masterEffects;
        public VolumeSampleProvider MasterVolume => _masterVolume;

        public AudioEngine(int sampleRate = 44100)
        {
            // Low latency WASAPI out. Using Shared mode.
            _outputDevice = new WasapiOut(NAudio.CoreAudioApi.AudioClientShareMode.Shared, 40);
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
            
            _mixer = new MixingSampleProvider(waveFormat) { ReadFully = true };
            
            _masterEffects = new CrusherLimiter(_mixer);
            _masterVolume = new VolumeSampleProvider(_masterEffects) { Volume = 0.8f };
            
            Slots = new PolyphonicVoiceAllocator[16];
            for (int i = 0; i < 16; i++)
            {
                // Each slot gets 4 notes of polyphony. We can increase if needed.
                Slots[i] = new PolyphonicVoiceAllocator(_mixer, waveFormat, 4);
            }

            _outputDevice.Init(_masterVolume);
            _outputDevice.Play();
        }

        public void PlaySlot(int slotIndex, CachedSound sound, double pitchRatio = 1.0, float velocity = 1.0f, float pan = 0.0f)
        {
            if (slotIndex >= 0 && slotIndex < Slots.Length && sound != null)
            {
                Slots[slotIndex].Play(sound, pitchRatio, velocity, pan);
            }
        }

        public void StopAll()
        {
            foreach (var slot in Slots)
            {
                slot.StopAll();
            }
        }

        public void Dispose()
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
        }
    }
}
