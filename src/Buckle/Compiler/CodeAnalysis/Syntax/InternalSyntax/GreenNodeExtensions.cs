
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Extensions on the <see cref="GreenNode" /> class.
/// </summary>
internal static class GreenNodeExtensions {
    /// <summary>
    /// Converts a <see cref="SyntaxNode" /> to a <see cref="SyntaxList<T>" />.
    /// </summary>
    internal static SyntaxList<T> ToGreenList<T>(this SyntaxNode node) where T : GreenNode {
        return node is not null ? ToGreenList<T>(node.green) : null;
    }

    /// <summary>
    /// Converts a <see cref="SyntaxNode" /> to a <see cref="SeparatedSyntaxList<T>" />.
    /// </summary>
    internal static SeparatedSyntaxList<T> ToGreenSeparatedList<T>(this SyntaxNode node) where T : GreenNode {
        return node is not null ? new SeparatedSyntaxList<T>(ToGreenList<T>(node.green)) : null;
    }

    /// <summary>
    /// Converts a <see cref="GreenNode" /> to a <see cref="SyntaxList<T>" />.
    /// </summary>
    internal static SyntaxList<T> ToGreenList<T>(this GreenNode node) where T : GreenNode {
        return new SyntaxList<T>(node);
    }
}
