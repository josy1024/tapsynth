using System.Windows;
using System.Windows.Input;
using TapSynth.ViewModels;

namespace TapSynth
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        // Map computer keyboard keys to the 16 slots/pads
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.IsRepeat) return; // Ignore hold repetition
            
            if (DataContext is MainViewModel vm)
            {
                int slotHit = -1;

                switch (e.Key)
                {
                    // Top Row (1-4)
                    case Key.D1: slotHit = 0; break;
                    case Key.D2: slotHit = 1; break;
                    case Key.D3: slotHit = 2; break;
                    case Key.D4: slotHit = 3; break;
                    // Second Row (5-8)
                    case Key.Q: slotHit = 4; break;
                    case Key.W: slotHit = 5; break;
                    case Key.E: slotHit = 6; break;
                    case Key.R: slotHit = 7; break;
                    // Third Row (9-12)
                    case Key.A: slotHit = 8; break;
                    case Key.S: slotHit = 9; break;
                    case Key.D: slotHit = 10; break;
                    case Key.F: slotHit = 11; break;
                    // Fourth Row (13-16) - Support both Z and Y due to QWERTY/QWERTZ layout variations
                    case Key.Z: case Key.Y: slotHit = 12; break;
                    case Key.X: slotHit = 13; break;
                    case Key.C: slotHit = 14; break;
                    case Key.V: slotHit = 15; break;
                    
                    // Hardware Controls
                    case Key.Space:
                        vm.TogglePlayCommand.Execute(null);
                        break;
                    case Key.LeftShift:
                    case Key.RightShift:
                        vm.EnableBitcrush = !vm.EnableBitcrush;
                        break;
                    case Key.P:
                        // Pattern switch placeholder
                        vm.ResetPattern();
                        break;
                    case Key.L:
                        // Toggle Live Looper / Write Mode
                        vm.LiveLooperMode = !vm.LiveLooperMode;
                        break;
                        
                    // Arrow Keys / Media Keys mapped to Knob A (Pitch) and Knob B (Volume)
                    case Key.Up:
                    case Key.MediaNextTrack:
                        vm.AdjustKnobA(1.0); return;
                    case Key.Down:
                    case Key.MediaPreviousTrack:
                        vm.AdjustKnobA(-1.0); return;
                    case Key.Right:
                    case Key.VolumeUp:
                        vm.AdjustKnobB(0.05f); return;
                    case Key.Left:
                    case Key.VolumeDown:
                        vm.AdjustKnobB(-0.05f); return;
                }

                if (slotHit != -1)
                {
                    vm.HitPad(slotHit);
                }
            }
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
                    vm.AdjustKnobB(e.Delta > 0 ? 0.05f : -0.05f);
                else
                    vm.AdjustKnobA(e.Delta > 0 ? 0.05 : -0.05);
            }
        }

        private void Window_Closed(object sender, System.EventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.Cleanup();
            }
        }
    }
}