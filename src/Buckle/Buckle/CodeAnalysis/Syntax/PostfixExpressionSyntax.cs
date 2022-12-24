
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Postfix expression, only two possible ones (++ or --). Cannot not be applied to literals.<br/>
/// E.g.
/// <code>
/// x++
/// </code>
/// </summary>
internal sealed partial class PostfixExpressionSyntax : ExpressionSyntax {
    /// <param name="op">Operator.</param>
    internal PostfixExpressionSyntax(SyntaxTree syntaxTree, ExpressionSyntax operand, SyntaxToken op)
        : base(syntaxTree) {
        this.operand = operand;
        this.op = op;
    }

    internal ExpressionSyntax operand { get; }

    /// <summary>
    /// Operator.
    /// </summary>
    internal SyntaxToken op { get; }

    internal override SyntaxKind kind => SyntaxKind.PostfixExpression;
}
