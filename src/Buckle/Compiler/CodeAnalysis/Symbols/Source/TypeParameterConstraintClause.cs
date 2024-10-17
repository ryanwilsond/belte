using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TypeParameterConstraintClause {
    internal static readonly TypeParameterConstraintClause Empty = new TypeParameterConstraintClause(
        TypeParameterConstraintKinds.None, []
    );

    internal static TypeParameterConstraintClause Create(
        TypeParameterConstraintKinds constraints,
        ImmutableArray<TypeWithAnnotations> constraintTypes) {
        if (constraintTypes.IsEmpty) {
            switch (constraints) {
                case TypeParameterConstraintKinds.None:
                    return Empty;
            }
        }

        return new TypeParameterConstraintClause(constraints, constraintTypes);
    }

    private TypeParameterConstraintClause(
        TypeParameterConstraintKinds constraints,
        ImmutableArray<TypeWithAnnotations> constraintTypes) {
        this.constraints = constraints;
        this.constraintTypes = constraintTypes;
    }

    internal readonly TypeParameterConstraintKinds constraints;
    internal readonly ImmutableArray<TypeWithAnnotations> constraintTypes;

    internal static Dictionary<TemplateParameterSymbol, bool> BuildIsPrimitiveTypeMap(
        ImmutableArray<TemplateParameterSymbol> typeParameters,
        ImmutableArray<TypeParameterConstraintClause> constraintClauses) {

        var isPrimitiveTypeMap = new Dictionary<TemplateParameterSymbol, bool>(ReferenceEqualityComparer.Instance);

        foreach (var typeParameter in typeParameters) {
            IsPrimitiveType(typeParameter, constraintClauses, isPrimitiveTypeMap, ConsList<TemplateParameterSymbol>.Empty);
        }

        return isPrimitiveTypeMap;

        static bool IsPrimitiveType(
            TemplateParameterSymbol thisTypeParameter,
            ImmutableArray<TypeParameterConstraintClause> constraintClauses,
            Dictionary<TemplateParameterSymbol, bool> isPrimitiveTypeMap,
            ConsList<TemplateParameterSymbol> inProgress) {
            if (inProgress.ContainsReference(thisTypeParameter))
                return false;

            if (isPrimitiveTypeMap.TryGetValue(thisTypeParameter, out var knownIsPrimitiveType))
                return knownIsPrimitiveType;

            var constraintClause = constraintClauses[thisTypeParameter.ordinal];
            var result = false;

            if ((constraintClause.constraints & TypeParameterConstraintKinds.Primitive) != 0) {
                result = true;
            } else {
                var container = thisTypeParameter.containingSymbol;
                inProgress = inProgress.Prepend(thisTypeParameter);

                foreach (var constraintType in constraintClause.constraintTypes) {
                    var type = constraintType.type;

                    if (type is TemplateParameterSymbol typeParameter && (object)typeParameter.containingSymbol == container) {
                        if (IsPrimitiveType(typeParameter, constraintClauses, isPrimitiveTypeMap, inProgress)) {
                            result = true;
                            break;
                        }
                    } else if (type.isPrimitiveType) {
                        result = true;
                        break;
                    }
                }
            }

            isPrimitiveTypeMap.Add(thisTypeParameter, result);
            return result;
        }
    }

    internal static Dictionary<TemplateParameterSymbol, bool> BuildIsObjectTypeFromConstraintTypesMap(
        ImmutableArray<TemplateParameterSymbol> typeParameters,
        ImmutableArray<TypeParameterConstraintClause> constraintClauses) {
        var isObjectTypeFromConstraintTypesMap = new Dictionary<TemplateParameterSymbol, bool>(ReferenceEqualityComparer.Instance);

        foreach (var typeParameter in typeParameters)
            IsObjectTypeFromConstraintTypes(typeParameter, constraintClauses, isObjectTypeFromConstraintTypesMap, ConsList<TemplateParameterSymbol>.Empty);

        return isObjectTypeFromConstraintTypesMap;

        static bool IsObjectTypeFromConstraintTypes(
            TemplateParameterSymbol thisTypeParameter,
            ImmutableArray<TypeParameterConstraintClause> constraintClauses,
            Dictionary<TemplateParameterSymbol, bool> isObjectTypeFromConstraintTypesMap,
            ConsList<TemplateParameterSymbol> inProgress) {
            if (inProgress.ContainsReference(thisTypeParameter)) {
                return false;
            }

            if (isObjectTypeFromConstraintTypesMap.TryGetValue(thisTypeParameter, out var knownIsObjectTypeFromConstraintTypes))
                return knownIsObjectTypeFromConstraintTypes;

            var constraintClause = constraintClauses[thisTypeParameter.ordinal];
            var result = false;

            var container = thisTypeParameter.containingSymbol;
            inProgress = inProgress.Prepend(thisTypeParameter);

            foreach (var constraintType in constraintClause.constraintTypes) {
                var type = constraintType.type;

                if (type is TemplateParameterSymbol typeParameter) {
                    if ((object)typeParameter.containingSymbol == container) {
                        if (IsObjectTypeFromConstraintTypes(typeParameter, constraintClauses, isObjectTypeFromConstraintTypesMap, inProgress)) {
                            result = true;
                            break;
                        }
                    } else if (typeParameter.isObjectTypeFromConstraintTypes) {
                        result = true;
                        break;
                    }
                } else if (TemplateParameterSymbol.NonTypeParameterConstraintImpliesObjectType(type)) {
                    result = true;
                    break;
                }
            }

            isObjectTypeFromConstraintTypesMap.Add(thisTypeParameter, result);
            return result;
        }
    }
}
