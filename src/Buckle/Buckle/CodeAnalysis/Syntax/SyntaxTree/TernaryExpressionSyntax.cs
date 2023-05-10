
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Ternary expression, with three operands and two operators.<br/>
/// E.g.
/// <code>
/// true ? 3 : 5
/// </code>
/// </summary>
internal sealed partial class TernaryExpressionSyntax : ExpressionSyntax {
    /// <param name="left">Left side operand.</param>
    /// <param name="leftOp">Left operator.</param>
    /// <param name="center">Center operand.</param>
    /// <param name="rightOp">Right operator.</param>
    /// <param name="right">Right side operand.</param>
    internal TernaryExpressionSyntax(
        SyntaxTree syntaxTree, ExpressionSyntax left, SyntaxToken leftOp,
        ExpressionSyntax center, SyntaxToken rightOp, ExpressionSyntax right)
        : base(syntaxTree) {
        this.left = left;
        this.leftOp = leftOp;
        this.center = center;
        this.rightOp = rightOp;
        this.right = right;
    }

    public override SyntaxKind kind => SyntaxKind.TernaryExpression;

    /// <summary>
    /// Left side operand.
    /// </summary>
    internal ExpressionSyntax left { get; }

    /// <summary>
    /// Left operator.
    /// </summary>
    internal SyntaxToken leftOp { get; }

    /// <summary>
    /// Center operand.
    /// </summary>
    internal ExpressionSyntax center { get; }

    /// <summary>
    /// Right operator.
    /// </summary>
    internal SyntaxToken rightOp { get; }

    /// <summary>
    /// Right side operand.
    /// </summary>
    internal ExpressionSyntax right { get; }
}

internal sealed partial class SyntaxFactory {
    internal TernaryExpressionSyntax TernaryExpression(
        ExpressionSyntax left, SyntaxToken leftOp,
        ExpressionSyntax center, SyntaxToken rightOp, ExpressionSyntax right)
        => Create(new TernaryExpressionSyntax(_syntaxTree, left, leftOp, center, rightOp, right));
}
