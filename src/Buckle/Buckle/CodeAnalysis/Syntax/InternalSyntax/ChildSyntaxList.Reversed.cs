
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class ChildSyntaxList {
    internal sealed partial class Reversed {
        private readonly GreenNode _node;

        internal Reversed(GreenNode node) {
            _node = node;
        }

        public Enumerator GetEnumerator() {
            return new Enumerator(_node);
        }
    }
}
