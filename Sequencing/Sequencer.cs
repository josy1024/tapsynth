using System;
using TapSynth.Audio;

namespace TapSynth.Sequencing
{
    public class Sequencer
    {
        public Clock Clock { get; }
        public AudioEngine Audio { get; }

        public Pattern CurrentPattern { get; set; } = new Pattern();
        public int CurrentStep { get; private set; } 

        public CachedSound[] SlotSounds { get; } = new CachedSound[16];

        public event Action<int> OnStepChanged;

        public bool LiveLooperMode { get; set; }
        private bool[] _liveRecording = new bool[16];

        private Random _rnd = new Random();

        public Sequencer(AudioEngine audio)
        {
            Audio = audio;
            Clock = new Clock();
            Clock.OnTick += HandleTick;
        }

        private void HandleTick(int totalTicks)
        {
            CurrentStep = totalTicks % 16;
            
            OnStepChanged?.Invoke(CurrentStep);
            
            for (int i = 0; i < 16; i++)
            {
                var track = CurrentPattern.Tracks[i];
                var step = track.Steps[CurrentStep];

                if (step.IsActive)
                {
                    if (step.Probability < 100 && _rnd.Next(0, 100) > step.Probability)
                        continue;

                    double trackPitchRatio = Math.Pow(2, track.PitchSemitones / 12.0);
                    double pitch = step.PitchRatio * trackPitchRatio;
                    float vol = step.Velocity * track.GlobalVolume;

                    if (step.RandomizePitch)
                        pitch = (0.5 + _rnd.NextDouble() * 1.5) * trackPitchRatio; 
                    
                    if (step.RandomizeVelocity)
                        vol = (float)(0.3 + _rnd.NextDouble() * 0.7) * track.GlobalVolume;

                    Audio.PlaySlot(track.SlotIndex, SlotSounds[track.SlotIndex], pitch, vol, step.Pan);
                }
            }
        }
        
        public void ToggleStep(int trackIndex, int stepIndex)
        {
            if (trackIndex < 0 || trackIndex > 15 || stepIndex < 0 || stepIndex > 15) return;
            var step = CurrentPattern.Tracks[trackIndex].Steps[stepIndex];
            step.IsActive = !step.IsActive;
        }

        // Live performance
        public void PlayPad(int slotIndex)
        {
            var track = CurrentPattern.Tracks[slotIndex];
            double trackPitchRatio = Math.Pow(2, track.PitchSemitones / 12.0);
            Audio.PlaySlot(slotIndex, SlotSounds[slotIndex], trackPitchRatio, track.GlobalVolume, 0.0f);
            
            // If recording is on (Live Looper), plot it on the current step
            if (LiveLooperMode && Clock.IsPlaying)
            {
                track.Steps[CurrentStep].IsActive = true;
            }
        }
    }
}
