
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Continue statement. Only used in <see cref="WhileStatement" />, <see cref="DoWhileStatement" />, and <see cref="ForStatements" /> statements (loops).
/// E.g. continue;
/// </summary>
internal sealed partial class ContinueStatement : Statement {
    internal ContinueStatement(SyntaxTree syntaxTree, Token keyword, Token semicolon) : base(syntaxTree) {
        this.keyword = keyword;
        this.semicolon = semicolon;
    }

    internal Token keyword { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.ContinueStatement;
}
