using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal static class AccessCheck {
    internal static bool IsSymbolAccessible(Symbol symbol, NamedTypeSymbol within, TypeSymbol throughType = null) {
        return IsSymbolAccessibleCore(symbol, within, throughType, out _, within.declaringCompilation);
    }

    internal static bool IsSymbolAccessible(
        Symbol symbol,
        NamedTypeSymbol within,
        TypeSymbol throughType,
        out bool failedThroughTypeCheck,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        return IsSymbolAccessibleCore(
            symbol,
            within,
            throughType,
            out failedThroughTypeCheck,
            within.declaringCompilation,
            basesBeingResolved
        );
    }

    private static bool IsSymbolAccessibleCore(
        Symbol symbol,
        NamedTypeSymbol within,
        TypeSymbol throughType,
        out bool failedThroughTypeCheck,
        Compilation compilation,
        ConsList<TypeSymbol> basesBeingResolved = null) {
        failedThroughTypeCheck = false;

        switch (symbol.kind) {
            case SymbolKind.NamedType:
            case SymbolKind.Local:
            case SymbolKind.Global:
            case SymbolKind.TemplateParameter:
            case SymbolKind.Parameter:
            case SymbolKind.Method when ((MethodSymbol)symbol).methodKind == MethodKind.LocalFunction:
                return true;
            case SymbolKind.Field:
            case SymbolKind.Method:
                if (!symbol.RequiresInstanceReceiver())
                    throughType = null;

                return IsMemberAccessible(
                    symbol.containingType,
                    symbol.declaredAccessibility,
                    within,
                    throughType,
                    out failedThroughTypeCheck,
                    compilation
                );
            default:
                throw ExceptionUtilities.UnexpectedValue(symbol.kind);
        }
    }

    private static bool IsMemberAccessible(
        NamedTypeSymbol containingType,
        Accessibility accessibility,
        Symbol within,
        TypeSymbol throughType,
        out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;

        if ((object)containingType == within)
            return true;

        if (!IsNamedTypeAccessible(containingType, within))
            return false;

        if (accessibility == Accessibility.Public)
            return true;

        return IsNonPublicMemberAccessible(containingType, accessibility, within, throughType, out failedThroughTypeCheck);
    }

    private static bool IsNonPublicMemberAccessible(
        NamedTypeSymbol containingType,
        Accessibility accessibility,
        Symbol within,
        TypeSymbol throughType,
        out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;

        var originalContainingType = containingType.originalDefinition;
        var withinType = within as NamedTypeSymbol;

        switch (accessibility) {
            case Accessibility.NotApplicable:
                return true;
            case Accessibility.Private:
                return (object)withinType is not null && IsPrivateSymbolAccessible(withinType, originalContainingType);
            case Accessibility.Protected:
                return IsProtectedSymbolAccessible(
                    withinType,
                    originalContainingType,
                    throughType,
                    out failedThroughTypeCheck
                );
            default:
                throw ExceptionUtilities.UnexpectedValue(accessibility);
        }
    }

    private static bool IsPrivateSymbolAccessible(Symbol within, NamedTypeSymbol originalContainingType) {
        if (within is not NamedTypeSymbol withinType)
            return false;

        return IsNestedWithinOriginalContainingType(withinType, originalContainingType);
    }

    private static bool IsProtectedSymbolAccessible(
        NamedTypeSymbol withinType,
        NamedTypeSymbol originalContainingType,
        TypeSymbol throughType,
        out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;

        if (withinType is null)
            return false;

        if (IsNestedWithinOriginalContainingType(withinType, originalContainingType))
            return true;

        var current = withinType.originalDefinition;
        var originalThroughType = throughType?.originalDefinition;

        while (current is not null) {
            if (current.InheritsFromIgnoringConstruction(originalContainingType)) {
                if (originalThroughType is null || originalThroughType.InheritsFromIgnoringConstruction(current))
                    return true;
                else
                    failedThroughTypeCheck = true;
            }

            current = current.containingType;
        }

        return false;
    }

    private static bool IsNestedWithinOriginalContainingType(
        NamedTypeSymbol withinType,
        NamedTypeSymbol originalContainingType) {
        var current = withinType.originalDefinition;

        while (current is not null) {
            if (current == (object)originalContainingType)
                return true;

            current = current.containingType;
        }

        return false;
    }

    private static bool IsNamedTypeAccessible(NamedTypeSymbol type, Symbol within) {
        return type.containingType is null
            || IsMemberAccessible(type.containingType, type.declaredAccessibility, within, null, out _);
    }
}
