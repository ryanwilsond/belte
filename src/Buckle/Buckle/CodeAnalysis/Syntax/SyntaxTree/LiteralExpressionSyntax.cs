
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Literal expression, such as a number or a string.<br/>
/// E.g.
/// <code>
/// "Hello, world!"
/// 34.6
/// </code>
/// </summary>
internal sealed partial class LiteralExpressionSyntax : ExpressionSyntax {
    internal LiteralExpressionSyntax(SyntaxTree syntaxTree, SyntaxToken token, object value) : base(syntaxTree) {
        this.token = token;
        this.value = value;
    }

    internal LiteralExpressionSyntax(SyntaxTree syntaxTree, SyntaxToken token)
        : this(syntaxTree, token, token.value) { }

    internal SyntaxToken token { get; }

    internal object value { get; }

    internal override SyntaxKind kind => SyntaxKind.LiteralExpression;
}

internal sealed partial class SyntaxFactory {
    internal LiteralExpressionSyntax Literal(SyntaxToken token, object value)
        => Create(new LiteralExpressionSyntax(_syntaxTree, token, value));

    internal LiteralExpressionSyntax Literal(SyntaxToken token)
        => Create(new LiteralExpressionSyntax(_syntaxTree, token, token.value));
}
