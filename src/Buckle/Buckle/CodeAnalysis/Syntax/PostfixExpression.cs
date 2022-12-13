
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Postfix expression, only two possible ones (++ or --). Cannot not be applied to literals.
/// E.g. x++
/// </summary>
internal sealed partial class PostfixExpression : Expression {
    /// <param name="identifier">Existing variable name.</param>
    /// <param name="op">Operator.</param>
    internal PostfixExpression(SyntaxTree syntaxTree, Token identifier, Token op)
        : base(syntaxTree) {
        this.identifier = identifier;
        this.op = op;
    }

    /// <summary>
    /// Existing variable name.
    /// </summary>
    internal Token identifier { get; }

    /// <summary>
    /// Operator.
    /// </summary>
    internal Token op { get; }

    internal override SyntaxType type => SyntaxType.PostfixExpression;
}
