using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax {

    internal sealed class SepereratedSyntaxList<T> : IEnumerable<T> where T: Node {
        private readonly ImmutableArray<Node> seperatorsAndNodes_;
        public int count => seperatorsAndNodes_.Length + 1 / 2;
        public T this[int index] => (T)seperatorsAndNodes_[index * 2];

        public SepereratedSyntaxList(ImmutableArray<Node> seperatorsAndNodes) {
            seperatorsAndNodes_ = seperatorsAndNodes;
        }

        public Token GetSeperator(int index) => (Token)seperatorsAndNodes_[index * 2 + 1];

        public ImmutableArray<Node> GetWithSeperators() => seperatorsAndNodes_;

        public IEnumerator<T> GetEnumerator() {
            for (int i=0; i<count; i++)
                yield return this[i];
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return GetEnumerator();
        }
    }
}
