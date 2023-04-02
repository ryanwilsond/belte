
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
internal sealed partial class DoWhileStatementSyntax : StatementSyntax {
    internal DoWhileStatementSyntax(
        SyntaxTree syntaxTree, SyntaxToken doKeyword, StatementSyntax body, SyntaxToken whileKeyword,
        SyntaxToken openParenthesis, ExpressionSyntax condition, SyntaxToken closeParenthesis, SyntaxToken semicolon)
        : base(syntaxTree) {
        this.doKeyword = doKeyword;
        this.body = body;
        this.whileKeyword = whileKeyword;
        this.openParenthesis = openParenthesis;
        this.condition = condition;
        this.closeParenthesis = closeParenthesis;
        this.semicolon = semicolon;
    }

    internal SyntaxToken doKeyword { get; }

    internal StatementSyntax body { get; }

    internal SyntaxToken whileKeyword { get; }

    internal SyntaxToken openParenthesis { get; }

    internal ExpressionSyntax condition { get; }

    internal SyntaxToken closeParenthesis { get; }

    internal SyntaxToken semicolon { get; }

    internal override SyntaxKind kind => SyntaxKind.DoWhileStatement;
}

internal sealed partial class SyntaxFactory {
    internal DoWhileStatementSyntax DoWhileStatement(
        SyntaxToken doKeyword, StatementSyntax body, SyntaxToken whileKeyword, SyntaxToken openParenthesis,
        ExpressionSyntax condition, SyntaxToken closeParenthesis, SyntaxToken semicolon) =>
        Create(new DoWhileStatementSyntax(
            _syntaxTree, doKeyword, body, whileKeyword, openParenthesis, condition, closeParenthesis, semicolon)
        );
}
