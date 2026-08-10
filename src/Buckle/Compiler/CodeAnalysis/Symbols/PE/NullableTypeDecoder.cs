using System.Collections.Immutable;
using System.Reflection.Metadata;

namespace Buckle.CodeAnalysis.Symbols;

internal static class NullableTypeDecoder {
    internal static TypeWithAnnotations TransformType(
        TypeWithAnnotations metadataType,
        EntityHandle targetSymbolToken,
        PEModuleSymbol containingModule,
        Symbol accessSymbol,
        Symbol nullableContext) {
        // C# attribute treats 0 as non-nullable, Belte encodes 0 as nullable
        var isBelteMode = false;

        // [NullableAttribute] is C#
        if (!containingModule.module.HasNullableAttribute(
            targetSymbolToken,
            out var defaultTransformFlag,
            out var nullableTransformFlags)) {
            // [NullabilityAttribute] is Belte
            if (!containingModule.module.HasNullabilityAttribute(targetSymbolToken, out var nullabilityTransformFlags)) {
                var value = nullableContext.GetNullableContextValue();

                // TODO Do we actually care about nullable annotations at all
                // if (value is null)
                //     return metadataType;

                defaultTransformFlag = value.GetValueOrDefault();
            } else {
                defaultTransformFlag = 0;
                nullableTransformFlags = nullabilityTransformFlags;
                isBelteMode = true;
            }
        }

        if (!containingModule.ShouldDecodeNullableAttributes(accessSymbol))
            return metadataType;

        return TransformType(metadataType, defaultTransformFlag, nullableTransformFlags, isBelteMode);
    }

    internal static TypeWithAnnotations TransformType(
        TypeWithAnnotations metadataType,
        byte defaultTransformFlag,
        ImmutableArray<byte> nullableTransformFlags,
        bool isBelteMode) {
        // TODO Do we actually care about C# nullable annotations at all
        // if (nullableTransformFlags.IsDefault && defaultTransformFlag == 0)
        //     return metadataType;

        if (isBelteMode && nullableTransformFlags.IsDefault && defaultTransformFlag == 1)
            return metadataType;

        var position = 0;

        if (metadataType.ApplyNullableTransforms(
                defaultTransformFlag,
                nullableTransformFlags,
                ref position,
                out var result,
                isBelteMode) &&
            (nullableTransformFlags.IsDefault || position == nullableTransformFlags.Length)) {
            return result;
        }

        return metadataType;
    }
}
