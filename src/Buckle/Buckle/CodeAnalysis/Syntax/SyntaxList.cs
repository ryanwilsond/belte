using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A syntax list of SyntaxNodes.
/// </summary>
/// <typeparam name="T">Child type of <see cref="SyntaxNode" />.</typeparam>
internal sealed class SyntaxList<T> : IEnumerable<T> where T : SyntaxNode {
    private readonly ImmutableArray<T> _nodes;

    internal SyntaxList(ImmutableArray<T> nodes) {
        _nodes = nodes;
    }

    /// <summary>
    /// Number of SyntaxNodes in collection.
    /// </summary>
    /// <returns>Count.</returns>
    internal int count => _nodes.Length;

    /// <summary>
    /// Indexes SyntaxNodes in collection.
    /// </summary>
    /// <returns><see cref="SyntaxNode" /> at index.</returns>
    internal T this[int index] => (T)_nodes[index];

    /// <summary>
    /// Gets enumerator of all SyntaxNodes.
    /// </summary>
    /// <returns>Yields all SyntaxNodes.</returns>
    public IEnumerator<T> GetEnumerator() {
        for (var i = 0; i < count; i++)
            yield return this[i];
    }

    IEnumerator IEnumerable.GetEnumerator() {
        return GetEnumerator();
    }
}
