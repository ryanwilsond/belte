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
        if (destination.StrippedType() is ArrayTypeSymbol arrayType) {
            if (arrayType.isSZArray) {
                elementType = arrayType.elementTypeWithAnnotations;
                return ListExpressionTypeKind.Array;
            } else {
                elementType = new TypeWithAnnotations(
                    ArrayTypeSymbol.CreateArray(arrayType.elementTypeWithAnnotations, arrayType.rank - 1)
                );

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
        var elementType = elementTypeWithAnnotations?.type;

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

    internal static bool HasIdentityConversion(TypeSymbol source, TypeSymbol target, bool includeNullability = true) {
        var compareKind = includeNullability
            ? TypeCompareKind.AllIgnoreOptions & ~TypeCompareKind.IgnoreNullability
            : TypeCompareKind.AllIgnoreOptions;

        return source.Equals(target, compareKind);
    }

    internal Conversion ClassifyConversionFromExpression(BoundExpression sourceExpression, TypeSymbol target) {
        var result = ClassifyImplicitConversionFromExpression(sourceExpression, target);

        if (result.exists || sourceExpression is BoundUnconvertedInitializerList || sourceExpression.IsLiteralNull())
            // We tried our best. There are no built-in conversions for lists.
            return result;

        return Conversion.Classify(sourceExpression.Type(), target);
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

            // TODO Eventually we will handle user conversions, but right now if we can't immediately convert this
            // we won't be able to
            return listExpressionConversion;
        }

        if (sourceExpression.IsLiteralNull()) {
            if (target.IsNullableType())
                return Conversion.NullLiteral;
            // TODO Do we actually need this type of cast:
            // else if (target.isObjectType)
            //     return Conversion.ImplicitReference;
            else
                return Conversion.None;
        }

        var conversion = FastClassifyConversion(sourceExpression.Type(), target);

        if (conversion.exists && Conversion.CollapseConversion(conversion).isImplicit)
            return conversion;

        conversion = Conversion.Classify(sourceExpression.Type(), target);

        if (Conversion.CollapseConversion(conversion).isImplicit)
            return conversion;

        return Conversion.None;
    }
}
