using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using TapSynth.Audio;
using TapSynth.Sequencing;
using TapSynth.State;

namespace TapSynth.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly AudioEngine _audio;
        private readonly Sequencer _sequencer;
        private readonly UndoRedoManager _undoManager;

        public ObservableCollection<StepViewModel> Steps { get; } = new ObservableCollection<StepViewModel>();

        private int _selectedTrackIndex = 0;
        public int SelectedTrackIndex
        {
            get => _selectedTrackIndex;
            set {
                _selectedTrackIndex = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SelectedTrackPitch));
                OnPropertyChanged(nameof(SelectedTrackVolume));
                UpdateStepDisplay();
            }
        }

        public string PlayButtonText => IsPlaying ? "STOP" : "PLAY";

        // Dynamic Colors for UI Buttons
        public string WriteButtonBackground => LiveLooperMode ? "#EE4444" : "#3A3A3A";
        public string SaveButtonBackground => IsSaveMode ? "#44EE44" : "#3A3A3A";
        public string LoadButtonBackground => IsLoadMode ? "#44AAEE" : "#3A3A3A";
        public string FxButtonBackground => IsFxModeActive ? "#EEAA44" : "#2D4D44";
        public string AudioRecordButtonBackground => IsRecordingAudio ? "#FF2222" : "#2D4D44";
        public string SoundButtonBackground => IsSoundEditMode ? "#FFCC00" : "#2D4D44";

        public System.Collections.ObjectModel.ObservableCollection<string> InputDevices { get; }
        public System.Collections.ObjectModel.ObservableCollection<string> OutputDevices { get; }

        private int _selectedInputDevice = 0;
        public int SelectedInputDevice { get => _selectedInputDevice; set { _selectedInputDevice = value; OnPropertyChanged(); } }

        private int _selectedOutputDevice = 0;
        public int SelectedOutputDevice { get => _selectedOutputDevice; set { _selectedOutputDevice = value; _audio.SetOutputDevice(value - 1); OnPropertyChanged(); } }

        private bool _isRecordingAudio;
        public bool IsRecordingAudio
        {
            get => _isRecordingAudio;
            set { _isRecordingAudio = value; OnPropertyChanged(); OnPropertyChanged(nameof(AudioRecordButtonBackground)); }
        }

        private bool _isSoundEditMode;
        public bool IsSoundEditMode
        {
            get => _isSoundEditMode;
            set { _isSoundEditMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(SoundButtonBackground)); }
        }

        public bool IsPlaying
        {
            get => _sequencer.Clock.IsPlaying;
            set { OnPropertyChanged(); OnPropertyChanged(nameof(PlayButtonText)); }
        }

        public int BPM
        {
            get => _sequencer.Clock.BPM;
            set { _sequencer.Clock.BPM = value; OnPropertyChanged(); }
        }

        private bool _liveLooperMode;
        public bool LiveLooperMode
        {
            get => _liveLooperMode;
            set { _liveLooperMode = value; _sequencer.LiveLooperMode = value; OnPropertyChanged(); OnPropertyChanged(nameof(WriteButtonBackground)); }
        }

        private bool _isSaveMode;
        public bool IsSaveMode
        {
            get => _isSaveMode;
            set { _isSaveMode = value; if(value) IsLoadMode = false; OnPropertyChanged(); OnPropertyChanged(nameof(SaveButtonBackground)); }
        }

        private bool _isLoadMode;
        public bool IsLoadMode
        {
            get => _isLoadMode;
            set { _isLoadMode = value; if(value) IsSaveMode = false; OnPropertyChanged(); OnPropertyChanged(nameof(LoadButtonBackground)); }
        }

        public bool EnableBitcrush
        {
            get => _audio.MasterEffects.EnableCrush;
            set { _audio.MasterEffects.EnableCrush = value; OnPropertyChanged(); }
        }

        public ICommand TogglePlayCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }
        public ICommand ToggleStepCommand { get; }
        public ICommand SelectTrackCommand { get; }
        public ICommand AdjustBPMCommand { get; }
        public ICommand AdjustPitchCommand { get; }
        public ICommand AdjustVolumeCommand { get; }
        public ICommand ShowHelpCommand { get; }
        public ICommand ToggleBitcrushCommand { get; }
        public ICommand ToggleFxModeCommand { get; }
        public ICommand ToggleLiveLooperCommand { get; }
        public ICommand ToggleSaveModeCommand { get; }
        public ICommand ToggleLoadModeCommand { get; }
        public ICommand ToggleSoundModeCommand { get; }
        public ICommand ToggleAudioRecordCommand { get; }

        private bool _isFxModeActive;
        public bool IsFxModeActive
        {
            get => _isFxModeActive;
            set { _isFxModeActive = value; OnPropertyChanged(); }
        }

        public MainViewModel()
        {
            _audio = new AudioEngine();
            _sequencer = new Sequencer(_audio);
            _undoManager = new UndoRedoManager();

            InputDevices = new System.Collections.ObjectModel.ObservableCollection<string>(AudioEngine.GetInputDevices());
            OutputDevices = new System.Collections.ObjectModel.ObservableCollection<string>(AudioEngine.GetOutputDevices());

            // Setup temporary synth sounds with more variety
            for (int i = 0; i < 16; i++)
            {
                if (i < 8)
                {
                    int freq = i < 4 ? 50 + i * 30 : 220 + (i - 4) * 110;
                    _sequencer.SlotSounds[i] = CachedSound.CreateTestTone($"Synth {i}", freq, i < 4 ? 120 : 300, 44100, false);
                }
                else
                {
                    if (i == 8)
                    {
                        // 808 Style Big Deep Kick!
                        _sequencer.SlotSounds[i] = CachedSound.CreateTestTone($"Kick Deep", 45, 500, 44100, false);
                    }
                    else
                    {
                        bool isNoise = (i == 9 || i == 10 || i == 11 || i == 13 || i == 15);
                        int freq = isNoise ? 100 : 60 + (i - 8) * 40;
                        _sequencer.SlotSounds[i] = CachedSound.CreateTestTone($"Drum {i}", freq, isNoise ? 80 : 150, 44100, isNoise);
                    }
                }
            }

            TogglePlayCommand = new RelayCommand(_ => TogglePlay());
            ToggleBitcrushCommand = new RelayCommand(_ => EnableBitcrush = !EnableBitcrush);
            ToggleFxModeCommand = new RelayCommand(_ => {
                IsFxModeActive = !IsFxModeActive;
                if (IsFxModeActive) ShowStatus("FX Mode: Press pad 1-6!");
            });
            ToggleLiveLooperCommand = new RelayCommand(_ => LiveLooperMode = !LiveLooperMode);
            ToggleSaveModeCommand = new RelayCommand(_ => IsSaveMode = !IsSaveMode);
            ToggleLoadModeCommand = new RelayCommand(_ => IsLoadMode = !IsLoadMode);
            ToggleSoundModeCommand = new RelayCommand(_ => {
                IsSoundEditMode = !IsSoundEditMode;
                if (IsSoundEditMode) ShowStatus("Select Track (1-16)...");
            });

            ToggleAudioRecordCommand = new RelayCommand(_ => {
                if (!IsRecordingAudio)
                {
                    _audio.StartRecording(SelectedInputDevice);
                    IsRecordingAudio = true;
                    ShowStatus("Recording...");
                }
                else
                {
                    var outFile = $"rec_slot{SelectedTrackIndex+1}.wav";
                    var sound = _audio.StopRecordingAndGetSound($"Rec_Slot{SelectedTrackIndex}", outFile);
                    if (sound != null)
                    {
                        if (SelectedTrackIndex >= 8)
                        {
                            var slices = sound.Slice(16);
                            _sequencer.SlicedSounds[SelectedTrackIndex] = slices;
                            ShowStatus("Audio sliced 16-ways!");
                        }
                        else
                        {
                            _sequencer.SlotSounds[SelectedTrackIndex] = sound;
                            ShowStatus("Audio sample mapped!");
                        }
                    }
                    IsRecordingAudio = false;
                }
            });

            UndoCommand = new RelayCommand(_ => { _undoManager.Undo(); UpdateStepDisplay(); });
            RedoCommand = new RelayCommand(_ => { _undoManager.Redo(); UpdateStepDisplay(); });
            ToggleStepCommand = new RelayCommand(param => {
                if(param != null)
                {
                    if (LiveLooperMode) return; // Disallow mouse clicking in live loop mode
                    ToggleStep(int.Parse(param.ToString()));
                }
            });
            SelectTrackCommand = new RelayCommand(param => { if(param != null) SelectedTrackIndex = int.Parse(param.ToString()); });

            AdjustBPMCommand = new RelayCommand(param => { if (param != null) BPM += int.Parse(param.ToString()); });
            AdjustPitchCommand = new RelayCommand(param => { if (param != null) AdjustKnobA(double.Parse(param.ToString())); });
            AdjustVolumeCommand = new RelayCommand(param => { if (param != null) AdjustKnobB(float.Parse(param.ToString())); });

            ShowHelpCommand = new RelayCommand(_ => ShowHelp());

            for (int i = 0; i < 16; i++)
            {
                Steps.Add(new StepViewModel(i, ToggleStepCommand));
            }

            _sequencer.OnStepChanged += OnStepChanged;

            _uiTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(30) }; // ~33fps UI updates
            _uiTimer.Tick += (s, e) => UpdateVisualizers();
            _uiTimer.Start();
        }

        private void TogglePlay()
        {
            if (_sequencer.Clock.IsPlaying) _sequencer.Clock.Stop();
            else _sequencer.Clock.Start();
            IsPlaying = _sequencer.Clock.IsPlaying;
        }

        private void ToggleStep(int stepIndex)
        {
            var cmd = new TapSynth.State.Commands.ToggleStepCommand(_sequencer, SelectedTrackIndex, stepIndex);
            _undoManager.Execute(cmd);
            UpdateStepDisplay();
        }

        private void UpdateStepDisplay()
        {
            var track = _sequencer.CurrentPattern.Tracks[SelectedTrackIndex];
            for (int i = 0; i < 16; i++)
            {
                Steps[i].IsActive = track.Steps[i].IsActive;
            }
        }

        private void OnStepChanged(int step)
        {
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() => {
                for (int i = 0; i < 16; i++) Steps[i].IsPlayhead = (i == step);
                if (LiveLooperMode) UpdateStepDisplay();
            });
        }

        public double SelectedTrackPitch
        {
            get => _sequencer.CurrentPattern.Tracks[SelectedTrackIndex].PitchSemitones;
            set {
                _sequencer.CurrentPattern.Tracks[SelectedTrackIndex].PitchSemitones = value;
                OnPropertyChanged();
            }
        }

        public float SelectedTrackVolume
        {
            get => _sequencer.CurrentPattern.Tracks[SelectedTrackIndex].GlobalVolume;
            set {
                _sequencer.CurrentPattern.Tracks[SelectedTrackIndex].GlobalVolume = value;
                OnPropertyChanged();
            }
        }

        public string StatusText
        {
            get => _statusText;
            set { _statusText = value; OnPropertyChanged(); }
        }
        private string _statusText;

        public System.Windows.Visibility StatusVisibility
        {
            get => _statusVisibility;
            set { _statusVisibility = value; OnPropertyChanged(); }
        }
        private System.Windows.Visibility _statusVisibility = System.Windows.Visibility.Hidden;

        private async void ShowStatus(string message)
        {
            StatusText = message;
            StatusVisibility = System.Windows.Visibility.Visible;
            await System.Threading.Tasks.Task.Delay(3000);
            if (StatusText == message)
            {
                StatusVisibility = System.Windows.Visibility.Hidden;
            }
        }

        public void HitPad(int padIndex)
        {
            if (IsSaveMode)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_sequencer.CurrentPattern, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText($"p{padIndex + 1}.json", json);
                IsSaveMode = false;
                ShowStatus($"p{padIndex + 1}.json saved");
                return;
            }

            if (IsLoadMode)
            {
                string file = $"p{padIndex + 1}.json";
                if (System.IO.File.Exists(file))
                {
                    var json = System.IO.File.ReadAllText(file);
                    _sequencer.CurrentPattern = Newtonsoft.Json.JsonConvert.DeserializeObject<TapSynth.Sequencing.Pattern>(json);
                    UpdateStepDisplay();
                    ShowStatus($"p{padIndex + 1}.json loaded");
                }
                else
                {
                    ShowStatus("Pattern not found!");
                }
                IsLoadMode = false;
                return;
            }

            if (IsFxModeActive)
            {
                TriggerPunchInFx(padIndex);
                IsFxModeActive = false; // Temporary punch-in toggle
                return;
            }

            if (IsSoundEditMode)
            {
                SelectedTrackIndex = padIndex;
                IsSoundEditMode = false;
                ShowStatus($"Track {padIndex:D2} Selected");
                return;
            }

            // Normal Playback Mode: play pitch/slice of CURRENT track!
            _sequencer.PlayPad(SelectedTrackIndex, padIndex);
        }

        private void TriggerPunchInFx(int slotIndex)
        {
            // Simple Punch-in FX mappings mapped to pads 1-16
            if (slotIndex == 0) { EnableBitcrush = !EnableBitcrush; ShowStatus(EnableBitcrush ? "FX: Bitcrush ON" : "FX: Bitcrush OFF"); }
            if (slotIndex == 1) { BPM = Math.Min(300, BPM * 2); ShowStatus("FX: Double Time"); }
            if (slotIndex == 2) { BPM = Math.Max(40, BPM / 2); ShowStatus("FX: Half Time"); }
            if (slotIndex == 3) { foreach(var t in _sequencer.CurrentPattern.Tracks) t.PitchSemitones += 12; ShowStatus("FX: Octave Up Warp"); }
            if (slotIndex == 4) { foreach(var t in _sequencer.CurrentPattern.Tracks) t.PitchSemitones -= 12; ShowStatus("FX: Sub Octave Warp"); }
            if (slotIndex == 5) { _sequencer.CurrentPattern.Clear(); UpdateStepDisplay(); ShowStatus("FX: Master Killswitch"); }
            UpdateStepDisplay();
        }

        public void AdjustKnobA(double delta)
        {
            SelectedTrackPitch = Math.Max(-24.0, Math.Min(24.0, SelectedTrackPitch + delta));
            HitPad(SelectedTrackIndex);
        }

        public void AdjustKnobB(float delta)
        {
            SelectedTrackVolume = Math.Max(0.0f, Math.Min(2.0f, SelectedTrackVolume + delta));
            HitPad(SelectedTrackIndex);
        }

        public void ResetPattern()
        {
            _sequencer.CurrentPattern = new TapSynth.Sequencing.Pattern();
            UpdateStepDisplay();
        }

        private void ShowHelp()
        {
            var msg = "TapSynth! Hotkeys:\n\n" +
                      "Keyboard Pads: 1234, QWER, ASDF, YXCV/ZXCV\n" +
                      "Play/Stop: SPACE\n" +
                      "Live Looper (WriteMode): Toggle with 'L'\n" +
                      "Bitcrush FX: SHIFT\n" +
                      "New Pattern: P\n" +
                      "Pitch (Knob A): Up/Down Arrows / Mouse Wheel\n" +
                      "Volume (Knob B): Left/Right Arrows / Shift + Scroll\n\n" +
                      "SND MODE: Click SND, then press pad 1-16 to select a Track.\n" +
                      "MIC REC: Click MIC to sample audio. Click again to save! Saving to Trk 1-8 allows melodic pitch playback. Saving to Trk 9-16 auto-slices the audio 16-ways across the pads!\n\n" +
                      "FX MODE: Click FX, then press a pad 1-6 to trigger a performance effect (e.g. Pad 1: Bitcrush, Pad 2: Double BPM).\n\n" +
                      "SAVE/LOAD: Click Save/Load, then click a pad (1-16) to store/retrieve the pattern sequence to a JSON file!";
            System.Windows.MessageBox.Show(msg, "Help", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        public void Cleanup()
        {
            _uiTimer?.Stop();
            _sequencer.Clock.Stop();
            _audio.Dispose();
        }

        public System.Windows.Media.PointCollection WaveformPoints { get; set; } = new System.Windows.Media.PointCollection();
        public System.Windows.Media.PointCollection SpectrumPoints { get; set; } = new System.Windows.Media.PointCollection();
        public double OutputLevel { get; set; }
        public double InputLevel { get; set; }

        private System.Windows.Threading.DispatcherTimer _uiTimer;

        private void UpdateVisualizers()
        {
            if (_audio.MasterAnalyzer != null)
            {
                OutputLevel = _audio.MasterAnalyzer.LastPeak * 100.0;
                OnPropertyChanged(nameof(OutputLevel));

                var wavePts = new System.Windows.Media.PointCollection();
                var buffer = _audio.MasterAnalyzer.WaveformBuffer;
                double width = 200;
                double height = 40;
                for (int i = 0; i < 2048; i += 8)
                {
                    double x = (i / 2048.0) * width;
                    double y = (height / 2) - (buffer[i] * (height / 2));
                    wavePts.Add(new System.Windows.Point(x, y));
                }
                WaveformPoints = wavePts;
                OnPropertyChanged(nameof(WaveformPoints));

                var specPts = new System.Windows.Media.PointCollection();
                double sWidth = 200;
                double sHeight = 40;

                specPts.Add(new System.Windows.Point(0, sHeight)); // Start bottom left

                for (int i = 0; i < 256; i += 2)
                {
                    double x = (i / 256.0) * sWidth;
                    double mag = _audio.MasterAnalyzer.SpectrumBuffer[i] * 600.0; // Scaled specifically for aesthetics
                    if (mag > sHeight) mag = sHeight;
                    double y = sHeight - mag;
                    specPts.Add(new System.Windows.Point(x, y));
                }

                specPts.Add(new System.Windows.Point(sWidth, sHeight)); // End bottom right

                SpectrumPoints = specPts;
                OnPropertyChanged(nameof(SpectrumPoints));
            }

            InputLevel = _audio.InputPeak * 100.0;
            OnPropertyChanged(nameof(InputLevel));
        }
    }
}
