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

    internal static bool StructDependsOn(NamedTypeSymbol depends, NamedTypeSymbol on) {
        var hs = PooledHashSet<Symbol>.GetInstance();
        StructDependsClosure(depends, hs, on);

        var result = hs.Contains(on);
        hs.Free();

        return result;
    }

    internal static TypeSymbol NonPointerType(this FieldSymbol field) {
        return field.type.IsPointerOrFunctionPointer() ? null : field.type;
    }

    private static void StructDependsClosure(NamedTypeSymbol type, HashSet<Symbol> partialClosure, NamedTypeSymbol on) {
        if ((object)type.originalDefinition == on) {
            partialClosure.Add(on);
            return;
        }

        if (partialClosure.Add(type)) {
            foreach (var member in type.GetMembersUnordered()) {
                var field = member as FieldSymbol;
                var fieldType = field?.NonPointerType();

                if (fieldType is null || fieldType.typeKind != TypeKind.Struct || field.isStatic)
                    continue;

                StructDependsClosure((NamedTypeSymbol)fieldType, partialClosure, on);
            }
        }
    }
}
