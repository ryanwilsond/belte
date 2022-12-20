
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Initializer list expression, to initialize array types.<br/>
/// E.g.
/// <code>
/// { 1, 2, 3 }
/// </code>
/// </summary>
internal sealed partial class InitializerListExpression : Expression {
    internal InitializerListExpression(SyntaxTree syntaxTree,
        Token openBrace, SeparatedSyntaxList<Expression> items, Token closeBrace)
        : base(syntaxTree) {
        this.openBrace = openBrace;
        this.items = items;
        this.closeBrace = closeBrace;
    }

    internal Token? openBrace { get; }

    internal SeparatedSyntaxList<Expression> items { get; }

    internal Token? closeBrace { get; }

    internal override SyntaxType type => SyntaxType.LiteralExpression;
}
