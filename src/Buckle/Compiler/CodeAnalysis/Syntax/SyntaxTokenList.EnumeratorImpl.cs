using System;
using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SyntaxTokenList {
    private class EnumeratorImpl : IEnumerator<SyntaxToken> {
        private Enumerator _enumerator;

        internal EnumeratorImpl(in SyntaxTokenList list) {
            _enumerator = new Enumerator(in list);
        }

        public SyntaxToken Current => _enumerator.Current;

        object IEnumerator.Current => _enumerator.Current;

        public bool MoveNext() {
            return _enumerator.MoveNext();
        }

        public void Reset() {
            throw new NotSupportedException();
        }

        public void Dispose() { }
    }
}
