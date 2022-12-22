
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Prefix expression, only two possible ones (++ or --). Cannot be applied to literals.<br/>
/// E.g.
/// <code>
/// ++x
/// </code>
/// </summary>
internal sealed partial class PrefixExpression : Expression {
    /// <param name="op">Operator.</param>
    internal PrefixExpression(SyntaxTree syntaxTree, Token op, Expression operand)
        : base(syntaxTree) {
        this.op = op;
        this.operand = operand;
    }

    /// <summary>
    /// Operator.
    /// </summary>
    internal Token op { get; }

    /// <summary>
    /// Existing variable name.
    /// </summary>
    internal Expression operand { get; }

    internal override SyntaxType type => SyntaxType.PrefixExpression;
}
