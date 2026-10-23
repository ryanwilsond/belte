
namespace Buckle.CodeAnalysis.Syntax;

internal static class SyntaxListBuilderExtensions {
    internal static SyntaxList<SyntaxNode> ToList(this SyntaxListBuilder builder) {
        var listNode = builder.ToListNode();

        if (listNode is null)
            return null;

        return new SyntaxList<SyntaxNode>(listNode.CreateRed());
    }

    internal static SeparatedSyntaxList<T> ToSeparatedList<T>(this SyntaxListBuilder builder) where T : SyntaxNode {
        var listNode = builder.ToListNode();

        if (listNode is null)
            return null;

        return new SeparatedSyntaxList<T>(new SyntaxNodeOrTokenList(listNode.CreateRed(), 0));
    }

    internal static bool IsNull<T>(this SyntaxListBuilder<T> builder) where T : SyntaxNode {
        return builder is null || builder.isNullInternal;
    }
}
