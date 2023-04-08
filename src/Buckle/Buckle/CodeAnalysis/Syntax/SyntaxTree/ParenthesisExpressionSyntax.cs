
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Parenthesis expression, only does something during parsing and adjusts tree order.<br/>
/// E.g.
/// <code>
/// (expression)
/// </code>
/// Not to be confused with the <see cref="CallExpressionSyntax" />, parenthesis do no invocation.
/// </summary>
internal sealed partial class ParenthesisExpressionSyntax : ExpressionSyntax {
    internal ParenthesisExpressionSyntax(
        SyntaxTree syntaxTree, SyntaxToken openParenthesis, ExpressionSyntax expression, SyntaxToken closeParenthesis)
        : base(syntaxTree) {
        this.openParenthesis = openParenthesis;
        this.expression = expression;
        this.closeParenthesis = closeParenthesis;
    }

    internal SyntaxToken? openParenthesis { get; }

    internal ExpressionSyntax expression { get; }

    internal SyntaxToken? closeParenthesis { get; }

    internal override SyntaxKind kind => SyntaxKind.ParenthesizedExpression;
}

internal sealed partial class SyntaxFactory {
    internal ParenthesisExpressionSyntax ParenthesisExpression(
        SyntaxToken openParenthesis, ExpressionSyntax expression, SyntaxToken closeParenthesis)
        => Create(new ParenthesisExpressionSyntax(_syntaxTree, openParenthesis, expression, closeParenthesis));
}
