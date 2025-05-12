
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal abstract partial class TypeSyntax {
    internal bool isImplicitlyTyped => IsIdentifierName("var") || this is EmptyNameSyntax;

    private bool IsIdentifierName(string id) {
        return this is IdentifierNameSyntax name && name.identifier.text == id;
    }
}
