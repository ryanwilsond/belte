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
        if (!containingModule.module.HasNullableAttribute(
            targetSymbolToken,
            out var defaultTransformFlag,
            out var nullableTransformFlags)) {
            var value = nullableContext.GetNullableContextValue();

            // TODO Do we actually care about nullable annotations at all
            // if (value is null)
            //     return metadataType;

            defaultTransformFlag = value.GetValueOrDefault();
        }

        if (!containingModule.ShouldDecodeNullableAttributes(accessSymbol))
            return metadataType;

        return TransformType(metadataType, defaultTransformFlag, nullableTransformFlags);
    }

    internal static TypeWithAnnotations TransformType(
        TypeWithAnnotations metadataType,
        byte defaultTransformFlag,
        ImmutableArray<byte> nullableTransformFlags) {
        // TODO Do we actually care about nullable annotations at all
        // if (nullableTransformFlags.IsDefault && defaultTransformFlag == 0)
        //     return metadataType;

        var position = 0;

        if (metadataType.ApplyNullableTransforms(
            defaultTransformFlag,
            nullableTransformFlags,
            ref position,
            out var result) &&
            (nullableTransformFlags.IsDefault || position == nullableTransformFlags.Length)) {
            return result;
        }

        return metadataType;
    }
}
