namespace Buckle.CodeAnalysis;

public partial interface IOperation {
    public readonly partial struct OperationList {
        public struct Enumerator {
            private readonly Operation _operation;
            private int _currentSlot;
            private int _currentIndex;

            internal Enumerator(Operation operation) {
                _operation = operation;
                _currentSlot = -1;
                _currentIndex = -1;
            }

            public IOperation Current => _operation.GetCurrent(_currentSlot, _currentIndex);

            public bool MoveNext() {
                bool result;
                (result, _currentSlot, _currentIndex) = _operation.MoveNext(_currentSlot, _currentIndex);
                return result;
            }

            public void Reset() {
                _currentSlot = -1;
                _currentIndex = -1;
            }
        }
    }
}
