using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using TapSynth.Audio.Effects;

namespace TapSynth.Audio
{
    public class AudioEngine : IDisposable
    {
        private IWavePlayer _outputDevice;
        private IWaveIn _inputDevice;
        private NAudio.Wave.WaveFileWriter _recordWriter;
        private string _tempRecFile;
        private readonly MixingSampleProvider _mixer;
        private readonly CrusherLimiter _masterEffects;
        private readonly VolumeSampleProvider _masterVolume;
        private readonly AnalyzerSampleProvider _masterAnalyzer;

        public PolyphonicVoiceAllocator[] Slots { get; private set; }

        public CrusherLimiter MasterEffects => _masterEffects;
        public VolumeSampleProvider MasterVolume => _masterVolume;
        public AnalyzerSampleProvider MasterAnalyzer => _masterAnalyzer;
        public float InputPeak { get; private set; }

        public static List<string> GetOutputDevices()
        {
            var list = new List<string> { "Default System Output" };
            for (int i = 0; i < WaveOut.DeviceCount; i++) list.Add(WaveOut.GetCapabilities(i).ProductName);
            return list;
        }

        public static List<string> GetInputDevices()
        {
            var list = new List<string> { "Default Microphone", "System Audio (Loopback)" };
            for (int i = 0; i < WaveIn.DeviceCount; i++) list.Add(WaveIn.GetCapabilities(i).ProductName);
            return list;
        }

        public AudioEngine(int sampleRate = 44100)
        {
            try
            {
                TapSynth.Utils.Logger.Info($"AudioEngine ctor: sampleRate={sampleRate}");
                var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, 2);
                _mixer = new MixingSampleProvider(waveFormat) { ReadFully = true };
                _masterEffects = new CrusherLimiter(_mixer);
                _masterVolume = new VolumeSampleProvider(_masterEffects) { Volume = 0.8f };
                _masterAnalyzer = new AnalyzerSampleProvider(_masterVolume);

                Slots = new PolyphonicVoiceAllocator[16];
                for (int i = 0; i < 16; i++)
                {
                    Slots[i] = new PolyphonicVoiceAllocator(_mixer, waveFormat, 4);
                }

                TapSynth.Utils.Logger.Info($"Detected Output devices: {WaveOut.DeviceCount}");
                SetOutputDevice(-1); // Default device
                TapSynth.Utils.Logger.Info("AudioEngine initialized successfully");
            }
            catch (Exception ex)
            {
                TapSynth.Utils.Logger.Exception(ex, "AudioEngine ctor failed");
                throw;
            }
        }

        public void SetOutputDevice(int deviceNumber)
        {
            _outputDevice?.Stop();
            _outputDevice?.Dispose();
            // deviceNumber -1 is default mapper
            _outputDevice = new WaveOutEvent { DeviceNumber = deviceNumber, DesiredLatency = 50 };
            _outputDevice.Init(_masterAnalyzer);
            _outputDevice.Play();
        }

        public void StartRecording(int selectionIndex)
        {
            _inputDevice?.Dispose();
            _recordWriter?.Dispose();

            _tempRecFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "tapsynth_rec.wav");

            if (selectionIndex == 1) // System Audio Loopback
            {
                _inputDevice = new WasapiLoopbackCapture();
            }
            else
            {
                int deviceNumber = selectionIndex <= 0 ? -1 : selectionIndex - 2;
                _inputDevice = new WaveInEvent { DeviceNumber = deviceNumber, WaveFormat = new WaveFormat(44100, 16, 1) }; // Mono
            }

            _recordWriter = new WaveFileWriter(_tempRecFile, _inputDevice.WaveFormat);

            _inputDevice.DataAvailable += (s, a) => {
                _recordWriter.Write(a.Buffer, 0, a.BytesRecorded);

                float max = 0;
                int bytesPerSample = _inputDevice.WaveFormat.BitsPerSample / 8;
                for (int i = 0; i < a.BytesRecorded; i += bytesPerSample)
                {
                    float val = 0;
                    if (bytesPerSample == 4) val = Math.Abs(BitConverter.ToSingle(a.Buffer, i));
                    else if (bytesPerSample == 2) val = Math.Abs(BitConverter.ToInt16(a.Buffer, i) / 32768f);
                    if (val > max) max = val;
                }
                InputPeak = max;
            };

            TapSynth.Utils.Logger.Info($"StartRecording: selectionIndex={selectionIndex}, tempFile={_tempRecFile}");

            _inputDevice.StartRecording();
        }

        public CachedSound StopRecordingAndGetSound(string name, string outFilePath = null)
        {
            if (_inputDevice != null)
            {
                _inputDevice.StopRecording();
                _inputDevice.Dispose();
                _inputDevice = null;
            }

            if (_recordWriter != null)
            {
                _recordWriter.Dispose();
                _recordWriter = null;
            }
            if (!System.IO.File.Exists(_tempRecFile)) return null;

            // Optionally copy the recorded WAV to the current working directory
            try
            {
                if (!string.IsNullOrEmpty(outFilePath))
                {
                    TapSynth.Utils.Logger.Info($"Copying recorded WAV to {outFilePath}");
                    System.IO.File.Copy(_tempRecFile, outFilePath, true);
                }
            }
            catch (Exception ex)
            {
                TapSynth.Utils.Logger.Exception(ex, "Copy recorded WAV failed");
            }

            try
            {
                TapSynth.Utils.Logger.Info($"Creating CachedSound from {_tempRecFile}");
                var sound = new CachedSound(_tempRecFile, 44100);
                sound.Name = name;
                TapSynth.Utils.Logger.Info("CachedSound created successfully");
                return sound;
            }
            catch (Exception ex)
            {
                TapSynth.Utils.Logger.Exception(ex, "CachedSound creation failed");
                return null;
            }
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
