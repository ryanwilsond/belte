
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Catch clause. Only used with the <see cref="TryStatementSyntax" />.<br/>
/// E.g. (see <see cref="TryStatementSyntax" />)
/// <code>
/// ... catch { ... }
/// </code>
/// </summary>
internal sealed partial class CatchClauseSyntax : SyntaxNode {
    /// <param name="keyword">Catch keyword.</param>
    internal CatchClauseSyntax(SyntaxTree syntaxTree, SyntaxToken keyword, BlockStatementSyntax body)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.body = body;
    }

    public override SyntaxKind kind => SyntaxKind.CatchClause;

    /// <summary>
    /// Catch keyword.
    /// </summary>
    internal SyntaxToken keyword { get; }

    internal BlockStatementSyntax body { get; }
}

internal sealed partial class SyntaxFactory {
    internal CatchClauseSyntax CatchClause(SyntaxToken keyword, BlockStatementSyntax body)
        => Create(new CatchClauseSyntax(_syntaxTree, keyword, body));
}
