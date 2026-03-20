
namespace Buckle.CodeAnalysis.Symbols;

public interface ITypeSymbol : INamespaceOrTypeSymbol {
    TypeKind typeKind { get; }

    INamedTypeSymbol baseType { get; }

    bool isObjectType { get; }

    bool isPrimitiveType { get; }

    SpecialType specialType { get; }
}
