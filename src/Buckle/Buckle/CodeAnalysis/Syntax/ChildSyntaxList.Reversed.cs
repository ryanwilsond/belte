using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class ChildSyntaxList {
    public sealed partial class Reversed : IEnumerable<SyntaxNodeOrToken> {
        private readonly SyntaxNode? _node;
        private readonly int _count;

        internal Reversed(SyntaxNode node, int count) {
            _node = node;
            _count = count;
        }

        public Enumerator GetEnumerator() {
            return new Enumerator(_node, _count);
        }

        IEnumerator<SyntaxNodeOrToken> IEnumerable<SyntaxNodeOrToken>.GetEnumerator() {
            if (_node == null)
                return new EmptyEnumerator<SyntaxNodeOrToken>();

            return new EnumeratorImpl(_node, _count);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            if (_node == null)
                return new EmptyEnumerator<SyntaxNodeOrToken>();

            return new EnumeratorImpl(_node, _count);
        }
    }
}
