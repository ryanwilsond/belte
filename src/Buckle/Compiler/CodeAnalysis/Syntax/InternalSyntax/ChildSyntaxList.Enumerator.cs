
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class ChildSyntaxList {
    internal struct Enumerator {
        private readonly GreenNode _node;
        private int _childIndex;

        internal Enumerator(GreenNode node) {
            _node = node;
            _childIndex = -1;
            current = null;
        }

        internal GreenNode current { get; private set; }

        internal bool MoveNext() {
            if (_node is not null) {
                while (true) {
                    _childIndex++;

                    if (_childIndex == _node.slotCount)
                        break;

                    var child = _node.GetSlot(_childIndex);

                    if (child is null)
                        continue;

                    current = child;
                    return true;
                }
            }

            current = null;
            return false;
        }
    }
}
