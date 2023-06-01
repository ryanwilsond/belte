using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SyntaxList<T> : IReadOnlyList<T> where T : SyntaxNode {
    public struct Enumerator {
        private readonly SyntaxList<T> _list;
        private int _index;

        internal Enumerator(SyntaxList<T> list) {
            _list = list;
            _index = -1;
        }

        public bool MoveNext() {
            int newIndex = _index + 1;

            if (newIndex < _list.Count) {
                _index = newIndex;
                return true;
            }

            return false;
        }

        public T Current => (T)_list.ItemInternal(_index);

        public void Reset() {
            _index = -1;
        }

        public override bool Equals(object? obj) {
            throw new NotSupportedException();
        }

        public override int GetHashCode() {
            throw new NotSupportedException();
        }
    }

}
