
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// If statement. Includes an optional <see cref="ElseClause" />.<br/>
/// E.g.
/// <code>
/// if (condition) {
///     ... statements ...
/// } else {
///     ... statement ...
/// }
/// </code>
/// </summary>
internal sealed partial class IfStatement : Statement {
    /// <param name="keyword">If keyword.</param>
    /// <param name="condition">Condition <see cref="Expression" />, must be of type bool.</param>
    /// <param name="elseClause"><see cref="ElseClause" /> (optional).</param>
    internal IfStatement(
        SyntaxTree syntaxTree, Token keyword, Token openParenthesis, Expression condition,
        Token closeParenthesis, Statement then, ElseClause elseClause)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.openParenthesis = openParenthesis;
        this.condition = condition;
        this.closeParenthesis = closeParenthesis;
        this.then = then;
        this.elseClause = elseClause;
    }

    /// <summary>
    /// If keyword.
    /// </summary>
    internal Token keyword { get; }

    internal Token openParenthesis { get; }

    /// <summary>
    /// Condition <see cref="Expression" />, of type bool.
    /// </summary>
    internal Expression condition { get; }

    internal Token closeParenthesis { get; }

    internal Statement then { get; }

    /// <summary>
    /// <see cref="ElseClause" /> (includes keyword and body).
    /// </summary>
    internal ElseClause? elseClause { get; }

    internal override SyntaxType type => SyntaxType.IfStatement;
}
