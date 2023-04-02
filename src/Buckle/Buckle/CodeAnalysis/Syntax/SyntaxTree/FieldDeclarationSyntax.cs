
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// A field declaration, syntactically identical to <see cref="VariableDeclarationStatementSyntax" /> except it is apart
/// of a type declaration, and cannot have an initializer (for now).</br>
/// E.g.
/// <code>
/// int a;
/// </code>
/// </summary>
internal sealed partial class FieldDeclarationSyntax : MemberSyntax {
    internal FieldDeclarationSyntax(SyntaxTree syntaxTree, VariableDeclarationStatementSyntax declaration)
        : base(syntaxTree) {
        this.declaration = declaration;
    }

    internal VariableDeclarationStatementSyntax declaration { get; }

    internal override SyntaxKind kind => SyntaxKind.FieldDeclaration;
}

internal sealed partial class SyntaxFactory {
    internal FieldDeclarationSyntax FieldDeclaration(VariableDeclarationStatementSyntax declaration) =>
        Create(new FieldDeclarationSyntax(_syntaxTree, declaration));
}
