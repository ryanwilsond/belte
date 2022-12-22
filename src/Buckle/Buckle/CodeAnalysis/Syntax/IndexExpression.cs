
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Index expression, only used on array types.<br/>
/// E.g.
/// <code>
/// myArr[3]
/// </code>
/// </summary>
internal sealed partial class IndexExpression : Expression {
    /// <param name="operand">Anything with a type with dimension greater than 0.</param>
    /// <param name="index">Zero indexed.</param>
    internal IndexExpression(
        SyntaxTree syntaxTree, Expression operand, Token openBracket, Expression index, Token closeBracket)
        : base(syntaxTree) {
        this.operand = operand;
        this.openBracket = openBracket;
        this.index = index;
        this.closeBracket = closeBracket;
    }

    /// <summary>
    /// Anything with a type with dimension greater than 0.
    /// </summary>
    internal Expression operand { get; }

    internal Token? openBracket { get; }

    /// <summary>
    /// Zero indexed.
    /// </summary>
    internal Expression index { get; }

    internal Token? closeBracket { get; }

    internal override SyntaxType type => SyntaxType.IndexExpression;
}
