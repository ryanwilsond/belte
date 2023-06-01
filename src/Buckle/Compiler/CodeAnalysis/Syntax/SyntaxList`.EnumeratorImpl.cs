using System;
using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SyntaxList<T> : IReadOnlyList<T> where T : SyntaxNode {
    private class EnumeratorImpl : IEnumerator<T> {
        private Enumerator _enumerator;

        internal EnumeratorImpl(in SyntaxList<T> list) {
            _enumerator = new Enumerator(list);
        }

        public bool MoveNext() {
            return _enumerator.MoveNext();
        }

        public T Current => _enumerator.Current;

        void IDisposable.Dispose() { }

        object IEnumerator.Current => _enumerator.Current;

        void IEnumerator.Reset() {
            _enumerator.Reset();
        }
    }
}
