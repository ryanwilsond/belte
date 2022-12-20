
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A member (anything, global or local).
/// </summary>
internal abstract class Member : Node {
    protected Member(SyntaxTree syntaxTree) : base(syntaxTree) { }
}
