
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Ternary expression, with three operands and two operators.<br/>
/// E.g.
/// <code>
/// true ? 3 : 5
/// </code>
/// </summary>
internal sealed partial class TernaryExpression : Expression {
    /// <param name="left">Left side operand.</param>
    /// <param name="leftOp">Left operator.</param>
    /// <param name="center">Center operand.</param>
    /// <param name="rightOp">Right operator.</param>
    /// <param name="right">Right side operand.</param>
    internal TernaryExpression(
        SyntaxTree syntaxTree, Expression left, Token leftOp, Expression center, Token rightOp, Expression right)
        : base(syntaxTree) {
        this.left = left;
        this.leftOp = leftOp;
        this.center = center;
        this.rightOp = rightOp;
        this.right = right;
    }

    /// <summary>
    /// Left side operand.
    /// </summary>
    internal Expression left { get; }

    /// <summary>
    /// Left operator.
    /// </summary>
    internal Token leftOp { get; }

    /// <summary>
    /// Center operand.
    /// </summary>
    internal Expression center { get; }

    /// <summary>
    /// Right operator.
    /// </summary>
    internal Token rightOp { get; }

    /// <summary>
    /// Right side operand.
    /// </summary>
    internal Expression right { get; }

    internal override SyntaxType type => SyntaxType.TernaryExpression;
}
