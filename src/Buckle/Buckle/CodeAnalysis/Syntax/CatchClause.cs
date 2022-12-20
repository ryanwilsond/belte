
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Catch clause. Only used with the <see cref="TryStatement" />.<br/>
/// E.g. (see <see cref="TryStatement" />)
/// <code>
/// ... catch { ... }
/// </code>
/// </summary>
internal sealed partial class CatchClause : Node {
    /// <param name="keyword">Catch keyword.</param>
    internal CatchClause(SyntaxTree syntaxTree, Token keyword, BlockStatement body) : base(syntaxTree) {
        this.keyword = keyword;
        this.body = body;
    }

    /// <summary>
    /// Catch keyword.
    /// </summary>
    internal Token keyword { get; }

    internal BlockStatement body { get; }

    internal override SyntaxType type => SyntaxType.CatchClause;
}
