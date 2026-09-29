using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Symbols;

internal static class PEPropertyOrEventHelpers {
    internal static ISet<PropertySymbol> GetPropertiesForExplicitlyImplementedAccessor(MethodSymbol accessor) {
        return GetSymbolsForExplicitlyImplementedAccessor<PropertySymbol>(accessor);
    }

    private static ISet<T> GetSymbolsForExplicitlyImplementedAccessor<T>(MethodSymbol accessor) where T : Symbol {
        if (accessor is null)
            return SpecializedCollections.EmptySet<T>();

        var implementedAccessors = accessor.explicitInterfaceImplementations;

        if (implementedAccessors.Length == 0)
            return SpecializedCollections.EmptySet<T>();

        var symbolsForExplicitlyImplementedAccessors = new HashSet<T>();

        foreach (var implementedAccessor in implementedAccessors) {
            if (implementedAccessor.associatedSymbol is T associatedProperty)
                symbolsForExplicitlyImplementedAccessors.Add(associatedProperty);
        }

        return symbolsForExplicitlyImplementedAccessors;
    }

    internal static Accessibility GetDeclaredAccessibilityFromAccessors(MethodSymbol accessor1, MethodSymbol accessor2) {
        if (accessor1 is null)
            return (accessor2 is null) ? Accessibility.NotApplicable : accessor2.declaredAccessibility;
        else if (accessor2 is null)
            return accessor1.declaredAccessibility;

        return GetDeclaredAccessibilityFromAccessors(accessor1.declaredAccessibility, accessor2.declaredAccessibility);
    }

    internal static Accessibility GetDeclaredAccessibilityFromAccessors(
        Accessibility accessibility1,
        Accessibility accessibility2) {
        var minAccessibility = (accessibility1 > accessibility2) ? accessibility2 : accessibility1;
        var maxAccessibility = (accessibility1 > accessibility2) ? accessibility1 : accessibility2;

        return ((minAccessibility == Accessibility.Protected) && (maxAccessibility == Accessibility.Internal))
            ? Accessibility.InternalOrProtected
            : maxAccessibility;
    }
}
