
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// While statement.
/// E.g.
/// while (condition) {
///     ... statements ...
/// }
/// </summary>
internal sealed partial class WhileStatement : Statement {
    internal WhileStatement(
        SyntaxTree syntaxTree, Token keyword, Token openParenthesis,
        Expression condition, Token closeParenthesis, Statement body)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.openParenthesis = openParenthesis;
        this.condition = condition;
        this.closeParenthesis = closeParenthesis;
        this.body = body;
    }

    internal Token keyword { get; }

    internal Token openParenthesis { get; }

    internal Expression condition { get; }

    internal Token closeParenthesis { get; }

    internal Statement body { get; }

    internal override SyntaxType type => SyntaxType.WhileStatement;
}
