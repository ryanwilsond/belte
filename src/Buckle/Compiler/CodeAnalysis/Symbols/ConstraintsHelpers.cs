using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static partial class ConstraintsHelpers {
    internal static bool CheckConstraints(
        this MethodSymbol method,
        ConversionsBase conversions,
        TextLocation location,
        ImmutableArray<BoundExpression> impliedConstraints,
        ImmutableArray<BoundExpressionOrTypeOrConstant> arguments,
        BelteDiagnosticQueue diagnostics,
        Symbol requester = null) {
        if (!RequiresChecking(method))
            return true;

        var result = CheckMethodConstraints(
            method,
            conversions,
            location,
            impliedConstraints,
            arguments,
            diagnostics,
            requester
        );

        return result;
    }

    internal static bool CheckConstraints(
        this NamedTypeSymbol namedType,
        ConversionsBase conversions,
        TextLocation location,
        ImmutableArray<BoundExpression> impliedConstraints,
        BelteDiagnosticQueue diagnostics,
        Symbol requester) {
        if (!RequiresChecking(namedType))
            return true;

        var result = CheckTypeConstraints(
            namedType,
            conversions,
            location,
            impliedConstraints,
            diagnostics,
            requester
        );

        return result;
    }

    internal static bool RequiresChecking(MethodSymbol method) {
        if (method.GetMemberArity() == 0)
            return false;

        if (ReferenceEquals(method.originalDefinition, method))
            return false;

        return true;
    }

    internal static TypeParameterBounds ResolveBounds(
        this TemplateParameterSymbol templateParameter,
        CorLibrary corLibrary,
        ConsList<TemplateParameterSymbol> inProgress,
        ImmutableArray<TypeWithAnnotations> constraintTypes,
        bool inherited,
        Compilation currentCompilation,
        BelteDiagnosticQueue diagnostics,
        TextLocation errorLocation) {
        var bounds = templateParameter.ResolveBoundsCore(
            corLibrary,
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
        CorLibrary corLibrary,
        ConsList<TemplateParameterSymbol> inProgress,
        ImmutableArray<TypeWithAnnotations> constraintTypes,
        bool inherited,
        Compilation currentCompilation,
        BelteDiagnosticQueue diagnostics,
        TextLocation errorLocation) {
        var effectiveBaseClass = corLibrary.GetSpecialType(
            templateParameter.hasValueTypeConstraint ? SpecialType.ValueType : SpecialType.Object
        );

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
                        constraintEffectiveBase = currentCompilation.GetSpecialType(SpecialType.Array);
                        constraintDeducedBase = constraintType.type;
                        break;
                    case TypeKind.Enum:
                        constraintEffectiveBase = currentCompilation.GetSpecialType(SpecialType.Enum);
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

    internal static ImmutableArray<TypeParameterConstraintClause> MakeTypeParameterConstraintTypes(
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

        return clauses;
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

    private static readonly Func<TypeSymbol, (ConversionsBase, TextLocation, ImmutableArray<BoundExpression>, BelteDiagnosticQueue, Symbol), bool, bool> CheckConstraintsSingleTypeFunc =
        (type, arg, unused) => CheckConstraintsSingleType(type, arg.Item1, arg.Item2, arg.Item3, arg.Item4, arg.Item5);

    internal static void CheckAllConstraints(
        this TypeSymbol type,
        ConversionsBase conversions,
        TextLocation location,
        ImmutableArray<BoundExpression> impliedConstraints,
        BelteDiagnosticQueue diagnostics,
        Symbol requester = null) {
        type.VisitType(CheckConstraintsSingleTypeFunc, (conversions, location, impliedConstraints, diagnostics, requester));
    }

    private static bool CheckConstraintsSingleType(
        TypeSymbol type,
        ConversionsBase conversions,
        TextLocation location,
        ImmutableArray<BoundExpression> impliedConstraints,
        BelteDiagnosticQueue diagnostics,
        Symbol requester) {
        if (type is NamedTypeSymbol namedType) {
            namedType.CheckConstraints(
                conversions,
                location,
                impliedConstraints,
                diagnostics,
                requester
            );
        }

        return false;
    }

    internal static bool CheckConstraintsForNamedType(
        this NamedTypeSymbol type,
        ConversionsBase conversions,
        TextLocation location,
        BelteDiagnosticQueue diagnostics,
        SyntaxNode typeSyntax,
        ImmutableArray<BoundExpression> impliedConstraints,
        ConsList<TypeSymbol> basesBeingResolved) {
        if (!RequiresChecking(type))
            return true;

        var result = !typeSyntax.containsDiagnostics &&
            CheckTypeConstraints(type, conversions, location, impliedConstraints, diagnostics, null);

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
        ConversionsBase conversions,
        TextLocation location,
        ImmutableArray<BoundExpression> impliedConstraints,
        BelteDiagnosticQueue diagnostics,
        Symbol requester) {
        return CheckConstraints(
            type,
            conversions,
            location,
            diagnostics,
            type.templateSubstitution,
            type.originalDefinition.templateParameters,
            type.templateArguments,
            impliedConstraints,
            requester: requester
        );
    }

    internal static bool CheckMethodConstraints(
        this MethodSymbol method,
        ConversionsBase conversions,
        TextLocation location,
        ImmutableArray<BoundExpression> impliedConstraints,
        ImmutableArray<BoundExpressionOrTypeOrConstant> arguments,
        BelteDiagnosticQueue diagnostics,
        Symbol requester = null) {
        return CheckConstraints(
            method,
            conversions,
            location,
            diagnostics,
            method.templateSubstitution,
            method.originalDefinition.templateParameters,
            method.templateArguments,
            impliedConstraints,
            arguments,
            requester
        );
    }

    internal static bool CheckConstraints(
        this Symbol containingSymbol,
        ConversionsBase conversions,
        TextLocation location,
        BelteDiagnosticQueue diagnostics,
        TemplateMap substitution,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<TypeOrConstant> templateArguments,
        ImmutableArray<BoundExpression> impliedConstraints,
        ImmutableArray<BoundExpressionOrTypeOrConstant> arguments = default,
        Symbol requester = null) {
        var n = templateParameters.Length;
        var succeeded = true;

        if (n > 0 && substitution is not null) {
            for (var i = 0; i < n; i++) {
                if (!CheckConstraints(
                    containingSymbol,
                    conversions,
                    location,
                    diagnostics,
                    substitution,
                    templateParameters[i],
                    templateArguments[i])) {
                    succeeded = false;
                }
            }
        }

        if (containingSymbol is ISymbolWithTemplates) {
            foreach (var constraint in ((ISymbolWithTemplates)containingSymbol.originalDefinition).templateConstraints) {
                if (!EvaluateConstraint(
                    containingSymbol,
                    requester,
                    constraint,
                    location,
                    substitution,
                    templateParameters,
                    templateArguments,
                    impliedConstraints,
                    arguments,
                    diagnostics)) {
                    succeeded = false;
                }
            }
        }

        return succeeded;
    }

    private static bool EvaluateConstraint(
        Symbol owner,
        Symbol requester,
        BoundExpression constraint,
        TextLocation location,
        TemplateMap substitution,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<TypeOrConstant> templateArguments,
        ImmutableArray<BoundExpression> impliedConstraints,
        ImmutableArray<BoundExpressionOrTypeOrConstant> arguments,
        BelteDiagnosticQueue diagnostics) {
        var args = new EvaluationArgs(substitution, arguments, diagnostics);
        var result = EvaluateConstraintCore(constraint, args, ConsList<TemplateParameterSymbol>.Empty);

        if (result is null) {
            if (!ConstraintIsProvenByImpliedConstraints(
                    constraint,
                    substitution,
                    templateParameters,
                    templateArguments,
                    impliedConstraints)) {
                // Prefer showing the user exactly what they typed, but in the case of metadata constraints
                // there is no syntax to refer to
                var display = constraint.syntax?.ToString() ?? constraint.ToString();

                if (!args.failedToSubstituteTemplateParameter || requester is null) {
                    diagnostics.Push(Error.ConstraintFailedToEvaluate(location, owner.originalDefinition, display));
                } else {
                    // Very unconventional diagnostic creation approach, but it is worth it
                    // because constraints can be confusing and knowing that the fix can be as simple as adding a constraint is very useful
                    var inner = CreateConstraintInnerSuggestion(requester, constraint, substitution);

                    diagnostics.Push(Error.ConstraintFailedToEvaluateWithInner(
                        location,
                        owner.originalDefinition,
                        display,
                        inner
                    ));
                }

                return false;
            }
        } else if (result.value is null) {
            var display = constraint.syntax?.ToString() ?? constraint.ToString();
            diagnostics.Push(Error.ConstraintWasNull(location, owner.originalDefinition, display));
            return false;
        } else if (!(bool)result.value) {
            var display = constraint.syntax?.ToString() ?? constraint.ToString();
            diagnostics.Push(Error.ConstraintFailed(location, owner.originalDefinition, display));
            return false;
        }

        return true;

        static ConstantValue EvaluateConstraintCore(
            BoundExpression expression,
            EvaluationArgs args,
            ConsList<TemplateParameterSymbol> templateConstantsInProgress) {
            if (expression.constantValue is not null)
                return expression.constantValue;

            switch (expression.kind) {
                case BoundKind.UnaryOperator:
                    var unary = (BoundUnaryOperator)expression;
                    return ConstantFolding.FoldUnary(
                        EvaluateConstraintCore(unary.operand, args, templateConstantsInProgress),
                        unary.operatorKind, unary.Type());
                case BoundKind.BinaryOperator:
                    var binary = (BoundBinaryOperator)expression;
                    return ConstantFolding.FoldBinary(
                        EvaluateConstraintCore(binary.left, args, templateConstantsInProgress),
                        binary.left.type,
                        EvaluateConstraintCore(binary.right, args, templateConstantsInProgress),
                        binary.right.type,
                        binary.operatorKind,
                        binary.left.Type(),
                        binary.syntax?.location,
                        args.diagnostics);
                case BoundKind.IsOperator:
                    var isOperator = (BoundIsOperator)expression;
                    return ConstantFolding.FoldIs(
                        EvaluateConstraintCore(isOperator.left, args, templateConstantsInProgress),
                        EvaluateConstraintCore(isOperator.right, args, templateConstantsInProgress),
                        isOperator.isNot);
                case BoundKind.NullCoalescingOperator:
                    var nullCoalescing = (BoundNullCoalescingOperator)expression;
                    return ConstantFolding.FoldNullCoalescing(
                        EvaluateConstraintCore(nullCoalescing.left, args, templateConstantsInProgress),
                        EvaluateConstraintCore(nullCoalescing.right, args, templateConstantsInProgress),
                        nullCoalescing.isPropagation,
                        nullCoalescing.Type());
                case BoundKind.NullAssertOperator:
                    var nullAssert = (BoundNullAssertOperator)expression;
                    return ConstantFolding.FoldNullAssert(
                        EvaluateConstraintCore(nullAssert.operand, args, templateConstantsInProgress));
                case BoundKind.CastExpression:
                    var cast = (BoundCastExpression)expression;
                    return ConstantFolding.FoldCast(
                        EvaluateConstraintCore(cast.operand, args, templateConstantsInProgress),
                        expression.syntax?.location,
                        cast.operand.type,
                        new TypeWithAnnotations(cast.type),
                        args.diagnostics);
                case BoundKind.ConditionalOperator:
                    var conditional = (BoundConditionalOperator)expression;
                    return ConstantFolding.FoldConditional(
                        EvaluateConstraintCore(conditional.condition, args, templateConstantsInProgress),
                        EvaluateConstraintCore(conditional.trueExpression, args, templateConstantsInProgress),
                        EvaluateConstraintCore(conditional.falseExpression, args, templateConstantsInProgress),
                        conditional.Type());
                case BoundKind.TypeOfExpression:
                    var target = ((BoundTypeOfExpression)expression).sourceType.type;

                    if (target.StrippedType() is TemplateParameterSymbol t &&
                        t.underlyingType.specialType != SpecialType.Type) {
                        var isNullable = target.IsNullableType();

                        return new ConstantValue(
                            isNullable
                                ? t.underlyingType.SetIsAnnotated()
                                : t.underlyingType,
                            SpecialType.Type
                        );
                    }

                    var substitutedTarget = args.substitution.SubstituteType(target);
                    Debug.Assert(substitutedTarget.isType);

                    // Didn't fully substitute: abort
                    if (substitutedTarget.type.type.ContainsTemplateParameter()) {
                        args.failedToSubstituteTemplateParameter = true;
                        return null;
                    }

                    return new ConstantValue(substitutedTarget.type.type, SpecialType.Type);
                case BoundKind.TypeExpression:
                    var templateParameter = (TemplateParameterSymbol)expression.type;
                    var substituted = args.substitution.SubstituteTemplateParameter(templateParameter);

                    if (substituted.isConstant) {
                        if (substituted.constant is TemplateConstantValue templateConstantValue) {
                            if (templateConstantsInProgress.Contains(templateParameter)) {
                                args.failedToSubstituteTemplateParameter = true;
                                return null;
                            }

                            templateConstantsInProgress = templateConstantsInProgress.Prepend(templateParameter);
                            return EvaluateConstraintCore(templateConstantValue.expression, args, templateConstantsInProgress);
                        }

                        return substituted.constant;
                    }

                    args.failedToSubstituteTemplateParameter = true;
                    return null;
                case BoundKind.ParameterExpression:
                    var parameter = ((BoundParameterExpression)expression).parameter;
                    Debug.Assert(parameter.isConstExpr && !args.arguments.IsDefaultOrEmpty);
                    var argument = args.arguments[parameter.ordinal];
                    Debug.Assert(argument.isExpression && argument.expression.constantValue is not null);
                    return argument.expression.constantValue;
                default:
                    return new ConstantValue(false, SpecialType.Bool);
            }
        }
    }

    private static BelteDiagnostic CreateConstraintInnerSuggestion(
        Symbol symbol,
        BoundExpression constraint,
        TemplateMap substitution) {
        var visitor = new ConstraintDisplayVisitor(substitution);
        var rewritten = visitor.Visit(constraint);
        var display = rewritten.ToString();
        TextLocation location;

        if ((symbol as ISymbolWithTemplates).TryGetConstraintsSyntax(out var constraintsSyntax)) {
            if (constraintsSyntax.constraintClauses.Count == 0) {
                location = constraintsSyntax.closeBrace.location;
                display = $"{display}; %";
            } else {
                location = constraintsSyntax.constraintClauses[^1].GetLastToken().location;
                display = $"% {display};";
            }
        } else {
            switch (symbol) {
                case NamedTypeSymbol:
                    location = ((TypeDeclarationSyntax)((NamedTypeSymbol)symbol).GetNonNullSyntaxNode())
                        .openBrace.location;
                    display = $"where {{ {display}; }} %";
                    break;
                case MethodSymbol:
                    var syntax = (BaseMethodDeclarationSyntax)((MethodSymbol)symbol).GetNonNullSyntaxNode();

                    if (syntax.body is not null) {
                        location = syntax.body.GetFirstToken().location;
                        display = $"where {{ {display}; }} %";
                    } else {
                        location = syntax.semicolon.location;
                        display = $"where {{ {display}; }} %";
                    }

                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(symbol.kind);
            }
        }

        return Error.ConstraintFailedToEvaluateWithInner_Inner(location, symbol.originalDefinition, display);
    }

    private static bool ConstraintIsProvenByImpliedConstraints(
        BoundExpression constraint,
        TemplateMap templateMap,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<TypeOrConstant> templateArguments,
        ImmutableArray<BoundExpression> impliedConstraints) {
        // TODO SMT solver goes here...
        // For now we just check if implied constraints take the same shape as the requested constraint

        var constraintComparer = new TemplateConstraintComparer(templateMap);

        foreach (var impliedConstraint in impliedConstraints) {
            if (constraintComparer.Equals(constraint, impliedConstraint))
                return true;
        }

        return false;
    }

    private static bool CheckConstraints(
        Symbol containingSymbol,
        ConversionsBase conversions,
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
                conversions,
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

        if (templateParameter.hasConstructorConstraint && !SatisfiesConstructorConstraint(templateArgument.type.type)) {
            diagnostics.Push(Error.ConstructorConstraintFailed(
                location,
                containingSymbol.ConstructedFrom(),
                templateParameter.name,
                templateArgument.type.type
            ));

            return false;
        }

        return true;

        static bool SatisfiesConstructorConstraint(TypeSymbol type) {
            switch (type.typeKind) {
                case TypeKind.Class:
                    if (type.isAbstract)
                        return false;

                    goto case TypeKind.Struct;
                case TypeKind.Struct:
                    var namedType = (NamedTypeSymbol)type;

                    foreach (var constructor in namedType.instanceConstructors) {
                        if (constructor.parameterCount == 0 &&
                            constructor.declaredAccessibility == Accessibility.Public) {
                            return true;
                        }
                    }

                    return false;
                case TypeKind.TemplateParameter:
                    return ((TemplateParameterSymbol)type).hasConstructorConstraint;
                case TypeKind.Enum:
                case TypeKind.Primitive:
                    return true;
                case TypeKind.Array:
                case TypeKind.Error:
                case TypeKind.Interface:
                case TypeKind.Function:
                case TypeKind.FunctionPointer:
                case TypeKind.Pointer:
                default:
                    return false;
            }
        }
    }

    private static void CheckConstraintType(
        Symbol containingSymbol,
        ConversionsBase conversions,
        TextLocation location,
        BelteDiagnosticQueue diagnostics,
        TemplateParameterSymbol templateParameter,
        TypeOrConstant templateArgument,
        TypeWithAnnotations constraintType,
        ref bool hasError) {
        if (templateArgument.isConstant)
            return;

        if (SatisfiesConstraintType(conversions, templateArgument.type.type, constraintType.type))
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

    private static bool SatisfiesConstraintType(
        ConversionsBase conversions,
        TypeSymbol typeArgument,
        TypeSymbol constraintType) {
        if (constraintType.IsErrorType())
            return false;

        if (conversions.HasIdentityOrImplicitReferenceConversion(typeArgument, constraintType))
            return true;

        if (typeArgument.isReferenceType && typeArgument.IsNullableType()) {
            // We treat R? the same as R! because at runtime they are the same, so it counts as satisfying the constraint
            if (conversions.HasIdentityOrImplicitReferenceConversion(typeArgument.StrippedType(), constraintType))
                return true;
        }

        if (typeArgument.isValueType) {
            if (conversions.HasBoxingConversion(
                typeArgument.IsNullableType() ? ((NamedTypeSymbol)typeArgument).constructedFrom : typeArgument,
                constraintType)) {
                return true;
            }
        }

        if (typeArgument.typeKind == TypeKind.TemplateParameter) {
            var typeParameter = (TemplateParameterSymbol)typeArgument;

            if (conversions.HasImplicitTemplateParameterConversion(typeParameter, constraintType))
                return true;

            foreach (var typeArgumentConstraint in typeParameter.constraintTypes) {
                if (SatisfiesConstraintType(conversions, typeArgumentConstraint.type, constraintType))
                    return true;
            }
        }

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
        // TODO This should use ConversionsBase not Conversion
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
