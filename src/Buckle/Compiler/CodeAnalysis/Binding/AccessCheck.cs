using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal static class AccessCheck {
    internal static bool IsSymbolAccessible(Symbol symbol, Symbol within, TypeSymbol throughType = null) {
        return IsSymbolAccessibleCore(symbol, within, throughType, out _);
    }

    internal static bool IsSymbolAccessible(
        Symbol symbol,
        Symbol within,
        TypeSymbol throughType,
        out bool failedThroughTypeCheck) {
        return IsSymbolAccessibleCore(
            symbol,
            within,
            throughType,
            out failedThroughTypeCheck
        );
    }

    internal static BelteDiagnostic GetProtectedMemberInSealedTypeError(
        NamedTypeSymbol containingType,
        TextLocation errorLocation) {
        return containingType.typeKind == TypeKind.Struct
            ? Error.ProtectedInStruct(errorLocation, containingType)
            : Warning.ProtectedInSealed(errorLocation, containingType);
    }

    private static bool IsSymbolAccessibleCore(
        Symbol symbol,
        Symbol within,
        TypeSymbol throughType,
        out bool failedThroughTypeCheck) {
        failedThroughTypeCheck = false;

        switch (symbol.kind) {
            case SymbolKind.ArrayType:
                return IsSymbolAccessibleCore(
                    ((ArrayTypeSymbol)symbol).elementType,
                    within,
                    null,
                    out failedThroughTypeCheck
                );
            case SymbolKind.PointerType:
                return IsSymbolAccessibleCore(
                    ((PointerTypeSymbol)symbol).pointedAtType,
                    within,
                    null,
                    out failedThroughTypeCheck
                );
            case SymbolKind.NamedType:
                return IsNamedTypeAccessible((NamedTypeSymbol)symbol, within);
            case SymbolKind.Local:
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
                    out failedThroughTypeCheck
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

    private static bool IsNamedTypeAccessible(
        NamedTypeSymbol type,
        Symbol within) {
        if (!type.isDefinition) {
            foreach (var templateArgument in type.templateArguments) {
                if (templateArgument.isType &&
                    !IsSymbolAccessibleCore(templateArgument.type.type, within, null, out _)) {
                    return false;
                }
            }
        }

        var containingType = type.containingType;

        return containingType is null
            ? IsNonNestedTypeAccessible(type.containingNamespace, type.declaredAccessibility, within)
            : IsMemberAccessible(containingType, type.declaredAccessibility, within, null, out _);
    }

    private static bool IsNonNestedTypeAccessible(
        NamespaceSymbol containingNamespace,
        Accessibility declaredAccessibility,
        Symbol within) {
        switch (declaredAccessibility) {
            case Accessibility.NotApplicable:
            case Accessibility.Public:
                return true;
            case Accessibility.Private:
            case Accessibility.Protected:
                return false;
            // When internal is added, the namespace will become relevant
            default:
                throw ExceptionUtilities.UnexpectedValue(declaredAccessibility);
        }
    }

    internal static bool IsEffectivelyPublicOrInternal(Symbol symbol, out bool isInternal) {
        switch (symbol.kind) {
            case SymbolKind.NamedType:
            case SymbolKind.Field:
            case SymbolKind.Method:
                break;
            case SymbolKind.TemplateParameter:
                symbol = symbol.containingSymbol;
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(symbol.kind);
        }

        isInternal = false;

        do {
            switch (symbol.declaredAccessibility) {
                case Accessibility.Public:
                case Accessibility.Protected:
                    break;
                case Accessibility.Private:
                    return false;
                default:
                    throw ExceptionUtilities.UnexpectedValue(symbol.declaredAccessibility);
            }

            symbol = symbol.containingType;
        } while (symbol is not null);

        return true;
    }
}
