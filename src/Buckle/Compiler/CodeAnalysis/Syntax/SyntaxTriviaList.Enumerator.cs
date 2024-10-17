using System;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SyntaxTriviaList {
    public struct Enumerator {
        private SyntaxToken _token;
        private GreenNode? _singleNodeOrList;
        private int _baseIndex;
        private int _count;

        private int _index;
        private GreenNode? _current;
        private int _position;

        internal Enumerator(SyntaxTriviaList list) {
            _token = list.token;
            _singleNodeOrList = list.node;
            _baseIndex = list.index;
            _count = list.Count;

            _index = -1;
            _current = null;
            _position = list.position;
        }

        private void InitializeFrom(SyntaxToken token, GreenNode greenNode, int index, int position) {
            _token = token;
            _singleNodeOrList = greenNode;
            _baseIndex = index;
            _count = greenNode.isList ? greenNode.slotCount : 1;

            _index = -1;
            _current = null;
            _position = position;
        }

        internal void InitializeFromLeadingTrivia(SyntaxToken token) {
            var node = token.node.GetLeadingTrivia();
            InitializeFrom(token, node, 0, token.position);
        }

        internal void InitializeFromTrailingTrivia(SyntaxToken token) {
            var leading = token.node.GetLeadingTrivia();
            var index = 0;

            if (leading is not null) {
                index = leading.isList ? leading.slotCount : 1;
            }

            var trailingGreen = token.node.GetTrailingTrivia();
            var trailingPosition = token.position + token.fullWidth;

            if (trailingGreen is not null)
                trailingPosition -= trailingGreen.fullWidth;

            InitializeFrom(token, trailingGreen, index, trailingPosition);
        }

        public bool MoveNext() {
            var newIndex = _index + 1;

            if (newIndex >= _count) {
                _current = null;
                return false;
            }

            _index = newIndex;

            if (_current is not null) {
                _position += _current.fullWidth;
            }

            _current = GetGreenNodeAt(_singleNodeOrList, newIndex);
            return true;
        }

        public SyntaxTrivia Current {
            get {
                if (_current is null)
                    throw new InvalidOperationException();

                return new SyntaxTrivia(_token, _current, _position, _baseIndex + _index);
            }
        }

        internal bool TryMoveNextAndGetCurrent(out SyntaxTrivia current) {
            if (!MoveNext()) {
                current = default;
                return false;
            }

            current = new SyntaxTrivia(_token, _current, _position, _baseIndex + _index);
            return true;
        }
    }
}
