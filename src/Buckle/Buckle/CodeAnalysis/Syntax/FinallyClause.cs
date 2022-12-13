
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Finally clause. Only used with the <see cref="TryStatement" />.<br/>
/// E.g. (see <see cref="TryStatement" />)
/// <code>
/// ... finally { ... }
/// </code>
/// </summary>
internal sealed partial class FinallyClause : Node {
    /// <param name="keyword">Finally keyword.</param>
    internal FinallyClause(SyntaxTree syntaxTree, Token keyword, BlockStatement body) : base(syntaxTree) {
        this.keyword = keyword;
        this.body = body;
    }

    /// <summary>
    /// Finally keyword.
    /// </summary>
    internal Token keyword { get; }

    internal BlockStatement body { get; }

    internal override SyntaxType type => SyntaxType.FinallyClause;
}
