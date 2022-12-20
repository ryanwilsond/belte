
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Binary expression, with two operands and an operator.<br/>
/// E.g.
/// <code>
/// 4 + 3
/// </code>
/// </summary>
internal sealed partial class BinaryExpression : Expression {
    /// <param name="left">Left side operand.</param>
    /// <param name="op">Operator.</param>
    /// <param name="right">Right side operand.</param>
    internal BinaryExpression(SyntaxTree syntaxTree, Expression left, Token op, Expression right)
        : base(syntaxTree) {
        this.left = left;
        this.op = op;
        this.right = right;
    }

    /// <summary>
    /// Left side operand.
    /// </summary>
    internal Expression left { get; }

    /// <summary>
    /// Operator.
    /// </summary>
    internal Token op { get; }

    /// <summary>
    /// Right side operand.
    /// </summary>
    internal Expression right { get; }

    internal override SyntaxType type => SyntaxType.BinaryExpression;
}
