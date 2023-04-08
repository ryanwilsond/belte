
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Else clause. Only used with the <see cref="IfStatementSyntax" />.
/// </summary>
internal sealed partial class ElseClauseSyntax : SyntaxNode {
    /// <param name="keyword">Else keyword.</param>
    internal ElseClauseSyntax(SyntaxTree syntaxTree, SyntaxToken keyword, StatementSyntax body) : base(syntaxTree) {
        this.keyword = keyword;
        this.body = body;
    }

    /// <summary>
    /// Else keyword.
    /// </summary>
    internal SyntaxToken keyword { get; }

    internal StatementSyntax body { get; }

    internal override SyntaxKind kind => SyntaxKind.ElseClause;
}

internal sealed partial class SyntaxFactory {
    internal ElseClauseSyntax ElseClause(SyntaxToken keyword, StatementSyntax body)
        => Create(new ElseClauseSyntax(_syntaxTree, keyword, body));
}
