
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// If statement. Includes an optional else clause.
/// E.g.
/// if (condition) {
///     ... statements ...
/// } else {
///     ... statement ...
/// }
/// </summary>
internal sealed partial class IfStatement : Statement {
    /// <param name="condition">Condition expression, must be of type bool</param>
    /// <param name="elseClause">Else clause (optional)</param>
    internal IfStatement(
        SyntaxTree syntaxTree, Token ifKeyword, Token openParenthesis, Expression condition,
        Token closeParenthesis, Statement then, ElseClause elseClause)
        : base(syntaxTree) {
        this.ifKeyword = ifKeyword;
        this.openParenthesis = openParenthesis;
        this.condition = condition;
        this.closeParenthesis = closeParenthesis;
        this.then = then;
        this.elseClause = elseClause;
    }

    internal Token ifKeyword { get; }

    internal Token openParenthesis { get; }

    /// <summary>
    /// Condition expression, of type bool.
    /// </summary>
    internal Expression condition { get; }

    internal Token closeParenthesis { get; }

    internal Statement then { get; }

    /// <summary>
    /// Else clause (includes keyword and body).
    /// </summary>
    internal ElseClause? elseClause { get; }

    internal override SyntaxType type => SyntaxType.IfStatement;
}
