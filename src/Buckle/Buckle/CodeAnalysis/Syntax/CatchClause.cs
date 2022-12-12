
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Catch clause. Only used with the try statement.
/// E.g. (see TryStatement)
/// ... catch { ... }
/// </summary>
internal sealed partial class CatchClause : Node {
    internal CatchClause(SyntaxTree syntaxTree, Token catchKeyword, BlockStatement body) : base(syntaxTree) {
        this.catchKeyword = catchKeyword;
        this.body = body;
    }

    internal Token catchKeyword { get; }

    internal BlockStatement body { get; }

    internal override SyntaxType type => SyntaxType.CatchClause;
}
