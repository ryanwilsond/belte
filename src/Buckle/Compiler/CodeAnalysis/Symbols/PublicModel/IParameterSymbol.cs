
namespace Buckle.CodeAnalysis.Symbols;

public interface IParameterSymbol : ISymbol {
    RefKind refKind { get; }

    bool isOptional { get; }

    ITypeSymbol type { get; }

    bool isNullable { get; }

    int ordinal { get; }

    object explicitDefaultValue { get; }
}
