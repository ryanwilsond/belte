
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Try block statement, including an <see cref="CatchClauseSyntax" /> and <see cref="FinallyClauseSyntax" />.
/// Either the catch or finally can be omitted (not both).
/// The finally block triggers whether or not the catch block threw.<br/>
/// E.g.
/// <code>
/// try {
///     ... statements that may throw ...
/// } catch {
///     ... handler code ...
/// } finally {
///     ... closing up code ...
/// }
/// </code>
/// </summary>
internal sealed partial class TryStatementSyntax : StatementSyntax {
    /// <param name="keyword">Try keyword.</param>
    /// <param name="catchClause">
    /// Either the <see cref="CatchClauseSyntax" /> or <see cref="FinallyClauseSyntax" /> is optional.
    /// </param>
    /// <param name="finallyClause">
    /// Either the <see cref="CatchClauseSyntax" /> or <see cref="FinallyClauseSyntax" /> is optional.
    /// </param>
    internal TryStatementSyntax(
        SyntaxTree syntaxTree, SyntaxToken keyword, BlockStatementSyntax body,
        CatchClauseSyntax catchClause, FinallyClauseSyntax finallyClause)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.body = body;
        this.catchClause = catchClause;
        this.finallyClause = finallyClause;
    }

    /// <summary>
    /// Try keyword.
    /// </summary>
    internal SyntaxToken keyword { get; }

    internal BlockStatementSyntax body { get; }

    internal CatchClauseSyntax? catchClause { get; }

    internal FinallyClauseSyntax? finallyClause { get; }

    internal override SyntaxKind kind => SyntaxKind.TryStatement;
}

internal sealed partial class SyntaxFactory {
    internal TryStatementSyntax TryStatement(
        SyntaxToken keyword, BlockStatementSyntax body,
        CatchClauseSyntax catchClause, FinallyClauseSyntax finallyClause) =>
        Create(new TryStatementSyntax(_syntaxTree, keyword, body, catchClause, finallyClause));
}
