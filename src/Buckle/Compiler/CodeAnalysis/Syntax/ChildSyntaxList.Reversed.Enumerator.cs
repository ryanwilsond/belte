using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class ChildSyntaxList {
    public sealed partial class Reversed : IEnumerable<SyntaxNodeOrToken> {
        public struct Enumerator {
            private readonly SyntaxNode? _node;
            private readonly int _count;
            private int _childIndex;

            internal Enumerator(SyntaxNode node, int count) {
                _node = node;
                _count = count;
                _childIndex = count;
            }

            public bool MoveNext() {
                return --_childIndex >= 0;
            }

            public SyntaxNodeOrToken Current => ItemInternal(_node, _childIndex);

            public void Reset() {
                _childIndex = _count;
            }
        }
    }
}
