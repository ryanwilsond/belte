using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static class ConstraintsHelpers {
    internal static TypeParameterBounds ResolveBounds(
        this SourceTemplateParameterSymbolBase templateParameter,
        ConsList<TemplateParameterSymbol> inProgress,
        ImmutableArray<TypeWithAnnotations> constraintTypes,
        bool inherited,
        Compilation currentCompilation,
        BelteDiagnosticQueue diagnostics,
        TextLocation errorLocation) {
        var effectiveBaseClass = CorLibrary.GetSpecialType(
            templateParameter.hasPrimitiveTypeConstraint ? SpecialType.None : SpecialType.Object
        );

        TypeSymbol deducedBaseType = effectiveBaseClass;

        if (constraintTypes.Length != 0) {
            var constraintTypesBuilder = ArrayBuilder<TypeWithAnnotations>.GetInstance();

            foreach (var constraintType in constraintTypes) {
                NamedTypeSymbol constraintEffectiveBase;
                TypeSymbol constraintDeducedBase;

                switch (constraintType.typeKind) {
                    case TypeKind.TemplateParameter:
                        var constraintTypeParameter = (TemplateParameterSymbol)constraintType.type;
                        ConsList<TemplateParameterSymbol> constraintsInProgress;

                        if (constraintTypeParameter.containingSymbol == templateParameter.containingSymbol) {
                            if (inProgress.ContainsReference(constraintTypeParameter)) {
                                diagnostics.Push(
                                    Error.CircularConstraint(
                                        errorLocation,
                                        constraintTypeParameter,
                                        templateParameter
                                    )
                                );

                                continue;
                            }

                            constraintsInProgress = inProgress;
                        } else {
                            constraintsInProgress = ConsList<TemplateParameterSymbol>.Empty;
                        }

                        constraintEffectiveBase = constraintTypeParameter.GetEffectiveBaseClass(constraintsInProgress);
                        constraintDeducedBase = constraintTypeParameter.GetDeducedBaseType(constraintsInProgress);

                        if (!inherited && currentCompilation != null && constraintTypeParameter.IsFromCompilation(currentCompilation)) {
                            if (constraintTypeParameter.hasPrimitiveTypeConstraint) {
                                diagnostics.Push(
                                    Error.TemplateObjectBaseWithPrimitiveBase(
                                        errorLocation,
                                        templateParameter,
                                        constraintTypeParameter
                                    )
                                );

                                continue;
                            }
                        }

                        break;
                    case TypeKind.Struct:
                        if (constraintType.IsNullableType()) {
                            var underlyingType = constraintType.type.GetNullableUnderlyingType();

                            if (underlyingType.typeKind == TypeKind.TemplateParameter) {
                                var underlyingTypeParameter = (TemplateParameterSymbol)underlyingType;

                                if (underlyingTypeParameter.containingSymbol == templateParameter.containingSymbol) {
                                    if (inProgress.ContainsReference(underlyingTypeParameter)) {
                                        diagnostics.Push(
                                            Error.CircularConstraint(errorLocation, underlyingTypeParameter, templateParameter)
                                        );

                                        continue;
                                    }
                                }
                            }
                        }

                        constraintEffectiveBase = null;
                        constraintDeducedBase = constraintType.type;
                        break;
                    case TypeKind.Array:
                        constraintEffectiveBase = CorLibrary.GetSpecialType(SpecialType.Array);
                        constraintDeducedBase = constraintType.type;
                        break;
                    case TypeKind.Error:
                    case TypeKind.Class:
                        constraintEffectiveBase = (NamedTypeSymbol)constraintType.type;
                        constraintDeducedBase = constraintType.type;
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(constraintType.typeKind);
                }

                constraintTypesBuilder.Add(constraintType);

                if (!deducedBaseType.IsErrorType() && !constraintDeducedBase.IsErrorType()) {
                    if (!IsEncompassedBy(deducedBaseType, constraintDeducedBase)) {
                        if (!IsEncompassedBy(constraintDeducedBase, deducedBaseType)) {
                            diagnostics.Push(
                                Error.TemplateBaseConstraintConflict(
                                    errorLocation,
                                    templateParameter,
                                    constraintDeducedBase,
                                    deducedBaseType
                                )
                            );
                        } else {
                            deducedBaseType = constraintDeducedBase;
                            effectiveBaseClass = constraintEffectiveBase;
                        }
                    }
                }
            }

            constraintTypes = constraintTypesBuilder.ToImmutableAndFree();
        }

        if ((constraintTypes.Length == 0) && (deducedBaseType.specialType == SpecialType.Object))
            return null;

        var bounds = new TypeParameterBounds(constraintTypes, effectiveBaseClass, deducedBaseType);

        if (inherited)
            CheckOverrideConstraints(templateParameter, bounds, diagnostics, errorLocation);

        return bounds;
    }

    internal static ImmutableArray<TypeParameterConstraintClause> AdjustConstraintKindsBasedOnConstraintTypes(
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<TypeParameterConstraintClause> constraintClauses) {
        var arity = templateParameters.Length;

        var isPrimitiveTypeMap = TypeParameterConstraintClause.BuildIsPrimitiveTypeMap(
            templateParameters,
            constraintClauses
        );

        var isObjectTypeFromConstraintTypesMap = TypeParameterConstraintClause.BuildIsObjectTypeFromConstraintTypesMap(
            templateParameters,
            constraintClauses
        );

        ArrayBuilder<TypeParameterConstraintClause> builder = null;

        for (var i = 0; i < arity; i++) {
            var constraint = constraintClauses[i];
            var typeParameter = templateParameters[i];
            var constraintKind = constraint.constraints;

            if ((constraintKind & TypeParameterConstraintKinds.Primitive) == 0 && isPrimitiveTypeMap[typeParameter])
                constraintKind |= TypeParameterConstraintKinds.Primitive;

            if (isObjectTypeFromConstraintTypesMap[typeParameter])
                constraintKind |= TypeParameterConstraintKinds.Object;

            if (constraint.constraints != constraintKind) {
                if (builder == null) {
                    builder = ArrayBuilder<TypeParameterConstraintClause>.GetInstance(constraintClauses.Length);
                    builder.AddRange(constraintClauses);
                }

                builder[i] = TypeParameterConstraintClause.Create(constraintKind, constraint.constraintTypes);
            }
        }

        if (builder != null)
            constraintClauses = builder.ToImmutableAndFree();

        return constraintClauses;
    }

    private static bool IsEncompassedBy(TypeSymbol a, TypeSymbol b) {
        return Conversion.HasIdentityOrImplicitConversion(a, b) || Conversion.HasBoxingConversion(a, b);
    }

    private static void CheckOverrideConstraints(
        TemplateParameterSymbol templateParameter,
        TypeParameterBounds bounds,
        BelteDiagnosticQueue diagnostics,
        TextLocation errorLocation) {
        var deducedBase = bounds.deducedBaseType;
        var constraintTypes = bounds.constraintTypes;

        if (IsPrimitiveType(templateParameter, constraintTypes) && IsObjectType(templateParameter, constraintTypes))
            diagnostics.Push(Error.TemplateBaseBothObjectAndPrimitive(errorLocation, templateParameter));
        else if (deducedBase.IsNullableType() && (templateParameter.hasPrimitiveTypeConstraint || templateParameter.hasObjectTypeConstraint))
            diagnostics.Push(Error.TemplateBaseBothObjectAndPrimitive(errorLocation, templateParameter));
    }

    private static bool IsPrimitiveType(
        TemplateParameterSymbol templateParameter,
        ImmutableArray<TypeWithAnnotations> constraintTypes) {
        return templateParameter.hasPrimitiveTypeConstraint ||
            TemplateParameterSymbol.CalculateIsPrimitiveTypeFromConstraintTypes(constraintTypes);
    }

    private static bool IsObjectType(
        TemplateParameterSymbol templateParameter,
        ImmutableArray<TypeWithAnnotations> constraintTypes) {
        return templateParameter.hasObjectTypeConstraint ||
            TemplateParameterSymbol.CalculateIsObjectTypeFromConstraintTypes(constraintTypes);
    }
}
