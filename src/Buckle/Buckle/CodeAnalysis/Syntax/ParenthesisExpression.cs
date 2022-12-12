
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Parenthesis expression, only does something doing parsing and adjusts tree order.
/// E.g. (expression)
/// Not to be confused with the call expression, parenthesis do no invocation.
/// </summary>
internal sealed partial class ParenthesisExpression : Expression {
    internal ParenthesisExpression(
        SyntaxTree syntaxTree, Token openParenthesis, Expression expression, Token closeParenthesis)
        : base(syntaxTree) {
        this.openParenthesis = openParenthesis;
        this.expression = expression;
        this.closeParenthesis = closeParenthesis;
    }

    internal Token? openParenthesis { get; }

    internal Expression expression { get; }

    internal Token? closeParenthesis { get; }

    internal override SyntaxType type => SyntaxType.ParenthesizedExpression;
}
