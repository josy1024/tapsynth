using System.Windows.Input;

namespace TapSynth.ViewModels
{
    public class StepViewModel : ViewModelBase
    {
        private bool _isActive;
        public bool IsActive
        {
            get => _isActive;
            set { _isActive = value; OnPropertyChanged(); }
        }

        private bool _isPlayhead;
        public bool IsPlayhead
        {
            get => _isPlayhead;
            set { _isPlayhead = value; OnPropertyChanged(); }
        }

        public string DisplayText { get; }
        public int Index { get; }
        public ICommand ToggleCommand { get; }

        public StepViewModel(int index, ICommand toggleCommand)
        {
            Index = index;
            DisplayText = (index + 1).ToString();
            ToggleCommand = toggleCommand;
        }
    }
}
