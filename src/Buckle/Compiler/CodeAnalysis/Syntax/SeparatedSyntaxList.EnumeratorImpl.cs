using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SeparatedSyntaxList<T> : IReadOnlyList<T> where T : SyntaxNode {
    private class EnumeratorImpl : IEnumerator<T> {
        private Enumerator _enumerator;

        internal EnumeratorImpl(SeparatedSyntaxList<T> list) {
            _enumerator = new Enumerator(list);
        }

        public T Current => _enumerator.Current;

        object IEnumerator.Current => _enumerator.Current;

        public void Dispose() { }

        public bool MoveNext() {
            return _enumerator.MoveNext();
        }

        public void Reset() {
            _enumerator.Reset();
        }
    }
}
