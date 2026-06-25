using System.Collections.Generic;
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
        var bounds = templateParameter.ResolveBoundsCore(
            inProgress,
            constraintTypes,
            inherited,
            currentCompilation,
            diagnostics,
            errorLocation
        );

        if (templateParameter.hasValueTypeConstraint && templateParameter.hasReferenceTypeConstraint)
            diagnostics.Push(Error.TemplateBaseBothReferenceAndValueType(errorLocation, templateParameter.name));

        return bounds;
    }

    internal static TypeParameterBounds ResolveBoundsCore(
        this TemplateParameterSymbol templateParameter,
        ConsList<TemplateParameterSymbol> inProgress,
        ImmutableArray<TypeWithAnnotations> constraintTypes,
        bool inherited,
        Compilation currentCompilation,
        BelteDiagnosticQueue diagnostics,
        TextLocation errorLocation) {
        var effectiveBaseClass = CorLibrary.GetSpecialType(SpecialType.Object);
        TypeSymbol deducedBaseType = effectiveBaseClass;

        ImmutableArray<NamedTypeSymbol> interfaces;

        if (constraintTypes.Length != 0) {
            var constraintTypesBuilder = ArrayBuilder<TypeWithAnnotations>.GetInstance();
            var interfacesBuilder = ArrayBuilder<NamedTypeSymbol>.GetInstance();

            foreach (var constraintType in constraintTypes) {
                NamedTypeSymbol constraintEffectiveBase;
                TypeSymbol constraintDeducedBase;
                var strippedConstraintType = constraintType.type.StrippedType();

                switch (strippedConstraintType.typeKind) {
                    case TypeKind.TemplateParameter:
                        var constraintTypeParameter = (TemplateParameterSymbol)strippedConstraintType;
                        ConsList<TemplateParameterSymbol> constraintsInProgress;

                        if (constraintTypeParameter.underlyingType.specialType != SpecialType.Type)
                            diagnostics.Push(Error.CannotDeriveTemplate(errorLocation, constraintTypeParameter));

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
                        AddInterfaces(interfacesBuilder, constraintTypeParameter.GetInterfaces(constraintsInProgress));

                        if (!inherited &&
                            currentCompilation is not null &&
                            constraintTypeParameter.IsFromCompilation(currentCompilation)) {
                            if (constraintTypeParameter.hasValueTypeConstraint) {
                                diagnostics.Push(
                                    Error.TemplateObjectBaseWithValueTypeBase(
                                        errorLocation,
                                        constraintTypeParameter.name,
                                        templateParameter.name
                                    )
                                );

                                continue;
                            }
                        }

                        break;
                    case TypeKind.Class:
                    case TypeKind.Interface:
                        if (constraintType.type.IsInterfaceType()) {
                            AddInterface(interfacesBuilder, (NamedTypeSymbol)constraintType.type);
                            constraintTypesBuilder.Add(constraintType);
                            continue;
                        } else {
                            constraintEffectiveBase = (NamedTypeSymbol)constraintType.type;
                            constraintDeducedBase = constraintType.type;
                            break;
                        }
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
                    case TypeKind.Enum:
                        constraintEffectiveBase = CorLibrary.GetSpecialType(SpecialType.Enum);
                        constraintDeducedBase = constraintType.type;
                        break;
                    case TypeKind.Error:
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
                            !templateParameter.hasValueTypeConstraint) {
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
            interfaces = interfacesBuilder.ToImmutableAndFree();
        } else {
            interfaces = [];
        }

        if ((constraintTypes.Length == 0) && (deducedBaseType.specialType == SpecialType.Object))
            return null;

        var bounds = new TypeParameterBounds(constraintTypes, interfaces, effectiveBaseClass, deducedBaseType);

        if (inherited)
            CheckOverrideConstraints(templateParameter, bounds, diagnostics, errorLocation);

        return bounds;
    }

    private static void AddInterface(ArrayBuilder<NamedTypeSymbol> builder, NamedTypeSymbol @interface) {
        if (!builder.Contains(@interface))
            builder.Add(@interface);
    }

    private static void AddInterfaces(
        ArrayBuilder<NamedTypeSymbol> builder,
        ImmutableArray<NamedTypeSymbol> interfaces) {
        foreach (var @interface in interfaces)
            AddInterface(builder, @interface);
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

        var isValueTypeFromConstraintTypesMap = TypeParameterConstraintClause.BuildIsValueTypeFromConstraintTypesMap(
            templateParameters,
            constraintClauses
        );

        var isReferenceTypeFromConstraintTypesMap = TypeParameterConstraintClause.BuildIsReferenceTypeFromConstraintTypesMap(
            templateParameters,
            constraintClauses
        );

        ArrayBuilder<TypeParameterConstraintClause> builder = null;

        for (var i = 0; i < arity; i++) {
            var constraint = constraintClauses[i];
            var typeParameter = templateParameters[i];
            var constraintKind = constraint.constraints;

            if ((constraintKind & TypeParameterConstraintKinds.ValueType) == 0 &&
                isValueTypeFromConstraintTypesMap[typeParameter]) {
                constraintKind |= TypeParameterConstraintKinds.ValueType;
            }

            if (isReferenceTypeFromConstraintTypesMap[typeParameter])
                constraintKind |= TypeParameterConstraintKinds.ReferenceType;

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

    internal static void CheckAllConstraints(
        this TypeSymbol type,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        // TODO This is probably wrong, I don't think this is exhaustive of all types
        while (true) {
            var current = type;

            switch (type.typeKind) {
                case TypeKind.Class:
                case TypeKind.Struct:
                case TypeKind.Interface:

                    var containingType = current.containingType;

                    if (containingType is not null)
                        CheckConstraintsSingleType(containingType, location, diagnostics);

                    break;
            }

            TypeWithAnnotations next;

            switch (type.typeKind) {
                case TypeKind.TemplateParameter:
                case TypeKind.Primitive:
                    return;
                case TypeKind.Error:
                case TypeKind.Class:
                case TypeKind.Interface:
                case TypeKind.Enum:
                case TypeKind.Struct:
                    var typeArguments = ((NamedTypeSymbol)current).templateArguments;

                    if (typeArguments.IsEmpty)
                        return;

                    var nextType = typeArguments[0].type.nullableUnderlyingTypeOrSelf;

                    if (nextType is NamedTypeSymbol namedNext)
                        CheckConstraintsSingleType(namedNext, location, diagnostics);

                    return;
                case TypeKind.Array:
                    next = ((ArrayTypeSymbol)current).elementTypeWithAnnotations;
                    break;
                case TypeKind.Pointer:
                    next = ((PointerTypeSymbol)current).pointedAtTypeWithAnnotations;
                    break;
                case TypeKind.FunctionPointer:
                    VisitFunctionPointerType((FunctionPointerTypeSymbol)current, out next);
                    break;
                case TypeKind.Function:
                    VisitFunctionType((FunctionTypeSymbol)current, out next);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(current.typeKind);
            }

            type = next.nullableUnderlyingTypeOrSelf;
        }

        void VisitFunctionPointerType(FunctionPointerTypeSymbol type, out TypeWithAnnotations next) {
            MethodSymbol currentPointer = type.signature;

            if (currentPointer.parameterCount == 0) {
                next = currentPointer.returnTypeWithAnnotations;
                return;
            }

            CheckAllConstraints(currentPointer.returnType, location, diagnostics);

            int i;
            for (i = 0; i < currentPointer.parameterCount - 1; i++)
                CheckAllConstraints(currentPointer.parameters[i].type, location, diagnostics);

            next = currentPointer.parameters[i].typeWithAnnotations;
            return;
        }

        void VisitFunctionType(FunctionTypeSymbol type, out TypeWithAnnotations next) {
            MethodSymbol current = type.signature;

            if (current.parameterCount == 0) {
                next = current.returnTypeWithAnnotations;
                return;
            }

            CheckAllConstraints(current.returnType, location, diagnostics);

            int i;
            for (i = 0; i < current.parameterCount - 1; i++)
                CheckAllConstraints(current.parameters[i].type, location, diagnostics);

            next = current.parameters[i].typeWithAnnotations;
            return;
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
        SyntaxNode typeSyntax,
        ConsList<TypeSymbol> basesBeingResolved) {
        if (!RequiresChecking(type))
            return true;

        var result = !typeSyntax.containsDiagnostics && CheckTypeConstraints(type, location, diagnostics);

        if (HasDuplicateInterfaces(type, basesBeingResolved))
            result = false;

        return result;
    }

    private static bool HasDuplicateInterfaces(NamedTypeSymbol type, ConsList<TypeSymbol> basesBeingResolved) {
        if (type.originalDefinition is not PENamedTypeSymbol)
            return false;

        var array = type.originalDefinition.Interfaces(basesBeingResolved);

        switch (array.Length) {
            case 0:
            case 1:
                return false;
            case 2:
                if ((object)array[0].originalDefinition == array[1].originalDefinition)
                    break;

                return false;
            default:
                var set = PooledHashSet<object>.GetInstance();

                foreach (var i in array) {
                    if (!set.Add(i.originalDefinition)) {
                        set.Free();
                        goto hasRelatedInterfaces;
                    }
                }

                set.Free();
                return false;
        }

hasRelatedInterfaces:
        return type.Interfaces(basesBeingResolved).HasDuplicates(SymbolEqualityComparer.IgnoreTupleNames);
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

        if (n > 0 && substitution is not null) {
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
        }

        if (containingSymbol is NamedTypeSymbol named) {
            foreach (var constraint in named.originalDefinition.templateConstraints)
                EvaluateConstraint(constraint, location, templateParameters, templateArguments, diagnostics);
        }

        return succeeded;
    }

    private static void EvaluateConstraint(
        BoundExpression constraint,
        TextLocation location,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<TypeOrConstant> templateArguments,
        BelteDiagnosticQueue diagnostics) {
        var n = templateParameters.Length;
        var names = new Dictionary<string, int>(n, StringOrdinalComparer.Instance);

        foreach (var templateParameter in templateParameters) {
            var name = templateParameter.name;

            if (!names.ContainsKey(name))
                names.Add(name, names.Count);
        }

        var result = EvaluateConstraintCore(constraint, names, templateArguments, diagnostics);

        if (result is null)
            diagnostics.Push(Error.ConstraintFailedToEvaluate(location, constraint.syntax.ToString()));
        else if (result.value is null)
            diagnostics.Push(Error.ConstraintWasNull(location, constraint.syntax.ToString()));
        else if (!(bool)result.value)
            diagnostics.Push(Error.ConstraintFailed(location, constraint.syntax.ToString()));

        static ConstantValue EvaluateConstraintCore(
            BoundExpression expression,
            Dictionary<string, int> names,
            ImmutableArray<TypeOrConstant> templateArguments,
            BelteDiagnosticQueue diagnostics) {
            if (expression.constantValue is not null)
                return expression.constantValue;

            switch (expression.kind) {
                case BoundKind.UnaryOperator:
                    var unary = (BoundUnaryOperator)expression;
                    return ConstantFolding.FoldUnary(
                        EvaluateConstraintCore(unary.operand, names, templateArguments, diagnostics),
                        unary.operatorKind, unary.Type());
                case BoundKind.BinaryOperator:
                    var binary = (BoundBinaryOperator)expression;
                    return ConstantFolding.FoldBinary(
                        EvaluateConstraintCore(binary.left, names, templateArguments, diagnostics),
                        binary.left.type,
                        EvaluateConstraintCore(binary.right, names, templateArguments, diagnostics),
                        binary.right.type,
                        binary.operatorKind,
                        binary.left.Type(),
                        binary.syntax.location,
                        diagnostics);
                case BoundKind.IsOperator:
                    var isOperator = (BoundIsOperator)expression;
                    return ConstantFolding.FoldIs(
                        EvaluateConstraintCore(isOperator.left, names, templateArguments, diagnostics),
                        EvaluateConstraintCore(isOperator.right, names, templateArguments, diagnostics),
                        isOperator.isNot);
                case BoundKind.NullCoalescingOperator:
                    var nullCoalescing = (BoundNullCoalescingOperator)expression;
                    return ConstantFolding.FoldNullCoalescing(
                        EvaluateConstraintCore(nullCoalescing.left, names, templateArguments, diagnostics),
                        EvaluateConstraintCore(nullCoalescing.right, names, templateArguments, diagnostics),
                        nullCoalescing.isPropagation,
                        nullCoalescing.Type());
                case BoundKind.NullAssertOperator:
                    var nullAssert = (BoundNullAssertOperator)expression;
                    return ConstantFolding.FoldNullAssert(
                        EvaluateConstraintCore(nullAssert.operand, names, templateArguments, diagnostics));
                case BoundKind.CastExpression:
                    var cast = (BoundCastExpression)expression;
                    return ConstantFolding.FoldCast(
                        EvaluateConstraintCore(cast.operand, names, templateArguments, diagnostics),
                        expression.syntax.location,
                        cast.operand.type,
                        new TypeWithAnnotations(cast.type),
                        diagnostics);
                case BoundKind.ConditionalOperator:
                    var conditional = (BoundConditionalOperator)expression;
                    return ConstantFolding.FoldConditional(
                        EvaluateConstraintCore(conditional.condition, names, templateArguments, diagnostics),
                        EvaluateConstraintCore(conditional.trueExpression, names, templateArguments, diagnostics),
                        EvaluateConstraintCore(conditional.falseExpression, names, templateArguments, diagnostics),
                        conditional.Type());
                case BoundKind.TypeExpression:
                    var templateParameter = (TemplateParameterSymbol)expression.type;
                    return templateArguments[names[templateParameter.name]].constant;
                default:
                    return new ConstantValue(false, SpecialType.Bool);
            }
        }
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

        // TODO
        // if (templateArgument.type?.type is NamedTypeSymbol { isInterface: true } iface &&
        //     SelfOrBaseHasStaticAbstractMember(iface, out Symbol member)) {
        //         diagnostics.Push(Error.TemplateConstraintNotSatisfiedInterfaceWithStaticAbstractMembers(iface.location, member)));
        //     diagnosticsBuilder.Add(new TypeParameterDiagnosticInfo(typeParameter,
        //         new UseSiteInfo<AssemblySymbol>(new CSDiagnosticInfo(ErrorCode.ERR_GenericConstraintNotSatisfiedInterfaceWithStaticAbstractMembers, iface, member))));
        //     hasError = true;
        // }

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

        if (templateArgument.type.IsVoidType() ||
            templateArgument.type.type.StrippedType().IsPointerOrFunctionPointer()) {
            diagnostics.Push(Error.BadTemplateArgument(location, templateArgument.type.type));
            return false;
        }

        if (templateArgument.type.type.StrippedType().isStatic) {
            diagnostics.Push(Error.TemplateIsStatic(location, templateArgument.type.type));
            return false;
        }

        if (templateParameter.hasReferenceTypeConstraint && !templateArgument.type.type.StrippedType().isReferenceType) {
            diagnostics.Push(Error.ReferenceTypeConstraintFailed(
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

        if (templateParameter.hasValueTypeConstraint && !templateArgument.type.type.StrippedType().isValueType) {
            diagnostics.Push(Error.ValueTypeConstraintFailed(
                location,
                containingSymbol.ConstructedFrom(),
                templateParameter.name,
                templateArgument.type.type
            ));

            return false;
        }

        if (templateParameter.hasDefaultConstraint && !templateArgument.type.type.HasDefaultValue()) {
            diagnostics.Push(Error.DefaultConstraintFailed(
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

        // TODO Distinguish diagnostics for ref/val types, class/interface, etc.
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

        if (IsValueType(templateParameter, constraintTypes) && IsReferenceType(templateParameter, constraintTypes)) {
            diagnostics.Push(Error.TemplateBaseBothReferenceAndValueType(errorLocation, templateParameter.name));
        } else if (deducedBase.IsNullableType() &&
            (templateParameter.hasValueTypeConstraint || templateParameter.hasReferenceTypeConstraint)) {
            diagnostics.Push(Error.TemplateBaseBothReferenceAndValueType(errorLocation, templateParameter.name));
        }
    }

    private static bool IsValueType(
        TemplateParameterSymbol templateParameter,
        ImmutableArray<TypeWithAnnotations> constraintTypes) {
        return templateParameter.hasValueTypeConstraint ||
            TemplateParameterSymbol.CalculateIsValueTypeFromConstraintTypes(constraintTypes);
    }

    private static bool IsReferenceType(
        TemplateParameterSymbol templateParameter,
        ImmutableArray<TypeWithAnnotations> constraintTypes) {
        return templateParameter.hasReferenceTypeConstraint ||
            TemplateParameterSymbol.CalculateIsReferenceTypeFromConstraintTypes(constraintTypes);
    }
}
