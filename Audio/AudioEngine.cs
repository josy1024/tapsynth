using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using TapSynth.Audio.Effects;

namespace TapSynth.Audio
{
    public class AudioEngine : IDisposable
    {
        private IWavePlayer _outputDevice;
        private WaveInEvent _inputDevice;
        private readonly MixingSampleProvider _mixer;
        private readonly CrusherLimiter _masterEffects;
        private readonly VolumeSampleProvider _masterVolume;
        private List<float> _recordedAudio = new List<float>();

        public PolyphonicVoiceAllocator[] Slots { get; private set; }

        public CrusherLimiter MasterEffects => _masterEffects;
        public VolumeSampleProvider MasterVolume => _masterVolume;

        public static List<string> GetOutputDevices()
        {
            var list = new List<string> { "Default System Output" };
            for (int i = 0; i < WaveOut.DeviceCount; i++) list.Add(WaveOut.GetCapabilities(i).ProductName);
            return list;
        }

        public static List<string> GetInputDevices()
        {
            var list = new List<string> { "Default Microphone" };
            for (int i = 0; i < WaveIn.DeviceCount; i++) list.Add(WaveIn.GetCapabilities(i).ProductName);
            return list;
        }

        public AudioEngine(int sampleRate = 44100)
        {
            var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
            _mixer = new MixingSampleProvider(waveFormat) { ReadFully = true };
            _masterEffects = new CrusherLimiter(_mixer);
            _masterVolume = new VolumeSampleProvider(_masterEffects) { Volume = 0.8f };
            
            Slots = new PolyphonicVoiceAllocator[16];
            for (int i = 0; i < 16; i++)
            {
                Slots[i] = new PolyphonicVoiceAllocator(_mixer, waveFormat, 4);
            }

            SetOutputDevice(-1); // Default device
        }

        public void SetOutputDevice(int deviceNumber)
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            // deviceNumber -1 is default mapper
            _outputDevice = new WaveOutEvent { DeviceNumber = deviceNumber, DesiredLatency = 50 };
            _outputDevice.Init(_masterVolume);
            _outputDevice.Play();
        }

        public void StartRecording(int inputDeviceNumber)
        {
            _recordedAudio.Clear();
            _inputDevice?.Dispose();
            
            _inputDevice = new WaveInEvent { DeviceNumber = inputDeviceNumber, WaveFormat = new WaveFormat(44100, 16, 1) }; // Mono 16-bit
            _inputDevice.DataAvailable += (s, a) => {
                for (int i = 0; i < a.BytesRecorded; i += 2)
                {
                    short sample = BitConverter.ToInt16(a.Buffer, i);
                    float f = sample / 32768f;
                    _recordedAudio.Add(f); // Mono L
                    _recordedAudio.Add(f); // Mono R to make it stereo format
                }
            };
            _inputDevice.StartRecording();
        }

        public CachedSound StopRecordingAndGetSound(string name)
        {
            _inputDevice?.StopRecording();
            _inputDevice?.Dispose();
            _inputDevice = null;

            if (_recordedAudio.Count == 0) return null;

            var array = _recordedAudio.ToArray();
            var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2); // Now stereo
            return new CachedSound(array, format, name);
        }

        public void PlaySlot(int slotIndex, CachedSound sound, double pitchRatio = 1.0, float velocity = 1.0f, float pan = 0.0f)
        {
            if (slotIndex >= 0 && slotIndex < Slots.Length && sound != null && sound.AudioData != null)
            {
                Slots[slotIndex].Play(sound, pitchRatio, velocity, pan);
            }
        }

        public void StopAll()
        {
            foreach (var slot in Slots) slot.StopAll();
        }

        public void Dispose()
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            _inputDevice?.Dispose();
        }
    }
}
