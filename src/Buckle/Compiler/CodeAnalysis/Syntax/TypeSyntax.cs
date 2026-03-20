
namespace Buckle.CodeAnalysis.Syntax;

public abstract partial class TypeSyntax {
    public bool isImplicitlyTyped => ((InternalSyntax.TypeSyntax)green).isImplicitlyTyped;
}
