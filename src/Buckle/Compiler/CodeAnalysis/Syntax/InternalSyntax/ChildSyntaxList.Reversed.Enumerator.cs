
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal sealed partial class ChildSyntaxList {
    internal sealed partial class Reversed {
        internal struct Enumerator {
            private readonly GreenNode _node;
            private int _childIndex;
            private GreenNode _list;
            private int _listIndex;
            private GreenNode _currentChild;

            internal Enumerator(GreenNode node) {
                if (node != null) {
                    _node = node;
                    _childIndex = node.slotCount;
                    _listIndex = -1;
                } else {
                    _node = null;
                    _childIndex = 0;
                    _listIndex = -1;
                }

                _list = null;
                _currentChild = null;
            }

            public bool MoveNext() {
                if (_node != null) {
                    if (_list != null) {
                        if (--_listIndex >= 0) {
                            _currentChild = _list.GetSlot(_listIndex);
                            return true;
                        }

                        _list = null;
                        _listIndex = -1;
                    }

                    while (--_childIndex >= 0) {
                        var child = _node.GetSlot(_childIndex);
                        if (child is null) {
                            continue;
                        }

                        if (child.isList) {
                            _list = child;
                            _listIndex = _list.slotCount;
                            if (--_listIndex >= 0) {
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

            public GreenNode Current => _currentChild;
        }
    }
}
