using System.Collections.Generic;

namespace TapSynth.State
{
    public interface IUndoableCommand
    {
        void Execute();
        void Undo();
    }

    public class UndoRedoManager
    {
        private Stack<IUndoableCommand> _undoStack = new Stack<IUndoableCommand>();
        private Stack<IUndoableCommand> _redoStack = new Stack<IUndoableCommand>();

        public void Execute(IUndoableCommand command)
        {
            command.Execute();
            _undoStack.Push(command);
            _redoStack.Clear(); // Clear redo on new action
        }

        public void Undo()
        {
            if (_undoStack.Count > 0)
            {
                var cmd = _undoStack.Pop();
                cmd.Undo();
                _redoStack.Push(cmd);
            }
        }

        public void Redo()
        {
            if (_redoStack.Count > 0)
            {
                var cmd = _redoStack.Pop();
                cmd.Execute();
                _undoStack.Push(cmd);
            }
        }
    }
}
