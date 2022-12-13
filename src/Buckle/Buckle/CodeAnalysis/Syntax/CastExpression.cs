
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Cast expresion (C-Style).
/// E.g. (int)3.4
/// </summary>
internal sealed partial class CastExpression : Expression {
    /// <param name="typeClause">The target type clause.</param>
    internal CastExpression(
        SyntaxTree syntaxTree, Token openParenthesis, TypeClause typeClause,
        Token closeParenthesis, Expression expression)
        : base(syntaxTree) {
        this.openParenthesis = openParenthesis;
        this.typeClause = typeClause;
        this.closeParenthesis = closeParenthesis;
        this.expression = expression;
    }

    internal Token? openParenthesis { get; }

    /// <summary>
    /// The target <see cref="TypeClause" />.
    /// </summary>
    internal TypeClause typeClause { get; }

    internal Token? closeParenthesis { get; }

    internal Expression expression { get; }

    internal override SyntaxType type => SyntaxType.CastExpression;
}
