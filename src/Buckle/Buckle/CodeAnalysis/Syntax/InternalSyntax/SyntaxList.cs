using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A syntax list of Nodes.
/// </summary>
/// <typeparam name="T">Child type of <see cref="Node" />.</typeparam>
internal sealed class SyntaxList<T> : IEnumerable<T> where T: Node {
    private readonly ImmutableArray<Node> _nodes;

    internal SyntaxList(ImmutableArray<Node> nodes) {
        _nodes = nodes;
    }

    /// <summary>
    /// Number of Nodes in collection.
    /// </summary>
    /// <returns>Count.</returns>
    internal int count => _nodes.Length;

    /// <summary>
    /// Indexes Nodes in collection.
    /// </summary>
    /// <returns><see cref="Node" /> at index.</returns>
    internal T this[int index] => (T)_nodes[index];

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
