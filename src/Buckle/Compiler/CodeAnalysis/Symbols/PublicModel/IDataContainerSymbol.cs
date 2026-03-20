
namespace Buckle.CodeAnalysis.Symbols;

public interface IDataContainerSymbol : ISymbol {
    ITypeSymbol type { get; }

    bool isNullable { get; }

    bool isConstExpr { get; }

    bool isConst { get; }

    bool isRef { get; }

    RefKind refKind { get; }

    bool hasConstantValue { get; }

    object constantValue { get; }
}
