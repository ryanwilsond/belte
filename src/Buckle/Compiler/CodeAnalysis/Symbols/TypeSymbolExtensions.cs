
namespace Buckle.CodeAnalysis.Symbols;

internal static class TypeSymbolExtensions {
    internal static SpecialType GetSpecialTypeSafe(this TypeSymbol type) {
        return type is not null ? type.specialType : SpecialType.None;
    }

    internal static bool IsNullableTypeOrTypeParameter(this TypeSymbol type) {
        if (type is null)
            return false;

        if (type.typeKind == TypeKind.TemplateParameter) {
            var constraintTypes = ((TemplateParameterSymbol)type).constraintTypes;

            foreach (var constraintType in constraintTypes) {
                if (constraintType.type.IsNullableTypeOrTypeParameter())
                    return true;
            }

            return false;
        }

        return type.IsNullableType();
    }

    internal static bool IsNullableType(this TypeSymbol type) {
        return type?.originalDefinition.specialType == SpecialType.Nullable;
    }

    public static bool IsNullableType(this TypeSymbol type, out TypeSymbol underlyingType) {
        if (type is NamedTypeSymbol nt
            && nt.originalDefinition.specialType == SpecialType.Nullable) {
            underlyingType = nt.templateArguments[0].type.type;
            return true;
        }

        underlyingType = null;
        return false;
    }

    internal static string ToNullOrString(this TypeSymbol? type) {
        if (type is null)
            return "<null>";

        return type.ToString();
    }
}
