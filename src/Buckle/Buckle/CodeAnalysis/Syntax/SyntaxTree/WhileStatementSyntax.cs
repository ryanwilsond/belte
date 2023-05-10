
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// While statement.<br/>
/// E.g.
/// <code>
/// while (condition) {
///     ... statements ...
/// }
/// </code>
/// </summary>
internal sealed partial class WhileStatementSyntax : StatementSyntax {
    internal WhileStatementSyntax(
        SyntaxTree syntaxTree, SyntaxToken keyword, SyntaxToken openParenthesis,
        ExpressionSyntax condition, SyntaxToken closeParenthesis, StatementSyntax body)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.openParenthesis = openParenthesis;
        this.condition = condition;
        this.closeParenthesis = closeParenthesis;
        this.body = body;
    }

    public override SyntaxKind kind => SyntaxKind.WhileStatement;

    internal SyntaxToken keyword { get; }

    internal SyntaxToken openParenthesis { get; }

    internal ExpressionSyntax condition { get; }

    internal SyntaxToken closeParenthesis { get; }

    internal StatementSyntax body { get; }
}

internal sealed partial class SyntaxFactory {
    internal WhileStatementSyntax WhileStatement(
        SyntaxToken keyword, SyntaxToken openParenthesis,
        ExpressionSyntax condition, SyntaxToken closeParenthesis, StatementSyntax body)
        => Create(new WhileStatementSyntax(_syntaxTree, keyword, openParenthesis, condition, closeParenthesis, body));
}
