
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A field declaration.
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
