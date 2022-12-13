
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// For statement, uses 3 part system, not for each.
/// E.g.
/// for (iterator declaration; condition; step) {
///     ... statements ...
/// }
/// </summary>
internal sealed partial class ForStatement : Statement {
    /// <param name="initializer">Declaration or name of variable used for stepping.</param>
    internal ForStatement(
        SyntaxTree syntaxTree, Token keyword, Token openParenthesis, Statement initializer,
        Expression condition, Token semicolon, Expression step, Token closeParenthesis, Statement body)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.openParenthesis = openParenthesis;
        this.initializer = initializer;
        this.condition = condition;
        this.semicolon = semicolon;
        this.step = step;
        this.closeParenthesis = closeParenthesis;
        this.body = body;
    }

    internal Token keyword { get; }

    internal Token openParenthesis { get; }

    /// <summary>
    /// Declaration or name of variable used for stepping.
    /// </summary>
    internal Statement initializer { get; }

    internal Expression condition { get; }

    internal Token semicolon { get; }

    internal Expression step { get; }

    internal Token closeParenthesis { get; }

    internal Statement body { get; }

    internal override SyntaxType type => SyntaxType.ForStatement;
}
