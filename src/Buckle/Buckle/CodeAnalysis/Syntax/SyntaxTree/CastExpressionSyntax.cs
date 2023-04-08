
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Cast expression (C-Style).<br/>
/// E.g.
/// <code>
/// (int)3.4
/// </code>
/// </summary>
internal sealed partial class CastExpressionSyntax : ExpressionSyntax {
    /// <param name="type">The target type clause.</param>
    internal CastExpressionSyntax(
        SyntaxTree syntaxTree, SyntaxToken openParenthesis, TypeSyntax type,
        SyntaxToken closeParenthesis, ExpressionSyntax expression)
        : base(syntaxTree) {
        this.openParenthesis = openParenthesis;
        this.type = type;
        this.closeParenthesis = closeParenthesis;
        this.expression = expression;
    }

    internal SyntaxToken? openParenthesis { get; }

    /// <summary>
    /// The target <see cref="TypeSyntax" />.
    /// </summary>
    internal TypeSyntax type { get; }

    internal SyntaxToken? closeParenthesis { get; }

    internal ExpressionSyntax expression { get; }

    internal override SyntaxKind kind => SyntaxKind.CastExpression;
}

internal sealed partial class SyntaxFactory {
    internal CastExpressionSyntax CastExpression(
        SyntaxToken openParenthesis, TypeSyntax type, SyntaxToken closeParenthesis, ExpressionSyntax expression)
        => Create(new CastExpressionSyntax(_syntaxTree, openParenthesis, type, closeParenthesis, expression));
}
