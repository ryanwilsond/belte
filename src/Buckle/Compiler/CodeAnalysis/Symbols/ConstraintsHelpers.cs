using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstraintsHelpers {
    internal static TypeParameterBounds ResolveBounds(
        this TemplateParameterSymbol templateParameter,
        ConsList<TemplateParameterSymbol> inProgress,
        ImmutableArray<TypeWithAnnotations> constraintTypes,
        bool inherited,
        Compilation currentCompilation,
        BelteDiagnosticQueue diagnostics,
        TextLocation errorLocation) {
        var effectiveBaseClass = CorLibrary.GetSpecialType(SpecialType.Object);
        TypeSymbol deducedBaseType = effectiveBaseClass;

        if (constraintTypes.Length != 0) {
            var constraintTypesBuilder = ArrayBuilder<TypeWithAnnotations>.GetInstance();

            foreach (var constraintType in constraintTypes) {
                NamedTypeSymbol constraintEffectiveBase;
                TypeSymbol constraintDeducedBase;
                var strippedConstraintType = constraintType.type.StrippedType();

                switch (strippedConstraintType.typeKind) {
                    case TypeKind.TemplateParameter:
                        var constraintTypeParameter = (TemplateParameterSymbol)strippedConstraintType;
                        ConsList<TemplateParameterSymbol> constraintsInProgress;

                        if (constraintTypeParameter.containingSymbol == templateParameter.containingSymbol) {
                            if (inProgress.ContainsReference(constraintTypeParameter)) {
                                diagnostics.Push(
                                    Error.CircularConstraint(
                                        errorLocation,
                                        constraintTypeParameter.name,
                                        templateParameter.name
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

                        if (!inherited &&
                            currentCompilation is not null &&
                            constraintTypeParameter.IsFromCompilation(currentCompilation)) {
                            if (constraintTypeParameter.hasPrimitiveTypeConstraint) {
                                diagnostics.Push(
                                    Error.TemplateObjectBaseWithPrimitiveBase(
                                        errorLocation,
                                        constraintTypeParameter.name,
                                        templateParameter.name
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
                                            Error.CircularConstraint(
                                                errorLocation,
                                                underlyingTypeParameter.name,
                                                templateParameter.name
                                            )
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
                        if (!IsEncompassedBy(constraintDeducedBase, deducedBaseType) &&
                            !templateParameter.hasPrimitiveTypeConstraint) {
                            diagnostics.Push(
                                Error.TemplateBaseConstraintConflict(
                                    errorLocation,
                                    templateParameter.name,
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

        // TODO We always want to check this, right?
        // if (inherited)
        CheckOverrideConstraints(templateParameter, bounds, diagnostics, errorLocation);

        return bounds;
    }

    internal static ImmutableArray<ImmutableArray<TypeWithAnnotations>> MakeTypeParameterConstraintTypes(
        this MethodSymbol containingSymbol,
        Binder withTemplateParametersBinder,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        TemplateParameterListSyntax templateParameterList,
        SyntaxList<TemplateConstraintClauseSyntax> constraintClauses,
        BelteDiagnosticQueue diagnostics) {
        if (templateParameters.Length == 0 || constraintClauses is null || constraintClauses.Count == 0)
            return [];

        withTemplateParametersBinder = withTemplateParametersBinder
            .WithAdditionalFlags(BinderFlags.TemplateConstraintsClause | BinderFlags.SuppressConstraintChecks);

        var clauses = withTemplateParametersBinder.BindTypeParameterConstraintClauses(
            containingSymbol,
            templateParameters,
            templateParameterList,
            constraintClauses,
            diagnostics
        );

        if (clauses.All(clause => clause.constraintTypes.IsEmpty))
            return [];

        return clauses.SelectAsArray(clause => clause.constraintTypes);
    }

    internal static ImmutableArray<TypeParameterConstraintKinds> MakeTypeParameterConstraintKinds(
        this MethodSymbol containingSymbol,
        Binder withTemplateParametersBinder,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        TemplateParameterListSyntax templateParameterList,
        SyntaxList<TemplateConstraintClauseSyntax> constraintClauses) {
        if (templateParameters.Length == 0)
            return [];

        ImmutableArray<TypeParameterConstraintClause> clauses;

        if (constraintClauses is null || constraintClauses.Count == 0) {
            clauses = withTemplateParametersBinder.GetDefaultTypeParameterConstraintClauses(templateParameterList);
        } else {
            withTemplateParametersBinder = withTemplateParametersBinder.WithAdditionalFlags(
                BinderFlags.TemplateConstraintsClause |
                BinderFlags.SuppressConstraintChecks |
                BinderFlags.SuppressTemplateArgumentBinding
            );

            clauses = withTemplateParametersBinder.BindTypeParameterConstraintClauses(
                containingSymbol,
                templateParameters,
                templateParameterList,
                constraintClauses,
                BelteDiagnosticQueue.Discarded
            );

            clauses = AdjustConstraintKindsBasedOnConstraintTypes(templateParameters, clauses);
        }

        if (clauses.All(clause => clause.constraints == TypeParameterConstraintKinds.None))
            return [];

        return clauses.SelectAsArray(clause => clause.constraints);
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
                if (builder is null) {
                    builder = ArrayBuilder<TypeParameterConstraintClause>.GetInstance(constraintClauses.Length);
                    builder.AddRange(constraintClauses);
                }

                builder[i] = TypeParameterConstraintClause.Create(constraintKind, constraint.constraintTypes);
            }
        }

        if (builder is not null)
            constraintClauses = builder.ToImmutableAndFree();

        return constraintClauses;
    }

    internal static void CheckAllConstraints(this TypeSymbol type, TextLocation location, BelteDiagnosticQueue diagnostics) {
        while (true) {
            var current = type;

            switch (type.typeKind) {
                case TypeKind.Class:
                case TypeKind.Struct:
                    CheckConstraintsSingleType((NamedTypeSymbol)type, location, diagnostics);
                    return;
            }

            TypeWithAnnotations next;

            switch (type.typeKind) {
                case TypeKind.TemplateParameter:
                case TypeKind.Primitive:
                    return;
                case TypeKind.Error:
                case TypeKind.Class:
                case TypeKind.Struct:
                    var typeArguments = ((NamedTypeSymbol)current).templateArguments;

                    if (typeArguments.IsEmpty)
                        return;

                    var nextType = typeArguments[0].type.nullableUnderlyingTypeOrSelf;
                    CheckConstraintsSingleType((NamedTypeSymbol)nextType, location, diagnostics);
                    return;
                case TypeKind.Array:
                    next = ((ArrayTypeSymbol)current).elementTypeWithAnnotations;
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(current.typeKind);
            }

            type = next.nullableUnderlyingTypeOrSelf;
        }
    }

    private static void CheckConstraintsSingleType(
        NamedTypeSymbol type,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        type.CheckConstraints(
            location,
            diagnostics,
            type.templateSubstitution,
            type.templateParameters,
            type.templateArguments
        );
    }

    internal static bool CheckConstraintsForNamedType(
        this NamedTypeSymbol type,
        TextLocation location,
        BelteDiagnosticQueue diagnostics,
        SyntaxNode typeSyntax) {
        if (!RequiresChecking(type))
            return true;

        return !typeSyntax.containsDiagnostics && CheckTypeConstraints(type, location, diagnostics);
    }

    private static bool CheckTypeConstraints(
        NamedTypeSymbol type,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        return CheckConstraints(
            type,
            location,
            diagnostics,
            type.templateSubstitution,
            type.originalDefinition.templateParameters,
            type.templateArguments
        );
    }

    internal static bool CheckMethodConstraints(
        this MethodSymbol method,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        return CheckConstraints(
            method,
            location,
            diagnostics,
            method.templateSubstitution,
            method.originalDefinition.templateParameters,
            method.templateArguments
        );
    }

    internal static bool CheckConstraints(
        this Symbol containingSymbol,
        TextLocation location,
        BelteDiagnosticQueue diagnostics,
        TemplateMap substitution,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<TypeOrConstant> templateArguments) {
        var n = templateParameters.Length;
        var succeeded = true;

        for (var i = 0; i < n; i++) {
            if (!CheckConstraints(
                containingSymbol,
                location,
                diagnostics,
                substitution,
                templateParameters[i],
                templateArguments[i])) {
                succeeded = false;
            }
        }

        // TODO Constraint expressions

        return succeeded;
    }

    private static bool CheckConstraints(
        Symbol containingSymbol,
        TextLocation location,
        BelteDiagnosticQueue diagnostics,
        TemplateMap substitution,
        TemplateParameterSymbol templateParameter,
        TypeOrConstant templateArgument) {
        if (templateArgument.type?.type?.IsErrorType() ?? false)
            return true;

        if (!CheckBasicConstraints(containingSymbol, location, diagnostics, templateParameter, templateArgument))
            return false;

        var constraintTypes = ArrayBuilder<TypeWithAnnotations>.GetInstance();
        var originalConstraintTypes = templateParameter.constraintTypes;
        substitution.SubstituteConstraintTypesDistinctWithoutModifiers(originalConstraintTypes, constraintTypes);
        var hasError = false;

        foreach (var constraintType in constraintTypes) {
            CheckConstraintType(
                containingSymbol,
                location,
                diagnostics,
                templateParameter,
                templateArgument,
                constraintType,
                ref hasError
            );
        }

        constraintTypes.Free();

        return !hasError;
    }

    private static bool CheckBasicConstraints(
        Symbol containingSymbol,
        TextLocation location,
        BelteDiagnosticQueue diagnostics,
        TemplateParameterSymbol templateParameter,
        TypeOrConstant templateArgument) {
        if (templateArgument.isConstant)
            return true;

        if (templateArgument.type.IsVoidType()) {
            diagnostics.Push(Error.BadTemplateArgument(location, templateArgument.type.type));
            return false;
        }

        if (templateArgument.type.type.StrippedType().isStatic) {
            diagnostics.Push(Error.TemplateIsStatic(location, templateArgument.type.type));
            return false;
        }

        if (templateParameter.hasObjectTypeConstraint && !templateArgument.type.type.StrippedType().isObjectType) {
            diagnostics.Push(Error.ObjectConstraintFailed(
                location,
                containingSymbol.ConstructedFrom(),
                templateParameter.name,
                templateArgument.type.type
            ));

            return false;
        }

        if (templateParameter.hasNotNullConstraint && templateArgument.type.isNullable) {
            diagnostics.Push(Error.NotNullableConstraintFailed(
                location,
                containingSymbol.ConstructedFrom(),
                templateParameter.name,
                templateArgument.type.type
            ));
        }

        if (templateParameter.hasPrimitiveTypeConstraint && !templateArgument.type.type.StrippedType().isPrimitiveType) {
            diagnostics.Push(Error.PrimitiveConstraintFailed(
                location,
                containingSymbol.ConstructedFrom(),
                templateParameter.name,
                templateArgument.type.type
            ));

            return false;
        }

        return true;
    }

    private static void CheckConstraintType(
        Symbol containingSymbol,
        TextLocation location,
        BelteDiagnosticQueue diagnostics,
        TemplateParameterSymbol templateParameter,
        TypeOrConstant templateArgument,
        TypeWithAnnotations constraintType,
        ref bool hasError) {
        if (templateArgument.isConstant)
            return;

        if (SatisfiesConstraintType(templateArgument.type.type, constraintType.type))
            return;

        diagnostics.Push(Error.ExtendConstraintFailed(
            location,
            containingSymbol.ConstructedFrom(),
            templateParameter.name,
            templateArgument.type.type,
            constraintType.type
        ));

        hasError = true;
    }

    private static bool SatisfiesConstraintType(TypeSymbol typeArgument, TypeSymbol constraintType) {
        if (constraintType.IsErrorType())
            return false;

        // TODO Does this properly handle nested template arguments or constructed types? (I think not)
        if (typeArgument.InheritsFromIgnoringConstruction((NamedTypeSymbol)constraintType))
            return true;

        return false;
    }

    internal static bool RequiresChecking(NamedTypeSymbol type) {
        if (type.arity == 0)
            return false;

        if (ReferenceEquals(type.originalDefinition, type))
            return false;

        return true;
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

        if (IsPrimitiveType(templateParameter, constraintTypes) && IsObjectType(templateParameter, constraintTypes)) {
            diagnostics.Push(Error.TemplateBaseBothObjectAndPrimitive(errorLocation, templateParameter.name));
        } else if (deducedBase.IsNullableType() &&
            (templateParameter.hasPrimitiveTypeConstraint || templateParameter.hasObjectTypeConstraint)) {
            diagnostics.Push(Error.TemplateBaseBothObjectAndPrimitive(errorLocation, templateParameter.name));
        }
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
