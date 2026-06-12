
namespace Buckle.CodeAnalysis.Symbols;

public interface ITypeSymbol : INamespaceOrTypeSymbol {
    TypeKind typeKind { get; }

    INamedTypeSymbol baseType { get; }

    bool isReferenceType { get; }

    bool isValueType { get; }

    SpecialType specialType { get; }
}
