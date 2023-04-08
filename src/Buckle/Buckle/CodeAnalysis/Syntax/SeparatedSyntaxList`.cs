using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A syntax list separated by a common <see cref="SyntaxNode" />.
/// </summary>
/// <typeparam name="T">Child type of <see cref="SyntaxNode" />.</typeparam>
internal sealed class SeparatedSyntaxList<T> : SeparatedSyntaxList, IEnumerable<T> where T : SyntaxNode {
    private readonly ImmutableArray<SyntaxNode> _nodesAndSeparators;

    internal SeparatedSyntaxList(ImmutableArray<SyntaxNode> nodesAndSeparators) {
        _nodesAndSeparators = nodesAndSeparators;
    }

    /// <summary>
    /// Number of non-separator SyntaxNodes in collection.
    /// </summary>
    /// <returns>Count.</returns>
    internal int count => (_nodesAndSeparators.Length + 1) / 2;

    /// <summary>
    /// Indexes SyntaxNodes in collection skipping separators.
    /// </summary>
    /// <returns><see cref="SyntaxNode" /> at index.</returns>
    internal T this[int index] => (T)_nodesAndSeparators[index * 2];

    /// <summary>
    /// Get a separator at an index. The index itself skips separators.
    /// </summary>
    /// <param name="index">Index of separator.</param>
    /// <returns>Separator <see cref="SyntaxToken" />.</returns>
    internal SyntaxToken GetSeparator(int index) {
        if (index == count - 1)
            return null;

        return (SyntaxToken)_nodesAndSeparators[index * 2 + 1];
    }

    internal override ImmutableArray<SyntaxNode> GetWithSeparators() => _nodesAndSeparators;

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
