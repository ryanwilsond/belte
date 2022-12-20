using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A syntax list separated by Nodes.
/// </summary>
internal abstract class SeparatedSyntaxList {
    /// <summary>
    /// Gets all Nodes, including separators.
    /// </summary>
    /// <returns>Array of all Nodes.</returns>
    internal abstract ImmutableArray<Node> GetWithSeparators();
}

/// <summary>
/// A syntax list separated by a common <see cref="Node" />.
/// </summary>
/// <typeparam name="T">Child type of <see cref="Node" />.</typeparam>
internal sealed class SeparatedSyntaxList<T> : SeparatedSyntaxList, IEnumerable<T> where T: Node {
    private readonly ImmutableArray<Node> _nodesAndSeparators;

    internal SeparatedSyntaxList(ImmutableArray<Node> nodesAndSeparators) {
        _nodesAndSeparators = nodesAndSeparators;
    }

    /// <summary>
    /// Number of non-separator Nodes in collection.
    /// </summary>
    /// <returns>Count.</returns>
    internal int count => (_nodesAndSeparators.Length + 1) / 2;

    /// <summary>
    /// Indexes Nodes in collection skipping separators.
    /// </summary>
    /// <returns><see cref="Node" /> at index.</returns>
    internal T this[int index] => (T)_nodesAndSeparators[index * 2];

    /// <summary>
    /// Get a separator at an index. The index itself skips separators.
    /// </summary>
    /// <param name="index">Index of separator.</param>
    /// <returns>Separator <see cref="Token" />.</returns>
    internal Token GetSeparator(int index) {
        if (index == count - 1)
            return null;

        return (Token)_nodesAndSeparators[index * 2 + 1];
    }

    internal override ImmutableArray<Node> GetWithSeparators() => _nodesAndSeparators;

    /// <summary>
    /// Gets enumerator of all Nodes.
    /// </summary>
    /// <returns>Yields all Nodes.</returns>
    public IEnumerator<T> GetEnumerator() {
        for (int i=0; i<count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
