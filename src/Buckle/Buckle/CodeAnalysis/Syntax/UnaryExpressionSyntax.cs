
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Unary expression, has higher precedence than binary expressions.<br/>
/// E.g.
/// <code>
/// -3
/// </code>
/// </summary>
internal sealed partial class UnaryExpressionSyntax : ExpressionSyntax {
    /// <param name="op">Operator.</param>
    internal UnaryExpressionSyntax(SyntaxTree syntaxTree, SyntaxToken op, ExpressionSyntax operand) : base(syntaxTree) {
        this.op = op;
        this.operand = operand;
    }

    /// <summary>
    /// Operator.
    /// </summary>
    internal SyntaxToken op { get; }

    internal ExpressionSyntax operand { get; }

    internal override SyntaxKind kind => SyntaxKind.UnaryExpression;
}
