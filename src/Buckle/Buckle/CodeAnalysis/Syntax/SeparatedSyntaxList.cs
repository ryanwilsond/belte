using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A syntax list separated by nodes.
/// </summary>
internal abstract class SeparatedSyntaxList {
    /// <summary>
    /// Gets all nodes, including separators.
    /// </summary>
    /// <returns>Array of all nodes</returns>
    internal abstract ImmutableArray<Node> GetWithSeparators();
}

/// <summary>
/// A syntax list separated by a common node.
/// </summary>
/// <typeparam name="T">Child type of Node</typeparam>
internal sealed class SeparatedSyntaxList<T> : SeparatedSyntaxList, IEnumerable<T> where T: Node {
    private readonly ImmutableArray<Node> nodesAndSeparators_;

    internal SeparatedSyntaxList(ImmutableArray<Node> nodesAndSeparators) {
        nodesAndSeparators_ = nodesAndSeparators;
    }

    /// <summary>
    /// Number of non separator nodes in collection.
    /// </summary>
    /// <returns>Count</returns>
    internal int count => (nodesAndSeparators_.Length + 1) / 2;

    /// <summary>
    /// Indexes nodes in collection skipping separators.
    /// </summary>
    /// <returns>Node at index</returns>
    internal T this[int index] => (T)nodesAndSeparators_[index * 2];

    /// <summary>
    /// Get a separator at an index. The index itself skips separators.
    /// </summary>
    /// <param name="index">Index of separator</param>
    /// <returns>Separator token</returns>
    internal Token GetSeparator(int index) {
        if (index == count - 1)
            return null;

        return (Token)nodesAndSeparators_[index * 2 + 1];
    }

    internal override ImmutableArray<Node> GetWithSeparators() => nodesAndSeparators_;

    /// <summary>
    /// Gets enumerator of all nodes.
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
