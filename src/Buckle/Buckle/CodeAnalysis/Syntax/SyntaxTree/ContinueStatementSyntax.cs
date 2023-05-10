
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Continue statement. Only used in <see cref="WhileStatementSyntax" />, <see cref="DoWhileStatementSyntax" />,
/// and <see cref="ForStatementSyntax" /> statements (loops).<br/>
/// E.g.
/// <code>
/// continue;
/// </code>
/// </summary>
internal sealed partial class ContinueStatementSyntax : StatementSyntax {
    internal ContinueStatementSyntax(SyntaxTree syntaxTree, SyntaxToken keyword, SyntaxToken semicolon)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.semicolon = semicolon;
    }

    public override SyntaxKind kind => SyntaxKind.ContinueStatement;

    internal SyntaxToken keyword { get; }

    internal SyntaxToken semicolon { get; }
}

internal sealed partial class SyntaxFactory {
    internal ContinueStatementSyntax ContinueStatement(SyntaxToken keyword, SyntaxToken semicolon)
        => Create(new ContinueStatementSyntax(_syntaxTree, keyword, semicolon));
}
