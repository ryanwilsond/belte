namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class ChildSyntaxList {
    public struct Enumerator {
        private SyntaxNode? _node;
        private int _count;
        private int _childIndex;

        internal Enumerator(SyntaxNode node, int count) {
            _node = node;
            _count = count;
            _childIndex = -1;
        }

        internal void InitializeFrom(SyntaxNode node) {
            _node = node;
            _count = CountNodes(node.green);
            _childIndex = -1;
        }

        public bool MoveNext() {
            var newIndex = _childIndex + 1;

            if (newIndex < _count) {
                _childIndex = newIndex;
                return true;
            }

            return false;
        }

        public SyntaxNodeOrToken Current => ItemInternal(_node, _childIndex);

        internal void Reset() {
            _childIndex = -1;
        }

        internal bool TryMoveNextAndGetCurrent(out SyntaxNodeOrToken current) {
            if (!MoveNext()) {
                current = default;
                return false;
            }

            current = ItemInternal(_node, _childIndex);
            return true;
        }

        internal SyntaxNode? TryMoveNextAndGetCurrentAsNode() {
            while (MoveNext()) {
                var nodeValue = ItemInternalAsNode(_node, _childIndex);

                if (nodeValue is not null) {
                    return nodeValue;
                }
            }

            return null;
        }
    }
}
