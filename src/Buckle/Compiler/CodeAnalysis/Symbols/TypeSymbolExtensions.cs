using System;
using Buckle.CodeAnalysis.Display;

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

    internal static bool IsValidEnumType(this TypeSymbol type) {
        var underlyingType = type.GetEnumUnderlyingType()?.StrippedType();
        return underlyingType is not null && underlyingType.specialType.IsValidEnumUnderlyingType();
    }

    internal static NamedTypeSymbol GetEnumUnderlyingType(this TypeSymbol type) {
        return (type is NamedTypeSymbol namedType) ? namedType.enumUnderlyingType : null;
    }

    internal static int FixedBufferElementSizeInBytes(this TypeSymbol type) {
        return type.specialType.FixedBufferElementSizeInBytes();
    }

    internal static bool IsValidNullableTypeArgument(this TypeSymbol type) {
        return type is { isPrimitiveType: true } && !type.IsNullableType();
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

    internal static string ToNullOrString(this TypeSymbol? type, SymbolDisplayFormat format = null) {
        if (type is null)
            return "<null>";

        return type.ToDisplayString(format);
    }

    internal static TypeSymbol EnumUnderlyingTypeOrSelf(this TypeSymbol type) {
        return type.GetEnumUnderlyingType() ?? type;
    }

    internal static bool IsPointerOrFunctionPointer(this TypeSymbol type) {
        switch (type.typeKind) {
            case TypeKind.Pointer:
            case TypeKind.FunctionPointer:
                return true;
            default:
                return false;
        }
    }

    internal static TypedConstantKind GetAttributeParameterTypedConstantKind(this TypeSymbol type, Compilation compilation) {
        var kind = TypedConstantKind.Error;

        if (type is null)
            return TypedConstantKind.Error;

        if (type.kind == SymbolKind.ArrayType) {
            var arrayType = (ArrayTypeSymbol)type;

            if (!arrayType.isSZArray)
                return TypedConstantKind.Error;

            kind = TypedConstantKind.Array;
            type = arrayType.elementType;
        }

        var typedConstantKind = TypedConstant.GetTypedConstantKind(type, compilation);

        switch (typedConstantKind) {
            case TypedConstantKind.Array:
            case TypedConstantKind.Error:
                return TypedConstantKind.Error;
            default:
                if (kind == TypedConstantKind.Array)
                    return kind;

                return typedConstantKind;
        }
    }

    internal static bool IsUnboundTemplateType(this TypeSymbol type) {
        return type is NamedTypeSymbol { isUnboundTemplateType: true };
    }

    internal static bool HasNameQualifier(this NamedTypeSymbol type, string qualifiedName) {
        const StringComparison Comparison = StringComparison.Ordinal;

        var container = type.containingSymbol;

        if (container.kind != SymbolKind.Namespace) {
            return string.Equals(
                container.ToDisplayString(SymbolDisplayFormat.QualifiedNameFormat),
                qualifiedName,
                Comparison
            );
        }

        var @namespace = (NamespaceSymbol)container;

        if (@namespace.isGlobalNamespace)
            return qualifiedName.Length == 0;

        return HasNamespaceName(@namespace, qualifiedName, Comparison, length: qualifiedName.Length);
    }

    private static bool HasNamespaceName(
        NamespaceSymbol @namespace,
        string namespaceName,
        StringComparison comparison,
        int length) {
        if (length == 0)
            return false;

        var container = @namespace.containingNamespace;
        var separator = namespaceName.LastIndexOf('.', length - 1, length);
        var offset = 0;

        if (separator >= 0) {
            if (container.isGlobalNamespace)
                return false;

            if (!HasNamespaceName(container, namespaceName, comparison, length: separator))
                return false;

            var n = separator + 1;
            offset = n;
            length -= n;
        } else if (!container.isGlobalNamespace) {
            return false;
        }

        var name = @namespace.name;
        return (name.Length == length) && (string.Compare(name, 0, namespaceName, offset, length, comparison) == 0);
    }

    internal static bool IsValidSwitchType(this TypeSymbol type, bool isTargetTypeOfUserDefinedOp = false) {
        if (type.IsNullableType())
            type = type.GetNullableUnderlyingType();

        if (!isTargetTypeOfUserDefinedOp) {
            // type = type.EnumUnderlyingTypeOrSelf();
            // TODO enums
        }

        switch (type.specialType) {
            case SpecialType.Int8:
            case SpecialType.UInt8:
            case SpecialType.Int16:
            case SpecialType.UInt16:
            case SpecialType.Int32:
            case SpecialType.UInt32:
            case SpecialType.Int64:
            case SpecialType.UInt64:
            case SpecialType.Float32:
            case SpecialType.Float64:
            case SpecialType.Int:
            case SpecialType.Decimal:
            case SpecialType.Char:
            case SpecialType.String:
            case SpecialType.Type:
                return true;
            case SpecialType.Bool:
                return !isTargetTypeOfUserDefinedOp;
        }

        return false;
    }
}
