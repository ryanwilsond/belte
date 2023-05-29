using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A class symbol.
/// </summary>
internal sealed class ClassSymbol : NamedTypeSymbol {
    /// <summary>
    /// Creates a <see cref="ClassSymbol" /> with template parameters and child members.
    /// </summary>
    internal ClassSymbol(
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<Symbol> symbols, ClassDeclarationSyntax declaration)
        : base(templateParameters, symbols, declaration) {
    }

    internal ImmutableArray<MethodSymbol> constructors => GetConstructors();

    private ImmutableArray<MethodSymbol> GetConstructors() {
        var candidates = GetMembers(WellKnownMemberNames.InstanceConstructorName);

        if (candidates.IsEmpty)
            return ImmutableArray<MethodSymbol>.Empty;

        ArrayBuilder<MethodSymbol> constructors = ArrayBuilder<MethodSymbol>.GetInstance();

        foreach (var candidate in candidates) {
            if (candidate is MethodSymbol method)
                constructors.Add(method);
        }

        return constructors.ToImmutableAndFree();
    }
}
