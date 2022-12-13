
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Try block statement, including an <see cref="CatchClause" /> and <see cref="FinallyClause" />.
/// Either the catch or finally can be omitted (not both).
/// The finally block triggers whether or not the catch block threw.
/// E.g.
/// try {
///     ... statements that may throw ...
/// } catch {
///     ... handler code ...
/// } finally {
///     ... closing up code ...
/// }
/// </summary>
internal sealed partial class TryStatement : Statement {
    internal TryStatement(
        SyntaxTree syntaxTree, Token tryKeyword, BlockStatement body,
        CatchClause catchClause, FinallyClause finallyClause)
        : base(syntaxTree) {
        this.tryKeyword = tryKeyword;
        this.body = body;
        this.catchClause = catchClause;
        this.finallyClause = finallyClause;
    }

    internal Token tryKeyword { get; }

    internal BlockStatement body { get; }

    internal CatchClause? catchClause { get; }

    internal FinallyClause? finallyClause { get; }

    internal override SyntaxType type => SyntaxType.TryStatement;
}
