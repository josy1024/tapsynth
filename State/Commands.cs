using TapSynth.Sequencing;

namespace TapSynth.State.Commands
{
    public class ToggleStepCommand : IUndoableCommand
    {
        private readonly Sequencer _sequencer;
        private readonly int _trackIndex;
        private readonly int _stepIndex;

        public ToggleStepCommand(Sequencer sequencer, int trackIndex, int stepIndex)
        {
            _sequencer = sequencer;
            _trackIndex = trackIndex;
            _stepIndex = stepIndex;
        }

        public void Execute()
        {
            _sequencer.ToggleStep(_trackIndex, _stepIndex);
        }

        public void Undo()
        {
            _sequencer.ToggleStep(_trackIndex, _stepIndex);
        }
    }
}
