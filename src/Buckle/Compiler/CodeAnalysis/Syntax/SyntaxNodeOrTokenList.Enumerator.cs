using System;
using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SyntaxNodeOrTokenList {
    public struct Enumerator : IEnumerator<SyntaxNodeOrToken> {
        private readonly SyntaxNodeOrTokenList _list;
        private int _index;

        internal Enumerator(in SyntaxNodeOrTokenList list) {
            _list = list;
            _index = -1;
        }

        public bool MoveNext() {
            if (_index < _list.Count) {
                _index++;
            }

            return _index < _list.Count;
        }

        public SyntaxNodeOrToken Current => _list[_index];

        object IEnumerator.Current => this.Current;

        void IEnumerator.Reset() {
            throw new NotSupportedException();
        }

        void IDisposable.Dispose() { }

        public override bool Equals(object? obj) {
            throw new NotSupportedException();
        }

        public override int GetHashCode() {
            throw new NotSupportedException();
        }
    }
}
