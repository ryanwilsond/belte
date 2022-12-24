
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

    internal SyntaxToken keyword { get; }

    internal SyntaxToken openParenthesis { get; }

    internal ExpressionSyntax condition { get; }

    internal SyntaxToken closeParenthesis { get; }

    internal StatementSyntax body { get; }

    internal override SyntaxKind kind => SyntaxKind.WhileStatement;
}
