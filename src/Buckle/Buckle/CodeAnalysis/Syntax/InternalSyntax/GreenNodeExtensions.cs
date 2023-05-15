
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal static class GreenNodeExtensions {
    internal static SyntaxList<T> ToGreenList<T>(this SyntaxNode node) where T : GreenNode {
        return node != null ? ToGreenList<T>(node.green) : null;
    }

    internal static SeparatedSyntaxList<T> ToGreenSeparatedList<T>(this SyntaxNode node) where T : GreenNode {
        return node != null ? new SeparatedSyntaxList<T>(ToGreenList<T>(node.green)) : null;
    }

    internal static SyntaxList<T> ToGreenList<T>(this GreenNode node) where T : GreenNode {
        return new SyntaxList<T>(node);
    }
}
