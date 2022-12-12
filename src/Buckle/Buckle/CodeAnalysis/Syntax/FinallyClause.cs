
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Finally clause. Only used with the try statement.
/// E.g. (see TryStatement)
/// ... finally { ... }
/// </summary>
internal sealed partial class FinallyClause : Node {
    internal FinallyClause(SyntaxTree syntaxTree, Token finallyKeyword, BlockStatement body) : base(syntaxTree) {
        this.finallyKeyword = finallyKeyword;
        this.body = body;
    }

    internal Token finallyKeyword { get; }

    internal BlockStatement body { get; }

    internal override SyntaxType type => SyntaxType.FinallyClause;
}
