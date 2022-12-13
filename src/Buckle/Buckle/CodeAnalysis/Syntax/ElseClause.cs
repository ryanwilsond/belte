
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Else clause. Only used with the <see cref="IfStatement" />.
/// </summary>
internal sealed partial class ElseClause : Node {
    /// <param name="keyword">Else keyword.</param>
    internal ElseClause(SyntaxTree syntaxTree, Token keyword, Statement body) : base(syntaxTree) {
        this.keyword = keyword;
        this.body = body;
    }

    /// <summary>
    /// Else keyword.
    /// </summary>
    internal Token keyword { get; }

    internal Statement body { get; }

    internal override SyntaxType type => SyntaxType.ElseClause;
}
