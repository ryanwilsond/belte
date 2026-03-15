using System.Collections.Generic;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static class BaseTypeAnalysis {
    internal static bool TypeDependsOn(NamedTypeSymbol depends, NamedTypeSymbol on) {
        var hs = PooledHashSet<Symbol>.GetInstance();
        TypeDependsClosure(depends, depends.declaringCompilation, hs);

        var result = hs.Contains(on);
        hs.Free();

        return result;
    }

    private static void TypeDependsClosure(
        NamedTypeSymbol type,
        Compilation currentCompilation,
        HashSet<Symbol> partialClosure) {
        if (type is null)
            return;

        type = type.originalDefinition;

        if (partialClosure.Add(type)) {
            TypeDependsClosure(type.GetDeclaredBaseType(null), currentCompilation, partialClosure);

            if (currentCompilation is not null && type.IsFromCompilation(currentCompilation))
                TypeDependsClosure(type.containingType, currentCompilation, partialClosure);
        }
    }
}
