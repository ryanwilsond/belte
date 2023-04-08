
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Prefix expression, only two possible ones (++ or --). Cannot be applied to literals.<br/>
/// E.g.
/// <code>
/// ++x
/// </code>
/// </summary>
internal sealed partial class PrefixExpressionSyntax : ExpressionSyntax {
    /// <param name="op">Operator.</param>
    internal PrefixExpressionSyntax(SyntaxTree syntaxTree, SyntaxToken op, ExpressionSyntax operand)
        : base(syntaxTree) {
        this.op = op;
        this.operand = operand;
    }

    /// <summary>
    /// Operator.
    /// </summary>
    internal SyntaxToken op { get; }

    /// <summary>
    /// Existing variable name.
    /// </summary>
    internal ExpressionSyntax operand { get; }

    internal override SyntaxKind kind => SyntaxKind.PrefixExpression;
}

internal sealed partial class SyntaxFactory {
    internal PrefixExpressionSyntax PrefixExpression(SyntaxToken op, ExpressionSyntax operand)
        => Create(new PrefixExpressionSyntax(_syntaxTree, op, operand));
}
