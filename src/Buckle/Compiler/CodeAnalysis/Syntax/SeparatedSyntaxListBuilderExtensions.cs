namespace Buckle.CodeAnalysis.Syntax;

internal static class SeparatedSyntaxListBuilderExtensions
{
    internal static bool IsNull<T>(this SeparatedSyntaxListBuilder<T> builder) where T : SyntaxNode
    {
        return builder is null || builder.isNullInternal;
    }
}
