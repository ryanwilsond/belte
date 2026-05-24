using System;
using System.Collections;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

public sealed partial class SyntaxTriviaList {
    public readonly partial struct Reversed : IEnumerable<SyntaxTrivia>, IEquatable<Reversed> {
        private readonly SyntaxTriviaList _list;

        public Reversed(SyntaxTriviaList list) {
            _list = list;
        }

        public Enumerator GetEnumerator() {
            return new Enumerator(in _list);
        }

        IEnumerator<SyntaxTrivia> IEnumerable<SyntaxTrivia>.GetEnumerator() {
            if (_list.Count == 0)
                return SpecializedCollections.EmptyEnumerator<SyntaxTrivia>();

            return new ReversedEnumeratorImpl(_list);
        }

        IEnumerator IEnumerable.GetEnumerator() {
            if (_list.Count == 0)
                return SpecializedCollections.EmptyEnumerator<SyntaxTrivia>();

            return new ReversedEnumeratorImpl(_list);
        }

        public override int GetHashCode() {
            return _list.GetHashCode();
        }

        public override bool Equals(object? obj) {
            return obj is Reversed reversed && Equals(reversed);
        }

        public bool Equals(Reversed other) {
            return _list.Equals(other._list);
        }
    }
}
