
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

public interface IMethodSymbol : ISymbol {
    MethodKind methodKind { get; }

    int arity { get; }

    bool isTemplateMethod { get; }

    bool returnsVoid { get; }

    bool returnsByRef { get; }

    bool returnsByRefConst { get; }

    RefKind refKind { get; }

    ITypeSymbol returnType { get; }

    bool returnTypeIsNullable { get; }

    ImmutableArray<IParameterSymbol> parameters { get; }

    bool isConst { get; }

    IMethodSymbol overriddenMethod { get; }

    ITypeSymbol receiverType { get; }
}
