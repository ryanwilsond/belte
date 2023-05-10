
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Finally clause. Only used with the <see cref="TryStatementSyntax" />.<br/>
/// E.g. (see <see cref="TryStatementSyntax" />)
/// <code>
/// ... finally { ... }
/// </code>
/// </summary>
internal sealed partial class FinallyClauseSyntax : SyntaxNode {
    /// <param name="keyword">Finally keyword.</param>
    internal FinallyClauseSyntax(SyntaxTree syntaxTree, SyntaxToken keyword, BlockStatementSyntax body)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.body = body;
    }

    public override SyntaxKind kind => SyntaxKind.FinallyClause;

    /// <summary>
    /// Finally keyword.
    /// </summary>
    internal SyntaxToken keyword { get; }

    internal BlockStatementSyntax body { get; }
}

internal sealed partial class SyntaxFactory {
    internal FinallyClauseSyntax FinallyClause(SyntaxToken keyword, BlockStatementSyntax body)
        => Create(new FinallyClauseSyntax(_syntaxTree, keyword, body));
}
