
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A field declaration, syntactically identical to <see cref="VariableDeclarationStatement" /> except it is apart
/// of a type declaration, and cannot have an initializer (for now).</br>
/// E.g.
/// <code>
/// int a;
/// </code>
/// </summary>
internal sealed partial class FieldDeclaration : Member {
    internal FieldDeclaration(
        SyntaxTree syntaxTree, VariableDeclarationStatement declaration)
        : base(syntaxTree) {
        this.declaration = declaration;
    }

    internal VariableDeclarationStatement declaration { get; }

    internal override SyntaxType type => SyntaxType.FieldDeclaration;
}
