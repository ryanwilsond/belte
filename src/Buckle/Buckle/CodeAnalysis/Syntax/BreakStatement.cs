
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Break statement. Only used in while, do while, and for statements (loops).
/// E.g. break;
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
