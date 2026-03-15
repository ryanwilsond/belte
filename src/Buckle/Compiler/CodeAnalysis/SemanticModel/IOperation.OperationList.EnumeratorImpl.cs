using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis;

public partial interface IOperation {
    public readonly partial struct OperationList {
        private sealed class EnumeratorImpl : IEnumerator<IOperation> {
            private Enumerator _enumerator;

            public EnumeratorImpl(Enumerator enumerator) {
                _enumerator = enumerator;
            }

            public IOperation Current => _enumerator.Current;
            object? IEnumerator.Current => _enumerator.Current;
            public void Dispose() { }
            public bool MoveNext() => _enumerator.MoveNext();
            public void Reset() => _enumerator.Reset();
        }
    }
}
