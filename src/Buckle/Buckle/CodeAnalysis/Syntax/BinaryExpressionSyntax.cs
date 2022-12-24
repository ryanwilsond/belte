
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Binary expression, with two operands and an operator.<br/>
/// E.g.
/// <code>
/// 4 + 3
/// </code>
/// </summary>
internal sealed partial class BinaryExpressionSyntax : ExpressionSyntax {
    /// <param name="left">Left side operand.</param>
    /// <param name="op">Operator.</param>
    /// <param name="right">Right side operand.</param>
    internal BinaryExpressionSyntax(
        SyntaxTree syntaxTree, ExpressionSyntax left, SyntaxToken op, ExpressionSyntax right)
        : base(syntaxTree) {
        this.left = left;
        this.op = op;
        this.right = right;
    }

    /// <summary>
    /// Left side operand.
    /// </summary>
    internal ExpressionSyntax left { get; }

    /// <summary>
    /// Operator.
    /// </summary>
    internal SyntaxToken op { get; }

    /// <summary>
    /// Right side operand.
    /// </summary>
    internal ExpressionSyntax right { get; }

    internal override SyntaxKind kind => SyntaxKind.BinaryExpression;
}
