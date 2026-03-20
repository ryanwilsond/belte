using System;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class Conversions {
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

    private Conversion GetImplicitUserDefinedConversion(
        BoundExpression sourceExpression,
        TypeSymbol source,
        TypeSymbol destination) {
        var conversionResult = AnalyzeImplicitUserDefinedConversions(
            sourceExpression,
            source,
            destination
        );

        return new Conversion(conversionResult, isImplicit: true);
    }

    private Conversion GetImplicitUserDefinedConversion(TypeSymbol source, TypeSymbol destination) {
        return GetImplicitUserDefinedConversion(sourceExpression: null, source, destination);
    }

    private Conversion GetExplicitUserDefinedConversion(
        BoundExpression sourceExpression,
        TypeSymbol source,
        TypeSymbol destination) {
        var conversionResult = AnalyzeExplicitUserDefinedConversions(sourceExpression, source, destination);
        return new Conversion(conversionResult, isImplicit: false);
    }

    private Conversion GetExplicitUserDefinedConversion(TypeSymbol source, TypeSymbol destination) {
        return GetExplicitUserDefinedConversion(sourceExpression: null, source, destination);
    }

    internal Conversion ClassifyBuiltInConversion(TypeSymbol source, TypeSymbol target) {
        return FastClassifyConversion(source, target);
    }

    internal Conversion ClassifyConversionFromType(TypeSymbol source, TypeSymbol target) {
        var conversion = GetImplicitUserDefinedConversion(source, target);

        if (conversion.exists)
            return conversion;

        conversion = GetExplicitUserDefinedConversion(source, target);

        if (conversion.exists)
            return conversion;

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
            else
                return Conversion.None;
        }

        var conversion = FastClassifyConversion(sourceExpression.Type(), target);

        if (conversion.exists && Conversion.CollapseConversion(conversion).isImplicit)
            return conversion;

        conversion = GetImplicitUserDefinedConversion(sourceExpression, sourceExpression.Type(), target);

        if (conversion.exists)
            return conversion;

        conversion = Conversion.Classify(sourceExpression.Type(), target);

        if (Conversion.CollapseConversion(conversion).isImplicit)
            return conversion;

        return Conversion.None;
    }

    private Conversion ClassifyStandardImplicitConversion(
        BoundExpression expression,
        TypeSymbol source,
        TypeSymbol target) {
        // TODO Do we need to use expression here
        var conversion = Conversion.Classify(source, target);

        if (conversion.isImplicit)
            return conversion;

        return Conversion.None;
    }

    private Conversion ClassifyStandardConversion(
        BoundExpression expression,
        TypeSymbol source,
        TypeSymbol target) {
        // TODO Do we need to use expression here
        return Conversion.Classify(source, target);
    }

    private UserDefinedConversionResult AnalyzeExplicitUserDefinedConversions(
        BoundExpression sourceExpression,
        TypeSymbol source,
        TypeSymbol target) {
        var d = ArrayBuilder<(NamedTypeSymbol ParticipatingType, TemplateParameterSymbol ConstrainedToTypeOpt)>
            .GetInstance();

        ComputeUserDefinedExplicitConversionTypeSet(source, target, d);

        var ubuild = ArrayBuilder<UserDefinedConversionAnalysis>.GetInstance();
        ComputeApplicableUserDefinedExplicitConversionSet(sourceExpression, source, target, d, ubuild);
        d.Free();
        var u = ubuild.ToImmutableAndFree();

        if (u.Length == 0)
            return UserDefinedConversionResult.NoApplicableOperators(u);

        var sx = MostSpecificSourceTypeForExplicitUserDefinedConversion(u, sourceExpression, source);

        if (sx is null)
            return UserDefinedConversionResult.NoBestSourceType(u);

        var tx = MostSpecificTargetTypeForExplicitUserDefinedConversion(u, target);

        if (tx is null)
            return UserDefinedConversionResult.NoBestTargetType(u);

        var best = MostSpecificConversionOperator(sx, tx, u);

        if (best is null)
            return UserDefinedConversionResult.Ambiguous(u);

        return UserDefinedConversionResult.Valid(u, best.Value);
    }

    private static void ComputeUserDefinedExplicitConversionTypeSet(
        TypeSymbol source,
        TypeSymbol target,
        ArrayBuilder<(NamedTypeSymbol ParticipatingType, TemplateParameterSymbol ConstrainedToTypeOpt)> d) {
        AddTypesParticipatingInUserDefinedConversion(d, source, includeBaseTypes: true);
        AddTypesParticipatingInUserDefinedConversion(d, target, includeBaseTypes: true);
    }

    private void ComputeApplicableUserDefinedExplicitConversionSet(
        BoundExpression sourceExpression,
        TypeSymbol source,
        TypeSymbol target,
        ArrayBuilder<(NamedTypeSymbol ParticipatingType, TemplateParameterSymbol ConstrainedToTypeOpt)> d,
        ArrayBuilder<UserDefinedConversionAnalysis> u) {
        foreach ((var declaringType, var constrainedToTypeOpt) in d) {
            AddCandidatesFromType(null, declaringType, sourceExpression, source, target, u);
        }

        void AddCandidatesFromType(
            TemplateParameterSymbol constrainedToTypeOpt,
            NamedTypeSymbol declaringType,
            BoundExpression sourceExpression,
            TypeSymbol source,
            TypeSymbol target,
            ArrayBuilder<UserDefinedConversionAnalysis> u) {
            AddUserDefinedConversionsToExplicitCandidateSet(
                sourceExpression,
                source,
                target,
                u,
                constrainedToTypeOpt,
                declaringType,
                isExplicit: true
            );

            AddUserDefinedConversionsToExplicitCandidateSet(
                sourceExpression,
                source,
                target,
                u,
                constrainedToTypeOpt,
                declaringType,
                isExplicit: false
            );
        }
    }

    private void AddUserDefinedConversionsToExplicitCandidateSet(
        BoundExpression sourceExpression,
        TypeSymbol source,
        TypeSymbol target,
        ArrayBuilder<UserDefinedConversionAnalysis> u,
        TemplateParameterSymbol constrainedToTypeOpt,
        NamedTypeSymbol declaringType,
        bool isExplicit) {
        var operators = declaringType.GetOperators(
            isExplicit ? WellKnownMemberNames.ExplicitConversionName : WellKnownMemberNames.ImplicitConversionName);

        var candidates = ArrayBuilder<MethodSymbol>.GetInstance(operators.Length);
        candidates.AddRange(operators);

        foreach (var op in candidates) {
            if (op.returnsVoid || op.parameterCount != 1 || op.returnType.typeKind == TypeKind.Error)
                continue;

            var convertsFrom = op.GetParameterType(0);
            var convertsTo = op.returnType;
            var fromConversion = EncompassingExplicitConversion(sourceExpression, source, convertsFrom);
            var toConversion = EncompassingExplicitConversion(convertsTo, target);

            if (!fromConversion.exists &&
                source is not null &&
                source.IsNullableType() &&
                EncompassingExplicitConversion(source.GetNullableUnderlyingType(), convertsFrom).exists) {
                fromConversion = ClassifyBuiltInConversion(source, convertsFrom);
            }

            if (!toConversion.exists &&
                target is not null &&
                target.IsNullableType() &&
                EncompassingExplicitConversion(convertsTo, target.GetNullableUnderlyingType()).exists) {
                toConversion = ClassifyBuiltInConversion(convertsTo, target);
            }

            if (fromConversion.exists && toConversion.exists) {
                if (source is not null && source.IsNullableType() &&
                    convertsFrom.IsValidNullableTypeArgument() && target.IsNullableType()) {
                    var nullableFrom = MakeNullableType(convertsFrom);
                    var nullableTo = convertsTo.IsValidNullableTypeArgument() ? MakeNullableType(convertsTo) : convertsTo;
                    var liftedFromConversion = EncompassingExplicitConversion(sourceExpression, source, nullableFrom);
                    var liftedToConversion = EncompassingExplicitConversion(nullableTo, target);

                    u.Add(UserDefinedConversionAnalysis.Lifted(
                        constrainedToTypeOpt,
                        op,
                        liftedFromConversion,
                        liftedToConversion,
                        nullableFrom,
                        nullableTo
                    ));
                } else {
                    if (target.IsNullableType() && convertsTo.IsValidNullableTypeArgument()) {
                        convertsTo = MakeNullableType(convertsTo);
                        toConversion = EncompassingExplicitConversion(convertsTo, target);
                    }

                    if (source is not null && source.IsNullableType() && convertsFrom.IsValidNullableTypeArgument()) {
                        convertsFrom = MakeNullableType(convertsFrom);
                        fromConversion = EncompassingExplicitConversion(convertsFrom, source);
                    }

                    u.Add(UserDefinedConversionAnalysis.Normal(constrainedToTypeOpt, op, fromConversion, toConversion, convertsFrom, convertsTo));
                }
            }
        }

        candidates.Free();
    }

    private TypeSymbol MostSpecificSourceTypeForExplicitUserDefinedConversion(
        ImmutableArray<UserDefinedConversionAnalysis> u,
        BoundExpression sourceExpression,
        TypeSymbol source) {
        if (source is not null) {
            if (u.Any(static (conv, source)
                => TypeSymbol.Equals(conv.fromType, source, TypeCompareKind.ConsiderEverything), source)) {
                return source;
            }

            Func<UserDefinedConversionAnalysis, bool> isValid =
                conv => IsEncompassedBy(sourceExpression, source, conv.fromType);

            if (u.Any(isValid)) {
                var result = MostEncompassedType(u, isValid, conv => conv.fromType);
                return result;
            }
        }

        return MostEncompassingType(u, conv => conv.fromType);
    }

    private TypeSymbol MostSpecificTargetTypeForExplicitUserDefinedConversion(
        ImmutableArray<UserDefinedConversionAnalysis> u,
        TypeSymbol target) {
        if (u.Any(static (conv, target)
            => TypeSymbol.Equals(conv.toType, target, TypeCompareKind.ConsiderEverything), target)) {
            return target;
        }

        Func<UserDefinedConversionAnalysis, bool> isValid = conv => IsEncompassedBy(conv.toType, target);

        if (u.Any(isValid)) {
            var result = MostEncompassingType(u, isValid, conv => conv.toType);
            return result;
        }

        return MostEncompassedType(u, conv => conv.toType);
    }

    private Conversion EncompassingExplicitConversion(BoundExpression expr, TypeSymbol a, TypeSymbol b) {
        return ClassifyStandardConversion(expr, a, b);
    }

    private Conversion EncompassingExplicitConversion(TypeSymbol a, TypeSymbol b) {
        return EncompassingExplicitConversion(expr: null, a, b);
    }

    private UserDefinedConversionResult AnalyzeImplicitUserDefinedConversions(
        BoundExpression sourceExpression,
        TypeSymbol source,
        TypeSymbol target) {
        var d = ArrayBuilder<(NamedTypeSymbol ParticipatingType, TemplateParameterSymbol ConstrainedToTypeOpt)>
            .GetInstance();
        ComputeUserDefinedImplicitConversionTypeSet(source, target, d);

        var ubuild = ArrayBuilder<UserDefinedConversionAnalysis>.GetInstance();
        ComputeApplicableUserDefinedImplicitConversionSet(sourceExpression, source, target, d, ubuild);
        d.Free();
        var u = ubuild.ToImmutableAndFree();

        if (u.Length == 0)
            return UserDefinedConversionResult.NoApplicableOperators(u);

        var sx = MostSpecificSourceTypeForImplicitUserDefinedConversion(u, source);

        if (sx is null)
            return UserDefinedConversionResult.NoBestSourceType(u);

        var tx = MostSpecificTargetTypeForImplicitUserDefinedConversion(u, target);

        if (tx is null)
            return UserDefinedConversionResult.NoBestTargetType(u);

        var best = MostSpecificConversionOperator(sx, tx, u);

        return best is null
            ? UserDefinedConversionResult.Ambiguous(u)
            : UserDefinedConversionResult.Valid(u, best.Value);
    }

    private static void ComputeUserDefinedImplicitConversionTypeSet(
        TypeSymbol s,
        TypeSymbol t,
        ArrayBuilder<(NamedTypeSymbol ParticipatingType, TemplateParameterSymbol ConstrainedToTypeOpt)> d) {
        AddTypesParticipatingInUserDefinedConversion(d, s, includeBaseTypes: true);
        AddTypesParticipatingInUserDefinedConversion(d, t, includeBaseTypes: false);
    }

    private void ComputeApplicableUserDefinedImplicitConversionSet(
        BoundExpression sourceExpression,
        TypeSymbol source,
        TypeSymbol target,
        ArrayBuilder<(NamedTypeSymbol ParticipatingType, TemplateParameterSymbol ConstrainedToTypeOpt)> d,
        ArrayBuilder<UserDefinedConversionAnalysis> u,
        bool allowAnyTarget = false) {
        if (source is not null && false || target is not null && false)
            return;

        foreach ((var declaringType, var constrainedToTypeOpt) in d) {
            AddCandidatesFromType(
                constrainedToTypeOpt: null,
                declaringType,
                sourceExpression,
                source,
                target,
                u,
                allowAnyTarget
            );
        }

        void AddCandidatesFromType(
            TemplateParameterSymbol constrainedToTypeOpt,
            NamedTypeSymbol declaringType,
            BoundExpression sourceExpression,
            TypeSymbol source,
            TypeSymbol target,
            ArrayBuilder<UserDefinedConversionAnalysis> u,
            bool allowAnyTarget) {
            foreach (var op in declaringType.GetOperators(WellKnownMemberNames.ImplicitConversionName)) {
                if (op.returnsVoid || op.parameterCount != 1)
                    continue;

                var convertsFrom = op.GetParameterType(0);
                var convertsTo = op.returnType;
                var fromConversion = EncompassingImplicitConversion(sourceExpression, source, convertsFrom);
                var toConversion = allowAnyTarget
                    ? Conversion.Identity
                    : EncompassingImplicitConversion(convertsTo, target);

                if (fromConversion.exists && toConversion.exists) {
                    if (target is not null && target.IsNullableType() && convertsTo.IsValidNullableTypeArgument()) {
                        convertsTo = MakeNullableType(convertsTo);
                        toConversion = allowAnyTarget
                            ? Conversion.Identity
                            : EncompassingImplicitConversion(convertsTo, target);
                    }

                    u.Add(UserDefinedConversionAnalysis.Normal(
                        constrainedToTypeOpt,
                        op,
                        fromConversion,
                        toConversion,
                        convertsFrom,
                        convertsTo
                    ));
                } else if (source is not null && source.IsNullableType() && convertsFrom.IsValidNullableTypeArgument() &&
                      (allowAnyTarget || target.IsNullableType())) {
                    var nullableFrom = MakeNullableType(convertsFrom);
                    var nullableTo = convertsTo.IsValidNullableTypeArgument()
                        ? MakeNullableType(convertsTo)
                        : convertsTo;
                    var liftedFromConversion = EncompassingImplicitConversion(sourceExpression, source, nullableFrom);
                    var liftedToConversion = !allowAnyTarget
                        ? EncompassingImplicitConversion(nullableTo, target)
                        : Conversion.Identity;

                    if (liftedFromConversion.exists && liftedToConversion.exists) {
                        u.Add(UserDefinedConversionAnalysis.Lifted(
                            constrainedToTypeOpt,
                            op,
                            liftedFromConversion,
                            liftedToConversion,
                            nullableFrom,
                            nullableTo
                        ));
                    }
                }
            }
        }
    }

    private TypeSymbol MakeNullableType(TypeSymbol type) {
        return new TypeWithAnnotations(type).SetIsAnnotated().type;
    }

    private TypeSymbol MostSpecificSourceTypeForImplicitUserDefinedConversion(
        ImmutableArray<UserDefinedConversionAnalysis> u,
        TypeSymbol source) {
        if (source is not null) {
            if (u.Any(static (conv, source)
                => TypeSymbol.Equals(conv.fromType, source, TypeCompareKind.ConsiderEverything), source)) {
                return source;
            }
        }

        return MostEncompassedType(u, conv => conv.fromType);
    }

    private TypeSymbol MostSpecificTargetTypeForImplicitUserDefinedConversion(
        ImmutableArray<UserDefinedConversionAnalysis> u,
        TypeSymbol target) {
        if (u.Any(static (conv, target)
            => TypeSymbol.Equals(conv.toType, target, TypeCompareKind.ConsiderEverything), target)) {
            return target;
        }

        return MostEncompassingType(u, conv => conv.toType);
    }

    private static int LiftingCount(UserDefinedConversionAnalysis conv) {
        var count = 0;

        if (!TypeSymbol.Equals(conv.fromType, conv.@operator.GetParameterType(0), TypeCompareKind.ConsiderEverything))
            count += 1;

        if (!TypeSymbol.Equals(conv.toType, conv.@operator.returnType, TypeCompareKind.ConsiderEverything))
            count += 1;

        return count;
    }

    private static int? MostSpecificConversionOperator(
        TypeSymbol sx,
        TypeSymbol tx,
        ImmutableArray<UserDefinedConversionAnalysis> u) {
        return MostSpecificConversionOperator(
            conv => TypeSymbol.Equals(conv.fromType, sx, TypeCompareKind.ConsiderEverything) &&
            TypeSymbol.Equals(conv.toType, tx, TypeCompareKind.ConsiderEverything), u);
    }

    private static int? MostSpecificConversionOperator(
        Func<UserDefinedConversionAnalysis, bool> constraint,
        ImmutableArray<UserDefinedConversionAnalysis> u) {
        var bestUnlifted = UniqueIndex(u,
            conv =>
            constraint(conv) &&
            LiftingCount(conv) == 0);

        if (bestUnlifted.kind == BestIndexKind.Best)
            return bestUnlifted.best;
        else if (bestUnlifted.kind == BestIndexKind.Ambiguous)
            return null;

        var bestHalfLifted = UniqueIndex(u,
            conv =>
            constraint(conv) &&
            LiftingCount(conv) == 1);

        if (bestHalfLifted.kind == BestIndexKind.Best)
            return bestHalfLifted.best;
        else if (bestHalfLifted.kind == BestIndexKind.Ambiguous)
            return null;

        var bestFullyLifted = UniqueIndex(u,
            conv =>
            constraint(conv) &&
            LiftingCount(conv) == 2);

        if (bestFullyLifted.kind == BestIndexKind.Best)
            return bestFullyLifted.best;
        else if (bestFullyLifted.kind == BestIndexKind.Ambiguous)
            return null;

        return null;
    }

    private static BestIndex UniqueIndex<T>(ImmutableArray<T> items, Func<T, bool> predicate) {
        if (items.IsEmpty)
            return BestIndex.None();

        int? result = null;

        for (var i = 0; i < items.Length; i++) {
            if (predicate(items[i])) {
                if (result is null)
                    result = i;
                else
                    return BestIndex.IsAmbiguous(result.Value, i);
            }
        }

        return result is null ? BestIndex.None() : BestIndex.HasBest(result.Value);
    }

    private bool IsEncompassedBy(BoundExpression aExpr, TypeSymbol a, TypeSymbol b) {
        return EncompassingImplicitConversion(aExpr, a, b).exists;
    }

    private bool IsEncompassedBy(TypeSymbol a, TypeSymbol b) {
        return IsEncompassedBy(aExpr: null, a, b);
    }

    private Conversion EncompassingImplicitConversion(BoundExpression aExpr, TypeSymbol a, TypeSymbol b) {
        var result = ClassifyStandardImplicitConversion(aExpr, a, b);
        return IsEncompassingImplicitConversionKind(result.kind) ? result : Conversion.None;
    }

    private Conversion EncompassingImplicitConversion(TypeSymbol a, TypeSymbol b) {
        return EncompassingImplicitConversion(aExpr: null, a, b);
    }

    private static bool IsEncompassingImplicitConversionKind(ConversionKind kind) {
        switch (kind) {
            case ConversionKind.None:
            case ConversionKind.ImplicitUserDefined:
            case ConversionKind.ExplicitUserDefined:
            case ConversionKind.ExplicitNullable:
            case ConversionKind.ExplicitReference:
            case ConversionKind.AnyUnboxing:
                return false;
            case ConversionKind.Identity:
            case ConversionKind.ImplicitNullable:
            case ConversionKind.ImplicitReference:
            case ConversionKind.AnyBoxing:
            case ConversionKind.ImplicitConstant:
            case ConversionKind.NullLiteral:
            case ConversionKind.DefaultLiteral:
                return true;
            default:
                throw ExceptionUtilities.UnexpectedValue(kind);
        }
    }

    private TypeSymbol MostEncompassedType<T>(
        ImmutableArray<T> items,
        Func<T, TypeSymbol> extract) {
        return MostEncompassedType(items, x => true, extract);
    }

    private TypeSymbol MostEncompassedType<T>(
        ImmutableArray<T> items,
        Func<T, bool> valid,
        Func<T, TypeSymbol> extract) {
        var best = UniqueBestValidIndex(items, valid,
            (left, right) => {
                var leftType = extract(left);
                var rightType = extract(right);

                if (TypeSymbol.Equals(leftType, rightType, TypeCompareKind.ConsiderEverything))
                    return BetterResult.Equal;

                var leftWins = IsEncompassedBy(leftType, rightType);
                var rightWins = IsEncompassedBy(rightType, leftType);

                if (leftWins == rightWins)
                    return BetterResult.Neither;

                return leftWins ? BetterResult.Left : BetterResult.Right;
            });

        return best is null ? null : extract(items[best.Value]);
    }

    private TypeSymbol MostEncompassingType<T>(
        ImmutableArray<T> items,
        Func<T, TypeSymbol> extract) {
        return MostEncompassingType(items, x => true, extract);
    }

    private TypeSymbol MostEncompassingType<T>(
        ImmutableArray<T> items,
        Func<T, bool> valid,
        Func<T, TypeSymbol> extract) {
        var best = UniqueBestValidIndex(items, valid,
            (left, right) => {
                var leftType = extract(left);
                var rightType = extract(right);

                if (TypeSymbol.Equals(leftType, rightType, TypeCompareKind.ConsiderEverything))
                    return BetterResult.Equal;

                var leftWins = IsEncompassedBy(rightType, leftType);
                var rightWins = IsEncompassedBy(leftType, rightType);

                if (leftWins == rightWins)
                    return BetterResult.Neither;

                return leftWins ? BetterResult.Left : BetterResult.Right;
            });

        return best is null ? null : extract(items[best.Value]);
    }

    private static int? UniqueBestValidIndex<T>(
        ImmutableArray<T> items,
        Func<T, bool> valid,
        Func<T, T, BetterResult> better) {
        if (items.IsEmpty)
            return null;

        int? candidateIndex = null;
        var candidateItem = default(T);

        for (var currentIndex = 0; currentIndex < items.Length; ++currentIndex) {
            var currentItem = items[currentIndex];

            if (!valid(currentItem))
                continue;

            if (candidateIndex is null) {
                candidateIndex = currentIndex;
                candidateItem = currentItem;
                continue;
            }

            var result = better(candidateItem, currentItem);

            if (result == BetterResult.Equal) {
                continue;
            } else if (result == BetterResult.Neither) {
                candidateIndex = null;
                candidateItem = default;
            } else if (result == BetterResult.Right) {
                candidateIndex = currentIndex;
                candidateItem = currentItem;
            }
        }

        if (candidateIndex is null)
            return null;

        for (var currentIndex = 0; currentIndex < candidateIndex.Value; currentIndex++) {
            var currentItem = items[currentIndex];

            if (!valid(currentItem))
                continue;

            var result = better(candidateItem, currentItem);

            if (result != BetterResult.Left && result != BetterResult.Equal)
                return null;
        }

        return candidateIndex;
    }

    private UserDefinedConversionResult AnalyzeImplicitUserDefinedConversionForV6SwitchGoverningType(
        TypeSymbol source) {
        var d = ArrayBuilder<(NamedTypeSymbol ParticipatingType, TemplateParameterSymbol ConstrainedToTypeOpt)>.GetInstance();
        ComputeUserDefinedImplicitConversionTypeSet(source, t: null, d: d);

        var ubuild = ArrayBuilder<UserDefinedConversionAnalysis>.GetInstance();
        ComputeApplicableUserDefinedImplicitConversionSet(
            sourceExpression: null,
            source,
            target: null,
            d: d,
            u: ubuild,
            allowAnyTarget: true
        );

        d.Free();
        var u = ubuild.ToImmutableAndFree();

        // TODO See V6 doc
        var best = MostSpecificConversionOperator(conv => true, u);

        if (best is null)
            return UserDefinedConversionResult.NoApplicableOperators(u);

        return UserDefinedConversionResult.Valid(u, best.Value);
    }

    internal static void AddTypesParticipatingInUserDefinedConversion(
        ArrayBuilder<(NamedTypeSymbol ParticipatingType, TemplateParameterSymbol ConstrainedToTypeOpt)> result,
        TypeSymbol type,
        bool includeBaseTypes) {
        if (type is null)
            return;

        type = type.StrippedType();

        var excludeExisting = result.Count > 0;

        if (type is TemplateParameterSymbol typeParameter) {
            var effectiveBaseClass = typeParameter.effectiveBaseClass;
            AddFromClassOrStruct(result, excludeExisting, effectiveBaseClass, includeBaseTypes);
        } else {
            AddFromClassOrStruct(result, excludeExisting, type, includeBaseTypes);
        }

        static void AddFromClassOrStruct(
            ArrayBuilder<(NamedTypeSymbol ParticipatingType, TemplateParameterSymbol ConstrainedToTypeOpt)> result,
            bool excludeExisting,
            TypeSymbol type,
            bool includeBaseTypes) {
            if (type.IsClassType() || type.IsStructType()) {
                var namedType = (NamedTypeSymbol)type;
                if (!excludeExisting || !HasIdentityConversionToAny(namedType, result)) {
                    result.Add((namedType, null));
                }
            }

            if (!includeBaseTypes)
                return;

            var t = type.baseType;

            while (t is not null) {
                if (!excludeExisting || !HasIdentityConversionToAny(t, result))
                    result.Add((t, null));

                t = t.baseType;
            }
        }
    }

    private static bool HasIdentityConversionToAny(
        NamedTypeSymbol type,
        ArrayBuilder<(NamedTypeSymbol ParticipatingType, TemplateParameterSymbol ConstrainedToTypeOpt)> targetTypes) {
        foreach (var (ParticipatingType, ConstrainedToTypeOpt) in targetTypes) {
            if (HasIdentityConversion(type, ParticipatingType, includeNullability: false))
                return true;
        }

        return false;
    }
}
