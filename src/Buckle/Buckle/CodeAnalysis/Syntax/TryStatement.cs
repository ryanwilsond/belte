
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Try block statement, including an <see cref="CatchClause" /> and <see cref="FinallyClause" />.
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
internal sealed partial class TryStatement : Statement {
    /// <param name="keyword">Try keyword.</param>
    /// <param name="catchClause">
    /// Either the <see cref="CatchClause" /> or <see cref="FinallyClause" /> is optional.
    /// </param>
    /// <param name="finallyClause">
    /// Either the <see cref="CatchClause" /> or <see cref="FinallyClause" /> is optional.
    /// </param>
    internal TryStatement(
        SyntaxTree syntaxTree, Token keyword, BlockStatement body,
        CatchClause catchClause, FinallyClause finallyClause)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.body = body;
        this.catchClause = catchClause;
        this.finallyClause = finallyClause;
    }

    /// <summary>
    /// Try keyword.
    /// </summary>
    internal Token keyword { get; }

    internal BlockStatement body { get; }

    internal CatchClause? catchClause { get; }

    internal FinallyClause? finallyClause { get; }

    internal override SyntaxType type => SyntaxType.TryStatement;
}
