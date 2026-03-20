using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal static class BestTypeInferrer {
    internal static TypeSymbol InferBestTypeForConditionalOperator(
        BoundExpression expression1,
        BoundExpression expression2,
        Conversions conversions,
        out bool hadMultipleCandidates) {
        var candidateTypes = ArrayBuilder<TypeSymbol>.GetInstance();

        try {
            // var conversionsWithoutNullability = conversions.WithNullability(false);
            // TODO figure this out
            var conversionsWithoutNullability = conversions;

            if (expression1.Type() is { } type1) {
                if (type1.IsErrorType()) {
                    hadMultipleCandidates = false;
                    return type1;
                }

                if (conversionsWithoutNullability.ClassifyImplicitConversionFromExpression(expression1, type1).exists)
                    candidateTypes.Add(type1);
            }

            if (expression2.Type() is { } type2) {
                if (type2.IsErrorType()) {
                    hadMultipleCandidates = false;
                    return type2;
                }

                if (conversionsWithoutNullability.ClassifyImplicitConversionFromExpression(expression1, type2).exists)
                    candidateTypes.Add(type2);
            }

            hadMultipleCandidates = candidateTypes.Count > 1;

            return GetBestType(candidateTypes, conversions);
        } finally {
            candidateTypes.Free();
        }
    }

    internal static TypeSymbol GetBestType(ArrayBuilder<TypeSymbol> types, Conversions conversions) {
        switch (types.Count) {
            case 0:
                return null;
            case 1:
                return types[0];
        }

        TypeSymbol best = null;
        var bestIndex = -1;
        for (var i = 0; i < types.Count; i++) {
            var type = types[i];

            if (type is null)
                continue;

            if (best is null) {
                best = type;
                bestIndex = i;
            } else {
                var better = Better(best, type, conversions);

                if (better is null) {
                    best = null;
                } else {
                    best = better;
                    bestIndex = i;
                }
            }
        }

        if (best is null)
            return null;

        for (var i = 0; i < bestIndex; i++) {
            var type = types[i];

            if (type is null)
                continue;

            var better = Better(best, type, conversions);

            if (!best.Equals(better, TypeCompareKind.IgnoreNullability))
                return null;
        }

        return best;
    }

    private static TypeSymbol? Better(TypeSymbol type1, TypeSymbol type2, Conversions conversions) {
        if (type1.IsErrorType())
            return type2;

        if (type2 is null || type2.IsErrorType())
            return type1;

        // TODO figure this out
        // var conversionsWithoutNullability = conversions.WithNullability(false);
        var conversionsWithoutNullability = conversions;
        var t1tot2 = conversionsWithoutNullability.ClassifyImplicitConversionFromType(type1, type2).exists;
        var t2tot1 = conversionsWithoutNullability.ClassifyImplicitConversionFromType(type2, type1).exists;

        if (t1tot2 && t2tot1) {
            if (type1.Equals(type2, TypeCompareKind.IgnoreNullability)) {
                // TODO confirm this doesn't do anything else we want
                // return type1.MergeEquivalentTypes(type2, VarianceKind.Out);
                return type1.IsNullableType() ? type1 : type2;
            }

            return null;
        }

        if (t1tot2)
            return type2;

        if (t2tot1)
            return type1;

        return null;
    }
}
