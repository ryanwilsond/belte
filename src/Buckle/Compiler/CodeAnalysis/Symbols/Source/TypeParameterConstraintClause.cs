using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TypeParameterConstraintClause {
    internal static readonly TypeParameterConstraintClause Empty = new TypeParameterConstraintClause(
        TypeParameterConstraintKinds.None, [], null
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

        return new TypeParameterConstraintClause(constraints, constraintTypes, null);
    }

    internal static TypeParameterConstraintClause Create(ExpressionSyntax expressionConstraint) {
        return new TypeParameterConstraintClause(TypeParameterConstraintKinds.Expression, [], expressionConstraint);
    }

    private TypeParameterConstraintClause(
        TypeParameterConstraintKinds constraints,
        ImmutableArray<TypeWithAnnotations> constraintTypes,
        ExpressionSyntax expressionConstraint) {
        this.constraints = constraints;
        this.constraintTypes = constraintTypes;
        expression = expressionConstraint;
    }

    internal readonly TypeParameterConstraintKinds constraints;
    internal readonly ImmutableArray<TypeWithAnnotations> constraintTypes;
    internal readonly ExpressionSyntax expression;

    internal static Dictionary<TemplateParameterSymbol, bool> BuildIsValueTypeFromConstraintTypesMap(
        ImmutableArray<TemplateParameterSymbol> typeParameters,
        ImmutableArray<TypeParameterConstraintClause> constraintClauses) {
        var isValueTypeMap = new Dictionary<TemplateParameterSymbol, bool>(ReferenceEqualityComparer.Instance);

        foreach (var typeParameter in typeParameters) {
            IsValueType(
                typeParameter,
                constraintClauses,
                isValueTypeMap,
                ConsList<TemplateParameterSymbol>.Empty
            );
        }

        return isValueTypeMap;

        static bool IsValueType(
            TemplateParameterSymbol thisTypeParameter,
            ImmutableArray<TypeParameterConstraintClause> constraintClauses,
            Dictionary<TemplateParameterSymbol, bool> isValueTypeMap,
            ConsList<TemplateParameterSymbol> inProgress) {
            if (inProgress.ContainsReference(thisTypeParameter))
                return false;

            if (isValueTypeMap.TryGetValue(thisTypeParameter, out var knownIsValueType))
                return knownIsValueType;

            var constraintClause = constraintClauses[thisTypeParameter.ordinal];
            var result = false;

            if ((constraintClause.constraints & TypeParameterConstraintKinds.ValueType) != 0) {
                result = true;
            } else {
                var container = thisTypeParameter.containingSymbol;
                inProgress = inProgress.Prepend(thisTypeParameter);

                foreach (var constraintType in constraintClause.constraintTypes) {
                    var type = constraintType.type;

                    if (type is TemplateParameterSymbol typeParameter &&
                        (object)typeParameter.containingSymbol == container) {
                        if (IsValueType(typeParameter, constraintClauses, isValueTypeMap, inProgress)) {
                            result = true;
                            break;
                        }
                    } else if (type.isValueType) {
                        result = true;
                        break;
                    }
                }
            }

            isValueTypeMap.Add(thisTypeParameter, result);
            return result;
        }
    }

    internal static Dictionary<TemplateParameterSymbol, bool> BuildIsReferenceTypeFromConstraintTypesMap(
        ImmutableArray<TemplateParameterSymbol> typeParameters,
        ImmutableArray<TypeParameterConstraintClause> constraintClauses) {
        var isReferenceTypeMap = new Dictionary<TemplateParameterSymbol, bool>(ReferenceEqualityComparer.Instance);

        foreach (var typeParameter in typeParameters) {
            IsReferenceType(
                typeParameter,
                constraintClauses,
                isReferenceTypeMap,
                ConsList<TemplateParameterSymbol>.Empty
            );
        }

        return isReferenceTypeMap;

        static bool IsReferenceType(
            TemplateParameterSymbol thisTypeParameter,
            ImmutableArray<TypeParameterConstraintClause> constraintClauses,
            Dictionary<TemplateParameterSymbol, bool> isReferenceTypeMap,
            ConsList<TemplateParameterSymbol> inProgress) {
            if (inProgress.ContainsReference(thisTypeParameter))
                return false;

            if (isReferenceTypeMap.TryGetValue(thisTypeParameter, out var knownIsReferenceType))
                return knownIsReferenceType;

            var constraintClause = constraintClauses[thisTypeParameter.ordinal];
            var result = false;

            var container = thisTypeParameter.containingSymbol;
            inProgress = inProgress.Prepend(thisTypeParameter);

            foreach (var constraintType in constraintClause.constraintTypes) {
                var type = constraintType.type;

                if (type is TemplateParameterSymbol typeParameter) {
                    if ((object)typeParameter.containingSymbol == container) {
                        if (IsReferenceType(typeParameter, constraintClauses, isReferenceTypeMap, inProgress)) {
                            result = true;
                            break;
                        }
                    } else if (typeParameter.isReferenceTypeFromConstraintTypes) {
                        result = true;
                        break;
                    }
                } else if (TemplateParameterSymbol.NonTypeParameterConstraintImpliesReferenceType(type)) {
                    result = true;
                    break;
                }
            }

            isReferenceTypeMap.Add(thisTypeParameter, result);
            return result;
        }
    }
}
