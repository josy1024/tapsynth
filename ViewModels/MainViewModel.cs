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
            ToggleFxModeCommand = new RelayCommand(_ => IsFxModeActive = !IsFxModeActive);
            ToggleLiveLooperCommand = new RelayCommand(_ => LiveLooperMode = !LiveLooperMode);
            ToggleSaveModeCommand = new RelayCommand(_ => IsSaveMode = !IsSaveMode);
            ToggleLoadModeCommand = new RelayCommand(_ => IsLoadMode = !IsLoadMode);

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

        public void HitPad(int slotIndex)
        {
            if (IsSaveMode)
            {
                var json = Newtonsoft.Json.JsonConvert.SerializeObject(_sequencer.CurrentPattern, Newtonsoft.Json.Formatting.Indented);
                System.IO.File.WriteAllText($"p{slotIndex + 1}.json", json);
                IsSaveMode = false;
                System.Windows.MessageBox.Show($"Pattern saved to slot {slotIndex + 1} (p{slotIndex + 1}.json)", "Saved");
                return;
            }

            if (IsLoadMode)
            {
                string file = $"p{slotIndex + 1}.json";
                if (System.IO.File.Exists(file))
                {
                    var json = System.IO.File.ReadAllText(file);
                    _sequencer.CurrentPattern = Newtonsoft.Json.JsonConvert.DeserializeObject<TapSynth.Sequencing.Pattern>(json);
                    UpdateStepDisplay();
                    System.Windows.MessageBox.Show($"Pattern loaded from slot {slotIndex + 1} (p{slotIndex + 1}.json)", "Loaded");
                }
                else
                {
                    System.Windows.MessageBox.Show("Pattern slot is empty!", "Load Failed");
                }
                IsLoadMode = false;
                return;
            }

            if (IsFxModeActive)
            {
                TriggerPunchInFx(slotIndex);
                IsFxModeActive = false; // Temporary punch-in toggle
                return;
            }

            _sequencer.PlayPad(slotIndex);
        }

        private void TriggerPunchInFx(int slotIndex)
        {
            // Simple Punch-in FX mappings mapped to pads 1-16
            if (slotIndex == 0) EnableBitcrush = !EnableBitcrush; // FX 1: Toggle global bitcrusher
            if (slotIndex == 1) BPM = Math.Min(300, BPM * 2); // FX 2: Double time stutter
            if (slotIndex == 2) BPM = Math.Max(40, BPM / 2); // FX 3: Half time slowdown
            if (slotIndex == 3) { foreach(var t in _sequencer.CurrentPattern.Tracks) t.PitchSemitones += 12; } // FX 4: Octave up warp
            if (slotIndex == 4) { foreach(var t in _sequencer.CurrentPattern.Tracks) t.PitchSemitones -= 12; } // FX 5: Octave down warp
            if (slotIndex == 5) { _sequencer.CurrentPattern.Clear(); UpdateStepDisplay(); } // FX 6: Master killswitch (clear current pattern visually)
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
            var msg = "TapSynth K.O! Hotkeys:\n\n" +
                      "Keyboard Pads: 1234, QWER, ASDF, YXCV/ZXCV\n" +
                      "Play/Stop: SPACE\n" +
                      "Live Looper (WriteMode): Toggle with 'L' or hold 'W'\n" +
                      "Bitcrush FX: SHIFT\n" +
                      "New Pattern: P\n" +
                      "Pitch (Knob A): Up/Down Arrows / Mouse Wheel\n" +
                      "Volume (Knob B): Left/Right Arrows / Shift + Scroll\n\n" +
                      "FX MODE: Toggle FX, then press a pad 1-16 to trigger a performance effect (e.g. Pad 1: Bitcrush, Pad 2: Double BPM, Pad 4: Octave Up).\n\n" +
                      "SAVE/LOAD: Click Save/Load, then click a pad (1-16) to store/retrieve the pattern sequence to a JSON file!\n\n" +
                      "If the sequencer is playing and Live Rec is on, hitting pads will record steps!";
            System.Windows.MessageBox.Show(msg, "Help", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
        }

        public void Cleanup()
        {
            _sequencer.Clock.Stop();
            _audio.Dispose();
        }
    }
}
