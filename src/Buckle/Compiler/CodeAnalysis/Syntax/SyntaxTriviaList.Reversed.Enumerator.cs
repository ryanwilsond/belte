using System;
using System.Runtime.InteropServices;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SyntaxTriviaList {
    public readonly partial struct Reversed {
        [StructLayout(LayoutKind.Auto)]
        public struct Enumerator {
            private readonly SyntaxToken _token;
            private readonly GreenNode _singleNodeOrList;
            private readonly int _baseIndex;
            private readonly int _count;

            private int _index;
            private GreenNode _current;
            private int _position;

            internal Enumerator(in SyntaxTriviaList list) : this() {
                if (list.node is not null) {
                    _token = list.token;
                    _singleNodeOrList = list.node;
                    _baseIndex = list.index;
                    _count = list.Count;

                    _index = _count;
                    _current = null;

                    var last = list.Last();
                    _position = last.position + last.fullWidth;
                }
            }

            public bool MoveNext() {
                if (_count == 0 || _index <= 0) {
                    _current = null;
                    return false;
                }

                _index--;

                _current = GetGreenNodeAt(_singleNodeOrList, _index);
                _position -= _current.fullWidth;

                return true;
            }

            public SyntaxTrivia Current {
                get {
                    if (_current is null)
                        throw new InvalidOperationException();

                    return new SyntaxTrivia(_token, _current, _position, _baseIndex + _index);
                }
            }
        }
    }
}
