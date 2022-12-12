
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Prefix expression, only two possible ones (++ or --). Cannot be applied to literals.
/// E.g. ++x
/// </summary>
internal sealed partial class PrefixExpression : Expression {
    /// <param name="identifier">Existing variable name</param>
    /// <param name="op">Operator</param>
    internal PrefixExpression(SyntaxTree syntaxTree, Token op, Token identifier)
        : base(syntaxTree) {
        this.op = op;
        this.identifier = identifier;
    }

    /// <summary>
    /// Operator.
    /// </summary>
    internal Token op { get; }

    /// <summary>
    /// Existing variable name.
    /// </summary>
    internal Token identifier { get; }

    internal override SyntaxType type => SyntaxType.PrefixExpression;
}
