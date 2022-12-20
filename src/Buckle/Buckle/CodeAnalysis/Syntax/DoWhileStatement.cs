
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Do while statement.<br/>
/// E.g.
/// <code>
/// do {
///     ... statements ...
/// } while (condition);
/// </code>
/// </summary>
internal sealed partial class DoWhileStatement : Statement {
    internal DoWhileStatement(
        SyntaxTree syntaxTree, Token doKeyword, Statement body, Token whileKeyword,
        Token openParenthesis, Expression condition, Token closeParenthesis, Token semicolon)
        : base(syntaxTree) {
        this.doKeyword = doKeyword;
        this.body = body;
        this.whileKeyword = whileKeyword;
        this.openParenthesis = openParenthesis;
        this.condition = condition;
        this.closeParenthesis = closeParenthesis;
        this.semicolon = semicolon;
    }

    internal Token doKeyword { get; }

    internal Statement body { get; }

    internal Token whileKeyword { get; }

    internal Token openParenthesis { get; }

    internal Expression condition { get; }

    internal Token closeParenthesis { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.DoWhileStatement;
}
