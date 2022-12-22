
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Postfix expression, only two possible ones (++ or --). Cannot not be applied to literals.<br/>
/// E.g.
/// <code>
/// x++
/// </code>
/// </summary>
internal sealed partial class PostfixExpression : Expression {
    /// <param name="op">Operator.</param>
    internal PostfixExpression(SyntaxTree syntaxTree, Expression operand, Token op)
        : base(syntaxTree) {
        this.operand = operand;
        this.op = op;
    }

    internal Expression operand { get; }

    /// <summary>
    /// Operator.
    /// </summary>
    internal Token op { get; }

    internal override SyntaxType type => SyntaxType.PostfixExpression;
}
