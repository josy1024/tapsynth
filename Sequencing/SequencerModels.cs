using System;
using Newtonsoft.Json;

namespace TapSynth.Sequencing
{
    public class StepInfo
    {
        public bool IsActive { get; set; }
        public float Velocity { get; set; } = 1.0f;
        public double PitchRatio { get; set; } = 1.0;
        public int PadIndex { get; set; } = 8; // Default root note / slice 8
        public float Pan { get; set; } = 0.0f;
        
        public bool RandomizePitch { get; set; }
        public bool RandomizeVelocity { get; set; }
        public int Probability { get; set; } = 100; // 0-100% chance to trigger

        public StepInfo Clone()
        {
            return new StepInfo { 
                IsActive = this.IsActive, 
                Velocity = this.Velocity, 
                PitchRatio = this.PitchRatio, 
                PadIndex = this.PadIndex,
                Pan = this.Pan,
                RandomizePitch = this.RandomizePitch,
                RandomizeVelocity = this.RandomizeVelocity,
                Probability = this.Probability
            };
        }
    }

    public class Track
    {
        public StepInfo[] Steps { get; set; } = new StepInfo[16];
        public bool IsMelodic { get; set; } 
        public int SlotIndex { get; set; }
        
        // Analogous to PO-33 Knob A and B for the Sound
        public double PitchSemitones { get; set; } = 0.0;
        public float GlobalVolume { get; set; } = 1.0f;

        public Track(int slotIndex)
        {
            SlotIndex = slotIndex;
            for (int i = 0; i < 16; i++)
            {
                Steps[i] = new StepInfo();
            }
        }

        // Empty constructor for serialization
        public Track() { }

        public Track Clone()
        {
            var t = new Track(SlotIndex) { IsMelodic = this.IsMelodic, PitchSemitones = this.PitchSemitones, GlobalVolume = this.GlobalVolume };
            for(int i = 0; i < 16; i++) t.Steps[i] = this.Steps[i].Clone();
            return t;
        }
    }

    public class Pattern
    {
        public Track[] Tracks { get; set; } = new Track[16];

        public Pattern()
        {
            for (int i = 0; i < 16; i++)
            {
                Tracks[i] = new Track(i);
                // First 8 typical Melodic, next 8 Drum
                Tracks[i].IsMelodic = i < 8; 
            }
        }

        public Pattern Clone()
        {
            var p = new Pattern();
            for (int i=0; i<16; i++) p.Tracks[i] = this.Tracks[i].Clone();
            return p;
        }
        
        public void Clear()
        {
            for(int i=0; i<16; i++) {
                Tracks[i] = new Track(i) { IsMelodic = i < 8 };
            }
        }
    }
}
