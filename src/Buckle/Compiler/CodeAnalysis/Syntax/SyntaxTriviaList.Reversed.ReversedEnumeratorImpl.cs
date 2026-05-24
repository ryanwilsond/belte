using System;
using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SyntaxTriviaList {
    public readonly partial struct Reversed {
        private class ReversedEnumeratorImpl : IEnumerator<SyntaxTrivia> {
            private Enumerator _enumerator;

            internal ReversedEnumeratorImpl(SyntaxTriviaList list) {
                _enumerator = new Enumerator(list);
            }

            public SyntaxTrivia Current => _enumerator.Current;

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
}
