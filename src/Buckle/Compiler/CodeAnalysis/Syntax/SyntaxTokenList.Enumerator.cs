using System;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SyntaxTokenList {
    public struct Enumerator {
        private readonly SyntaxNode _parent;
        private readonly GreenNode _singleNodeOrList;
        private readonly int _baseIndex;
        private readonly int _count;

        private int _index;
        private GreenNode _current;
        private int _position;

        internal Enumerator(in SyntaxTokenList list) {
            _parent = list._parent;
            _singleNodeOrList = list.node;
            _baseIndex = list._index;
            _count = list.Count;

            _index = -1;
            _current = null;
            _position = list.position;
        }

        public bool MoveNext() {
            if (_count == 0 || _count <= _index + 1) {
                _current = null;
                return false;
            }

            _index++;

            if (_current is not null)
                _position += _current.fullWidth;

            _current = GetGreenNodeAt(_singleNodeOrList, _index);
            return true;
        }

        public SyntaxToken Current {
            get {
                if (_current is null)
                    throw new InvalidOperationException();

                return new SyntaxToken(_parent, _current, _position, _baseIndex + _index);
            }
        }

        public override bool Equals(object obj) {
            throw new NotSupportedException();
        }

        public override int GetHashCode() {
            throw new NotSupportedException();
        }
    }
}
