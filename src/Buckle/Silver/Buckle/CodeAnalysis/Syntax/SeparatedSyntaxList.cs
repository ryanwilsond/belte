using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

internal abstract class SeparatedSyntaxList {
    /// <summary>
    /// Gets all nodes, including separators
    /// </summary>
    /// <returns>Array of all nodes</returns>
    internal abstract ImmutableArray<Node> GetWithSeparators();
}

internal sealed class SeparatedSyntaxList<T> : SeparatedSyntaxList, IEnumerable<T> where T: Node {
    private readonly ImmutableArray<Node> nodesAndSeparators_;
    internal int count => (nodesAndSeparators_.Length + 1) / 2;
    internal T this[int index] => (T)nodesAndSeparators_[index * 2];

    internal SeparatedSyntaxList(ImmutableArray<Node> nodesAndSeparators) {
        nodesAndSeparators_ = nodesAndSeparators;
    }

    internal Token GetSeparator(int index) {
        if (index == count - 1)
            return null;

        return (Token)nodesAndSeparators_[index * 2 + 1];
    }

    internal override ImmutableArray<Node> GetWithSeparators() => nodesAndSeparators_;

    /// <summary>
    /// Gets enumerator of all nodes
    /// </summary>
    /// <returns>All nodes</returns>
    public IEnumerator<T> GetEnumerator() {
        for (int i=0; i<count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
