
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Break statement. Only used in <see cref="WhileStatementSyntax" />, <see cref="DoWhileStatementSyntax" />,
/// and <see cref="ForStatementSyntax" /> statements (loops).<br/>
/// E.g.
/// <code>
/// break;
/// </code>
/// </summary>
internal sealed partial class BreakStatementSyntax : StatementSyntax {
    internal BreakStatementSyntax(SyntaxTree syntaxTree, SyntaxToken keyword, SyntaxToken semicolon)
        : base(syntaxTree) {
        this.keyword = keyword;
        this.semicolon = semicolon;
    }

    internal SyntaxToken keyword { get; }

    internal SyntaxToken semicolon { get; }

    internal override SyntaxKind kind => SyntaxKind.BreakStatement;
}

internal sealed partial class SyntaxFactory {
    internal BreakStatementSyntax BreakStatement(SyntaxToken keyword, SyntaxToken semicolon) =>
        Create(new BreakStatementSyntax(_syntaxTree, keyword, semicolon));
}
