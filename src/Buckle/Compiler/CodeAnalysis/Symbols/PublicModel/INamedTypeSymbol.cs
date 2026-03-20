using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

public interface INamedTypeSymbol : ITypeSymbol {
    int arity { get; }

    bool isTemplateType { get; }

    ImmutableArray<IMethodSymbol> constructors { get; }
}
