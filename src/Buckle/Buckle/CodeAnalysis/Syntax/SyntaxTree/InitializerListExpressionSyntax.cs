
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Initializer list expression, to initialize array types.<br/>
/// E.g.
/// <code>
/// { 1, 2, 3 }
/// </code>
/// </summary>
internal sealed partial class InitializerListExpressionSyntax : ExpressionSyntax {
    internal InitializerListExpressionSyntax(SyntaxTree syntaxTree,
        SyntaxToken openBrace, SeparatedSyntaxList<ExpressionSyntax> items, SyntaxToken closeBrace)
        : base(syntaxTree) {
        this.openBrace = openBrace;
        this.items = items;
        this.closeBrace = closeBrace;
    }

    internal SyntaxToken? openBrace { get; }

    internal SeparatedSyntaxList<ExpressionSyntax> items { get; }

    internal SyntaxToken? closeBrace { get; }

    internal override SyntaxKind kind => SyntaxKind.LiteralExpression;
}

internal sealed partial class SyntaxFactory {
    internal InitializerListExpressionSyntax InitializerListExpression(
        SyntaxToken openBrace, SeparatedSyntaxList<ExpressionSyntax> items, SyntaxToken closeBrace) =>
        Create(new InitializerListExpressionSyntax(_syntaxTree, openBrace, items, closeBrace));
}
