
namespace Buckle.CodeAnalysis.Symbols;

public interface IFieldSymbol : ISymbol {
    bool isConstExpr { get; }

    bool isConst { get; }

    RefKind refKind { get; }

    ITypeSymbol type { get; }

    bool isNullable { get; }

    bool hasConstantValue { get; }

    object constantValue { get; }
}
