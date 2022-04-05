using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax {

    internal abstract class SeparatedSyntaxList {
        public abstract ImmutableArray<Node> GetWithSeparators();
    }

    internal sealed class SeparatedSyntaxList<T> : SeparatedSyntaxList, IEnumerable<T> where T: Node {
        private readonly ImmutableArray<Node> nodesAndSeparators_;
        public int count => (nodesAndSeparators_.Length + 1) / 2;
        public T this[int index] => (T)nodesAndSeparators_[index * 2];

        public SeparatedSyntaxList(ImmutableArray<Node> nodesAndSeparators) {
            nodesAndSeparators_ = nodesAndSeparators;
        }

        public Token GetSeparator(int index) => (Token)nodesAndSeparators_[index * 2 + 1];

        public override ImmutableArray<Node> GetWithSeparators() => nodesAndSeparators_;

        public IEnumerator<T> GetEnumerator() {
            for (int i=0; i<count; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
