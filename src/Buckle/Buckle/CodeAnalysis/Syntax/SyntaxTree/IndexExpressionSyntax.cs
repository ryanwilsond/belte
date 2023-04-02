
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Index expression, only used on array types.<br/>
/// E.g.
/// <code>
/// myArr[3]
/// </code>
/// </summary>
internal sealed partial class IndexExpressionSyntax : ExpressionSyntax {
    /// <param name="operand">Anything with a type with dimension greater than 0.</param>
    /// <param name="index">Zero indexed.</param>
    internal IndexExpressionSyntax(
        SyntaxTree syntaxTree, ExpressionSyntax operand, SyntaxToken openBracket,
        ExpressionSyntax index, SyntaxToken closeBracket)
        : base(syntaxTree) {
        this.operand = operand;
        this.openBracket = openBracket;
        this.index = index;
        this.closeBracket = closeBracket;
    }

    /// <summary>
    /// Anything with a type with dimension greater than 0.
    /// </summary>
    internal ExpressionSyntax operand { get; }

    internal SyntaxToken? openBracket { get; }

    /// <summary>
    /// Zero indexed.
    /// </summary>
    internal ExpressionSyntax index { get; }

    internal SyntaxToken? closeBracket { get; }

    internal override SyntaxKind kind => SyntaxKind.IndexExpression;
}

internal sealed partial class SyntaxFactory {
    internal IndexExpressionSyntax IndexExpression(
        ExpressionSyntax operand, SyntaxToken openBracket, ExpressionSyntax index, SyntaxToken closeBracket) =>
        Create(new IndexExpressionSyntax(_syntaxTree, operand, openBracket, index, closeBracket));
}
