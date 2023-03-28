
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A member (anything, global or local).
/// </summary>
public abstract class MemberSyntax : SyntaxNode {
    protected MemberSyntax(SyntaxTree syntaxTree) : base(syntaxTree) { }
}
