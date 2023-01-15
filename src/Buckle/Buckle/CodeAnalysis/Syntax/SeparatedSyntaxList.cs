using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A syntax list separated by SyntaxNodes.
/// </summary>
internal abstract class SeparatedSyntaxList {
    /// <summary>
    /// Gets all SyntaxNodes, including separators.
    /// </summary>
    /// <returns>Array of all SyntaxNodes.</returns>
    internal abstract ImmutableArray<SyntaxNode> GetWithSeparators();
}
