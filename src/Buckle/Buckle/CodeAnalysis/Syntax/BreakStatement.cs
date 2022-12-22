
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Break statement. Only used in <see cref="WhileStatement" />, <see cref="DoWhileStatement" />,
/// and <see cref="ForStatements" /> statements (loops).<br/>
/// E.g.
/// <code>
/// break;
/// </code>
/// </summary>
internal sealed partial class BreakStatement : Statement {
    internal BreakStatement(SyntaxTree syntaxTree, Token keyword, Token semicolon) : base(syntaxTree) {
        this.keyword = keyword;
        this.semicolon = semicolon;
    }

    internal Token keyword { get; }

    internal Token semicolon { get; }

    internal override SyntaxType type => SyntaxType.BreakStatement;
}
