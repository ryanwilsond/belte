
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Return statement. Only used in function bodies, or scopes that are inside a function body.
/// Have an optional return value.<br/>
/// E.g.
/// <code>
/// return 3;
/// </code>
/// </summary>
internal sealed partial class ReturnStatementSyntax : StatementSyntax {
    /// <param name="expression">Return value (optional).</param>
    internal ReturnStatementSyntax(
        SyntaxTree syntaxTree, SyntaxToken keyword, ExpressionSyntax expression, SyntaxToken semicolon)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.expression = expression;
        this.semicolon = semicolon;
    }

    internal SyntaxToken keyword { get; }

    /// <summary>
    /// Return value (optional).
    /// </summary>
    internal ExpressionSyntax? expression { get; }

    internal SyntaxToken semicolon { get; }

    internal override SyntaxKind kind => SyntaxKind.ReturnStatement;
}