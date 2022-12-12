
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Unary expression, has higher precedence than binary expressions.
/// E.g. -3
/// </summary>
internal sealed partial class UnaryExpression : Expression {
    /// <param name="op">Operator</param>
    internal UnaryExpression(SyntaxTree syntaxTree, Token op, Expression operand) : base(syntaxTree) {
        this.op = op;
        this.operand = operand;
    }

    /// <summary>
    /// Operator.
    /// </summary>
    internal Token op { get; }

    internal Expression operand { get; }

    internal override SyntaxType type => SyntaxType.UnaryExpression;
}
