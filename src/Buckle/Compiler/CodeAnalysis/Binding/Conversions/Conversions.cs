using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class Conversions {
    // TODO This class could be much more complex
    private readonly Binder _binder;

    internal Conversions(Binder binder) {
        _binder = binder;
    }

    internal static ListExpressionTypeKind GetListExpressionTypeKind(
        TypeSymbol destination,
        out TypeWithAnnotations elementType) {
        if (destination is ArrayTypeSymbol arrayType) {
            if (arrayType.isSZArray) {
                elementType = arrayType.elementTypeWithAnnotations;
                return ListExpressionTypeKind.Array;
            }
        }

        elementType = null;
        return ListExpressionTypeKind.None;
    }

    private Conversion GetImplicitListExpressionConversion(
        BoundUnconvertedInitializerList listExpression,
        TypeSymbol destination) {
        var listExpressionConversion = GetListExpressionConversion(listExpression, destination);

        if (listExpressionConversion.exists)
            return listExpressionConversion;

        if (destination.IsNullableType(out var underlyingDestination)) {
            var underlyingConversion = GetListExpressionConversion(listExpression, underlyingDestination);

            if (underlyingConversion.exists)
                return new Conversion(ConversionKind.ImplicitNullable, [underlyingConversion]);
        }

        return Conversion.None;
    }

    internal Conversion GetListExpressionConversion(BoundUnconvertedInitializerList node, TypeSymbol targetType) {
        var listTypeKind = GetListExpressionTypeKind(targetType, out var elementTypeWithAnnotations);
        var elementType = elementTypeWithAnnotations.type;

        switch (listTypeKind) {
            case ListExpressionTypeKind.None:
                return Conversion.None;
        }

        var items = node.items;

        var builder = ArrayBuilder<Conversion>.GetInstance(items.Length);

        foreach (var element in items) {
            var elementConversion = ClassifyImplicitConversionFromExpression(element, elementType);

            if (!elementConversion.exists) {
                builder.Free();
                return Conversion.None;
            }

            builder.Add(elementConversion);
        }

        return Conversion.CreateListExpressionConversion(listTypeKind, elementType, builder.ToImmutableAndFree());
    }

    internal static Conversion FastClassifyConversion(TypeSymbol source, TypeSymbol target) {
        var conversionKind = Conversion.EasyOut.Classify(source, target);

        if (conversionKind != ConversionKind.ImplicitNullable && conversionKind != ConversionKind.ExplicitNullable)
            return new Conversion(conversionKind);

        return Conversion.MakeNullableConversion(
            conversionKind,
            FastClassifyConversion(source.StrippedType(), target.StrippedType())
        );
    }

    internal static bool HasIdentityConversion(TypeSymbol source, TypeSymbol target, bool includeNullability = false) {
        var compareKind = includeNullability
            ? TypeCompareKind.AllIgnoreOptions & ~TypeCompareKind.IgnoreNullability
            : TypeCompareKind.AllIgnoreOptions;

        return source.Equals(target, compareKind);
    }

    internal Conversion ClassifyConversionFromExpression(BoundExpression sourceExpression, TypeSymbol target) {
        if (sourceExpression.IsLiteralNull()) {
            if (target.IsNullableType())
                return Conversion.NullLiteral;
            else if (target.isObjectType)
                return Conversion.ImplicitReference;
            else
                return Conversion.None;
        }

        return Conversion.Classify(sourceExpression.type, target);
    }

    internal Conversion ClassifyBuiltInConversion(TypeSymbol source, TypeSymbol target) {
        return FastClassifyConversion(source, target);
    }

    internal Conversion ClassifyConversionFromType(TypeSymbol source, TypeSymbol target) {
        return Conversion.Classify(source, target);
    }

    internal Conversion ClassifyImplicitConversionFromType(TypeSymbol source, TypeSymbol target) {
        var conversion = ClassifyConversionFromType(source, target);

        if (conversion.isImplicit)
            return conversion;

        return Conversion.None;
    }

    internal Conversion ClassifyImplicitConversionFromExpression(BoundExpression sourceExpression, TypeSymbol target) {
        if (sourceExpression is BoundUnconvertedInitializerList list) {
            var listExpressionConversion = GetImplicitListExpressionConversion(list, target);

            if (listExpressionConversion.exists)
                return listExpressionConversion;
        }

        var conversion = ClassifyConversionFromExpression(sourceExpression, target);

        if (conversion.isImplicit)
            return conversion;

        return Conversion.None;
        // var sourceType = sourceExpression.Type;

        // if (sourceType is { } && HasIdentityConversionInternal(sourceType, destination))
        //     return Conversion.Identity;

        // var conversion = ClassifyImplicitBuiltInConversionFromExpression(sourceExpression, sourceType, destination);

        // if (conversion.Exists)
        //     return conversion;

        // if (sourceType is { }) {
        //     // Try using the short-circuit "fast-conversion" path.
        //     Conversion fastConversion = FastClassifyConversion(sourceType, destination);
        //     if (fastConversion.Exists) {
        //         if (fastConversion.IsImplicit) {
        //             return fastConversion;
        //         }
        //     } else {
        //         conversion = ClassifyImplicitBuiltInConversionSlow(sourceType, destination, ref useSiteInfo);
        //         if (conversion.Exists) {
        //             return conversion;
        //         }
        //     }
        // } else if (sourceExpression.GetFunctionType() is { } sourceFunctionType) {
        //     if (HasImplicitFunctionTypeConversion(sourceFunctionType, destination, ref useSiteInfo)) {
        //         return Conversion.FunctionType;
        //     }
        // }

        // conversion = GetImplicitUserDefinedConversion(sourceExpression, sourceType, destination, ref useSiteInfo);
        // if (conversion.Exists) {
        //     return conversion;
        // }

        // // The switch expression conversion is "lowest priority", so that if there is a conversion from the expression's
        // // type it will be preferred over the switch expression conversion.  Technically, we would want the language
        // // specification to say that the switch expression conversion only "exists" if there is no implicit conversion
        // // from the type, and we accomplish that by making it lowest priority.  The same is true for the conditional
        // // expression conversion.
        // conversion = GetSwitchExpressionConversion(sourceExpression, destination, ref useSiteInfo);
        // if (conversion.Exists) {
        //     return conversion;
        // }
        // return GetConditionalExpressionConversion(sourceExpression, destination, ref useSiteInfo);
    }
}
