using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class ChildSyntaxList {
    public sealed partial class Reversed : IEnumerable<SyntaxNodeOrToken> {
        private class EnumeratorImpl : IEnumerator<SyntaxNodeOrToken> {
            private Enumerator _enumerator;

            internal EnumeratorImpl(SyntaxNode node, int count) {
                _enumerator = new Enumerator(node, count);
            }

            public SyntaxNodeOrToken Current => _enumerator.Current;

            object IEnumerator.Current => _enumerator.Current;

            public bool MoveNext() {
                return _enumerator.MoveNext();
            }

            public void Reset() {
                _enumerator.Reset();
            }

            public void Dispose() { }
        }
    }
}
