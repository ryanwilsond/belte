
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class ChildSyntaxList {
    internal struct Enumerator {
        private readonly GreenNode _node;
        private int _childIndex;
        private GreenNode _list;
        private int _listIndex;
        private GreenNode _currentChild;

        internal Enumerator(GreenNode node) {
            _node = node;
            _childIndex = -1;
            _listIndex = -1;
            _list = null;
            _currentChild = null;
        }

        internal GreenNode current => _currentChild;

        internal bool MoveNext() {
            if (_node is not null) {
                if (_list is not null) {
                    _listIndex++;

                    if (_listIndex < _list.slotCount) {
                        _currentChild = _list.GetSlot(_listIndex);
                        return true;
                    }

                    _list = null;
                    _listIndex = -1;
                }

                while (true) {
                    _childIndex++;

                    if (_childIndex == _node.slotCount)
                        break;

                    var child = _node.GetSlot(_childIndex);

                    if (child is null)
                        continue;

                    if (child.kind == GreenNode.ListKind) {
                        _list = child;
                        _listIndex++;

                        if (_listIndex < _list.slotCount) {
                            _currentChild = _list.GetSlot(_listIndex);
                            return true;
                        } else {
                            _list = null;
                            _listIndex = -1;
                            continue;
                        }
                    } else {
                        _currentChild = child;
                    }

                    return true;
                }
            }

            _currentChild = null;
            return false;
        }
    }
}
