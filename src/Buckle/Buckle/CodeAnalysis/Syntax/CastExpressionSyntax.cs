
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Cast expresion (C-Style).<br/>
/// E.g.
/// <code>
/// (int)3.4
/// </code>
/// </summary>
internal sealed partial class CastExpressionSyntax : ExpressionSyntax {
    /// <param name="typeClause">The target type clause.</param>
    internal CastExpressionSyntax(
        SyntaxTree syntaxTree, SyntaxToken openParenthesis, TypeClauseSyntax typeClause,
        SyntaxToken closeParenthesis, ExpressionSyntax expression)
        : base(syntaxTree) {
        this.openParenthesis = openParenthesis;
        this.typeClause = typeClause;
        this.closeParenthesis = closeParenthesis;
        this.expression = expression;
    }

    internal SyntaxToken? openParenthesis { get; }

    /// <summary>
    /// The target <see cref="TypeClauseSyntax" />.
    /// </summary>
    internal TypeClauseSyntax typeClause { get; }

    internal SyntaxToken? closeParenthesis { get; }

    internal ExpressionSyntax expression { get; }

    internal override SyntaxKind kind => SyntaxKind.CastExpression;
}
