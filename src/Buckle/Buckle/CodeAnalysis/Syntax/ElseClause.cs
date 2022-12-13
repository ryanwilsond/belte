
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Else clause. Only used with the <see cref="IfStatement" />.
/// </summary>
internal sealed partial class ElseClause : Node {
    internal ElseClause(SyntaxTree syntaxTree, Token elseKeyword, Statement body) : base(syntaxTree) {
        this.elseKeyword = elseKeyword;
        this.body = body;
    }

    internal Token elseKeyword { get; }

    internal Statement body { get; }

    internal override SyntaxType type => SyntaxType.ElseClause;
}
