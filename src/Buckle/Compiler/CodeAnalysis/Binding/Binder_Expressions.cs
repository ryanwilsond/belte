using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    internal static bool WasImplicitReceiver(BoundExpression receiver) {
        if (receiver is null)
            return true;

        return receiver.kind switch {
            BoundKind.ThisExpression => true,
            _ => false,
        };
    }

    internal static bool IsMemberAccessedThroughType(BoundExpression receiver) {
        if (receiver is null)
            return false;

        return receiver.kind == BoundKind.TypeExpression;
    }

    internal BoundExpression BindExpression(ExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        return BindExpressionInternal(node, diagnostics, false, false);
    }

    internal BoundExpression BindToNaturalType(
        BoundExpression expression,
        BelteDiagnosticQueue diagnostics,
        bool reportNoTargetType = true) {
        if (!expression.NeedsToBeConverted())
            return expression;

        BoundExpression result;

        switch (expression) {
            case BoundUnconvertedInitializerList list:
                if (reportNoTargetType && !expression.hasAnyErrors)
                    diagnostics.Push(Error.ListNoTargetType(expression.syntax.location));

                result = BindListForErrorRecovery(list, CreateErrorType(), diagnostics);
                break;
            case BoundUnconvertedNullptrExpression:
                if (reportNoTargetType && !expression.hasAnyErrors)
                    diagnostics.Push(Error.NullptrNoTargetType(expression.syntax.location));

                result = new BoundLiteralExpression(expression.syntax, null, CreateErrorType());
                break;
            case BoundUnconvertedExtendedLiteralExpression extended:
                if (reportNoTargetType && !expression.hasAnyErrors)
                    diagnostics.Push(Error.ExtendedLiteralNoTargetType(expression.syntax.location, extended.suffix));

                result = ErrorExpression(expression.syntax, expression);
                break;
            case BoundUnconvertedConditionalOperator op: {
                    var type = op.type;
                    var hasErrors = op.hasErrors;

                    if (type is null) {
                        type = CreateErrorType();
                        hasErrors = true;

                        if (!op.hasAnyErrors) {
                            // TODO In the case that the types have the same name but are from different assemblies, this error gets cryptic
                            // TODO Eventually we will want something like a SymbolDistinguisher in this case
                            diagnostics.Push(op.noCommonError);
                        }
                    }

                    result = ConvertConditionalExpression(op, type, conversionIfTargetTyped: null, diagnostics, hasErrors);
                }

                break;
            case BoundUnconvertedImplicitEnumFieldExpression:
                if (reportNoTargetType && !expression.hasAnyErrors)
                    diagnostics.Push(Error.EnumFieldNoTargetType(expression.syntax.location));

                result = ErrorExpression(expression.syntax, expression);
                break;
            case BoundDefaultLiteral literal:
                if (reportNoTargetType)
                    diagnostics.Push(Error.DefaultLiteralNoTargetType(literal.syntax.location));

                result = new BoundDefaultExpression(
                    literal.syntax,
                    literal.isLowLevel,
                    targetType: null,
                    literal.constantValue,
                    CreateErrorType(),
                    hasErrors: true
                );

                break;
            case BoundUnconvertedObjectCreationExpression expr: {
                    if (reportNoTargetType && !expr.hasAnyErrors)
                        diagnostics.Push(Error.ObjectCreationNoTargetType(expr.syntax.location));

                    result = BindObjectCreationForErrorRecovery(expr, diagnostics);
                }

                break;
            case BoundTupleLiteral literal:
                var boundArguments = ArrayBuilder<BoundExpression>.GetInstance(literal.arguments.Length);

                foreach (var arg in literal.arguments)
                    boundArguments.Add(BindToNaturalType(arg, diagnostics, reportNoTargetType));

                result = new BoundConvertedTupleLiteral(
                    literal.syntax,
                    literal,
                    wasTargetTyped: false,
                    boundArguments.ToImmutableAndFree(),
                    literal.type,
                    literal.hasErrors
                );

                break;
            case BoundUnconvertedArrayLength:
                var arrayLength = (BoundUnconvertedArrayLength)expression;

                result = new BoundArrayLength(
                    arrayLength.syntax,
                    arrayLength.receiver,
                    CorLibrary.GetSpecialType(SpecialType.Int),
                    arrayLength.hasErrors
                );

                break;
            case BoundConditionalAccessExpression:
                var access = (BoundConditionalAccessExpression)expression;

                if (access.accessExpression is BoundUnconvertedArrayLength length) {
                    result = new BoundConditionalAccessExpression(
                        access.syntax,
                        access.receiver,
                        new BoundArrayLength(
                            length.syntax,
                            length.receiver,
                            CorLibrary.GetSpecialType(SpecialType.Int),
                            length.hasErrors
                        ),
                        access.type,
                        access.hasErrors
                    );

                    break;
                }

                goto default;
            default:
                result = expression;
                break;
        }

        return result;
    }

    internal BoundExpression BindObjectCreationForErrorRecovery(
        BoundUnconvertedObjectCreationExpression node,
        BelteDiagnosticQueue diagnostics) {
        var arguments = AnalyzedArguments.GetInstance(
            node.arguments.Select(a => new BoundExpressionOrTypeOrConstant(a)).ToImmutableArray(),
            node.arguments.Select(a => false).ToImmutableArray(),
            node.arguments.Select(a => a.syntax).ToImmutableArray(),
            node.arguments.Select(a => a.type).ToImmutableArray(),
            node.argumentRefKinds,
            node.argumentNames
        );

        var result = MakeErrorExpressionForObjectCreation(
            node.syntax,
            CreateErrorType(),
            arguments,
            node.syntax,
            diagnostics
        );

        arguments.Free();
        return result;
    }

    internal BoundExpression BindToTypeForErrorRecovery(BoundExpression expression, TypeSymbol type = null) {
        if (expression is null)
            return null;

        var result = !expression.NeedsToBeConverted()
            ? expression
            : type is null
                ? BindToNaturalType(expression, BelteDiagnosticQueue.Discarded, reportNoTargetType: false)
                : GenerateConversionForAssignment(type, expression, BelteDiagnosticQueue.Discarded);

        return result;
    }

    internal BoundExpression BindRValueWithoutTargetType(
        ExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        bool reportNoTargetType = true) {
        return BindToNaturalType(BindValue(node, diagnostics, BindValueKind.RValue), diagnostics, reportNoTargetType);
    }

    private BoundInitializerList BindListForErrorRecovery(
        BoundUnconvertedInitializerList node,
        TypeSymbol targetType,
        BelteDiagnosticQueue diagnostics) {
        var syntax = node.syntax;
        var builder = ArrayBuilder<BoundExpression>.GetInstance(node.items.Length);

        foreach (var item in node.items) {
            var result = item is BoundExpression expression
                ? BindToNaturalType(expression, diagnostics, reportNoTargetType: !targetType.IsErrorType())
                : item;

            builder.Add(result);
        }

        return new BoundInitializerList(
            syntax,
            builder.ToImmutableAndFree(),
            targetType,
            true
        );
    }

    internal BoundExpression BindValue(
        ExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        BindValueKind valueKind) {
        var result = BindExpressionInternal(node, diagnostics, false, false);
        return CheckValue(result, valueKind, diagnostics);
    }

    internal BoundExpression BindDataContainerInitializerValue(
        EqualsValueClauseSyntax initializer,
        RefKind refKind,
        TypeSymbol varType,
        BelteDiagnosticQueue diagnostics) {
        if (initializer is null)
            return null;

        IsInitializerRefKindValid(initializer, initializer, refKind, diagnostics, out var valueKind, out var value);
        var boundInitializer = BindPossibleArrayInitializer(value, varType, valueKind, diagnostics);
        boundInitializer = GenerateConversionForAssignment(varType, boundInitializer, diagnostics);
        return boundInitializer;
    }

    internal BoundExpression BindInferredDataContainerInitializer(
        BelteDiagnosticQueue diagnostics,
        RefKind refKind,
        EqualsValueClauseSyntax initializer,
        BelteSyntaxNode errorSyntax) {
        IsInitializerRefKindValid(initializer, initializer, refKind, diagnostics, out var valueKind, out var value);
        return BindInferredVariableInitializer(diagnostics, value, valueKind, errorSyntax);
    }

    internal Binder CreateBinderForParameterDefaultValue(Symbol parameter, EqualsValueClauseSyntax defaultValueSyntax) {
        var binder = new LocalScopeBinder(
            WithAdditionalFlagsAndContainingMember(BinderFlags.ParameterDefaultValue, parameter.containingSymbol)
        );

        return new ExecutableCodeBinder(defaultValueSyntax, parameter.containingSymbol, binder);
    }

    internal BoundExpression BindConstructorInitializer(
        ArgumentListSyntax initializerArgumentList,
        MethodSymbol constructor,
        BelteDiagnosticQueue diagnostics) {
        Binder argumentListBinder = null;

        if (initializerArgumentList is not null)
            argumentListBinder = GetBinder(initializerArgumentList);

        var result = (argumentListBinder ?? this)
            .BindConstructorInitializerCore(initializerArgumentList, constructor, diagnostics);

        if (argumentListBinder is not null)
            result = argumentListBinder.WrapWithVariablesIfAny(initializerArgumentList, result);

        return result;
    }

    internal BoundEqualsValue BindParameterDefaultValue(
        EqualsValueClauseSyntax defaultValueSyntax,
        Symbol parameter,
        BelteDiagnosticQueue diagnostics,
        out BoundExpression valueBeforeConversion) {
        var defaultValueBinder = GetBinder(defaultValueSyntax);
        valueBeforeConversion = defaultValueBinder.BindValue(
            defaultValueSyntax.value,
            diagnostics,
            BindValueKind.RValue
        );

        var isTemplate = parameter is TemplateParameterSymbol;

        var parameterType = parameter is ParameterSymbol p
            ? p.type
            : (parameter as TemplateParameterSymbol).underlyingType.type;

        valueBeforeConversion = ReduceNumericIfApplicable(parameterType, valueBeforeConversion);

        var locals = defaultValueBinder.GetDeclaredLocalsForScope(defaultValueSyntax);
        var value = defaultValueBinder.GenerateConversionForAssignment(
            parameterType,
            valueBeforeConversion,
            diagnostics,
            ConversionForAssignmentFlags.DefaultParameter
        );

        if (isTemplate) {
            return new BoundTemplateParameterEqualsValue(
                defaultValueSyntax,
                (TemplateParameterSymbol)parameter,
                locals,
                value
            );
        } else {
            return new BoundParameterEqualsValue(
                defaultValueSyntax,
                (ParameterSymbol)parameter,
                locals,
                value
            );
        }
    }

    internal BoundFieldEqualsValue BindFieldInitializer(
        FieldSymbol field,
        EqualsValueClauseSyntax initializer,
        BelteDiagnosticQueue diagnostics) {
        if (initializer is null)
            return null;

        var initializerBinder = GetBinder(initializer);
        var result = initializerBinder.BindVariableOrAutoPropInitializerValue(
            initializer,
            field.refKind,
            field.GetFieldType(initializerBinder.fieldsBeingBound).type,
            diagnostics
        );

        return new BoundFieldEqualsValue(
            initializer,
            field,
            initializerBinder.GetDeclaredLocalsForScope(initializer),
            result
        );
    }

    internal BoundFieldEqualsValue BindEnumConstantInitializer(
        SourceEnumConstantSymbol symbol,
        EqualsValueClauseSyntax equalsValueSyntax,
        BelteDiagnosticQueue diagnostics) {
        var initializerBinder = GetBinder(equalsValueSyntax);
        var initializer = initializerBinder.BindValue(equalsValueSyntax.value, diagnostics, BindValueKind.RValue);
        initializer = ReduceNumericIfApplicable(symbol.containingType.enumUnderlyingType, initializer);
        initializer = initializerBinder.GenerateConversionForAssignment(
            symbol.containingType.enumUnderlyingType,
            initializer,
            diagnostics
        );

        return new BoundFieldEqualsValue(
            equalsValueSyntax,
            symbol,
            initializerBinder.GetDeclaredLocalsForScope(equalsValueSyntax),
            initializer
        );
    }

    internal BoundExpression BindVariableOrAutoPropInitializerValue(
        EqualsValueClauseSyntax initializerOpt,
        RefKind refKind,
        TypeSymbol varType,
        BelteDiagnosticQueue diagnostics) {
        if (initializerOpt is null)
            return null;

        IsInitializerRefKindValid(
            initializerOpt,
            initializerOpt,
            refKind,
            diagnostics,
            out var valueKind,
            out var value
        );

        var initializer = BindPossibleArrayInitializer(value, varType, valueKind, diagnostics);
        initializer = ReduceNumericIfApplicable(varType, initializer);
        initializer = GenerateConversionForAssignment(varType, initializer, diagnostics);
        return initializer;
    }

    internal static BoundCallExpression GenerateBaseParameterlessConstructorInitializer(
        MethodSymbol constructor,
        BelteDiagnosticQueue diagnostics) {
        var containingType = constructor.containingType;
        var baseType = containingType.baseType;
        MethodSymbol baseConstructor = null;
        var resultKind = LookupResultKind.Viable;

        foreach (var ctor in baseType.instanceConstructors) {
            if (ctor.parameterCount == 0) {
                baseConstructor = ctor;
                break;
            }
        }

        var hasErrors = false;

        if (!AccessCheck.IsSymbolAccessible(baseConstructor, containingType)) {
            diagnostics.Push(Error.MemberIsInaccessible(constructor.location, baseConstructor));
            resultKind = LookupResultKind.Inaccessible;
            hasErrors = true;
        }

        var syntax = constructor.GetNonNullSyntaxNode();
        var receiver = new BoundThisExpression(syntax, containingType);
        return new BoundCallExpression(
            syntax,
            receiver,
            baseConstructor,
            [],
            [],
            BitVector.Empty,
            resultKind,
            baseConstructor.returnType,
            hasErrors
        );
    }

    private static bool IsInitializerRefKindValid(
        EqualsValueClauseSyntax initializer,
        BelteSyntaxNode node,
        RefKind variableRefKind,
        BelteDiagnosticQueue diagnostics,
        out BindValueKind valueKind,
        out ExpressionSyntax value) {
        var expressionRefKind = RefKind.None;
        value = initializer?.value.UnwrapRefExpression(out expressionRefKind);

        if (variableRefKind == RefKind.None) {
            valueKind = BindValueKind.RValue;
            if (expressionRefKind == RefKind.Ref) {
                diagnostics.Push(Error.InitializeByValueWithByReference(node.location));
                return false;
            }
        } else {
            valueKind = variableRefKind is RefKind.RefConst or RefKind.RefFinal
                ? BindValueKind.RefConst
                : BindValueKind.RefOrOut;

            if (initializer is null) {
                // Error(diagnostics, ErrorCode.ERR_ByReferenceVariableMustBeInitialized, node);
                // return false
                // TODO should we error here?
                // We need to consider if `ref int? y;` creating a temporary is valuable or not
                return true;
            } else if (expressionRefKind != RefKind.Ref) {
                diagnostics.Push(Error.InitializeByReferenceWithByValue(node.location));
                return false;
            }
        }

        return true;
    }

    private BoundExpression BindPossibleArrayInitializer(
        ExpressionSyntax node,
        TypeSymbol destinationType,
        BindValueKind valueKind,
        BelteDiagnosticQueue diagnostics) {
        if (node.kind != SyntaxKind.InitializerListExpression)
            return BindValue(node, diagnostics, valueKind);

        BoundExpression result;

        var strippedType = destinationType.StrippedType();
        var fatArray = CorLibrary.GetWellKnownType(WellKnownType.Array);

        if (strippedType.kind == SymbolKind.ArrayType || strippedType.originalDefinition.Equals(fatArray)) {
            result = BindArrayCreationWithInitializer(
                diagnostics,
                null,
                (InitializerListExpressionSyntax)node,
                strippedType,
                []
            );
        } else {
            // TODO Would be nice if lists could accept empty/non-inferred array initializers
            // diagnostics.Push(Error.ArrayInitToNonArrayType(node.location));
            result = BindUnexpectedArrayInitializer((InitializerListExpressionSyntax)node, diagnostics, true);
        }

        return CheckValue(result, valueKind, diagnostics);
    }

    private BoundExpression BindInitializerDictionaryExpression(
        InitializerDictionaryExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        TypeSymbol foundKeyType = null;
        TypeSymbol foundValueType = null;
        var failed = false;

        var builder = ArrayBuilder<(BoundExpression, BoundExpression)>.GetInstance();

        foreach (var item in node.items) {
            var boundKey = BindValue(item.key, diagnostics, BindValueKind.RValue);

            if (foundKeyType is null) {
                foundKeyType = boundKey.Type();
            } else {
                if (!boundKey.Type().Equals(foundKeyType))
                    failed = true;
            }

            var boundValue = BindValue(item.value, diagnostics, BindValueKind.RValue);

            if (foundValueType is null) {
                foundValueType = boundValue.Type();
            } else {
                if (!boundValue.Type().Equals(foundValueType))
                    failed = true;
            }

            builder.Add((boundKey, boundValue));
        }

        if (!failed) {
            for (var i = 0; i < builder.Count; i++) {
                var castedKey = GenerateConversionForAssignment(foundKeyType, builder[i].Item1, diagnostics);
                var castedValue = GenerateConversionForAssignment(foundValueType, builder[i].Item2, diagnostics);
                builder[i] = (castedKey, castedValue);
            }
        }

        var dictType = new TypeWithAnnotations(CorLibrary.GetWellKnownType(WellKnownType.Dictionary)
            .Construct([new TypeOrConstant(foundKeyType), new TypeOrConstant(foundValueType)]))
            .SetIsAnnotated().type;

        if (failed) {
            diagnostics.Push(Error.InvalidInitializerDictionary(node.location));

            return new BoundInitializerDictionary(
                node,
                builder.ToImmutableAndFree(),
                dictType,
                hasErrors: true
            );
        }

        return new BoundInitializerDictionary(
            node,
            builder.ToImmutableAndFree(),
            dictType
        );
    }

    private BoundExpression BindUnexpectedArrayInitializer(
        InitializerListExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        bool inferType,
        bool shouldLiftIfPossible = false) {
        var result = BindArrayInitializerList(
            diagnostics,
            node,
            CreateArrayTypeSymbol(CorLibrary.GetNullableType(SpecialType.Any)),
            new long?[1],
            1,
            false
        );

        if (inferType)
            return InferTypeOfArrayInitializer(result, diagnostics, shouldLiftIfPossible);

        if (!result.hasAnyErrors && !inferType) {
            result = new BoundInitializerList(
                node,
                result.items,
                result.Type(),
                hasErrors: true
            );
        }

        return result;
    }

    private BoundExpression InferTypeOfArrayInitializer(
        BoundInitializerList expression,
        BelteDiagnosticQueue diagnostics,
        bool shouldLiftIfPossible) {
        var shouldLift = true;
        TypeSymbol foundElementType = null;
        TypeWithAnnotations foundTypeWithAnnotations;

        foreach (var item in expression.items) {
            var operand = item is BoundCastExpression c ? c.operand : item;

            if (foundElementType is null) {
                foundElementType = operand.Type();

                if (operand.kind != BoundKind.LiteralExpression)
                    shouldLift = false;

                continue;
            }

            if (!operand.Type().Equals(foundElementType)) {
                foundElementType = null;
                break;
            }
        }

        if (foundElementType is null) {
            if (!expression.hasErrors) {
                diagnostics.Push(Error.UnexpectedArrayInit(expression.syntax.location));

                expression = new BoundInitializerList(
                    expression.syntax,
                    expression.items,
                    expression.Type(),
                    hasErrors: true
                );
            }

            return expression;
        }

        foundTypeWithAnnotations = new TypeWithAnnotations(foundElementType);

        if (shouldLift && shouldLiftIfPossible && !foundElementType.IsNullableType())
            foundTypeWithAnnotations = foundTypeWithAnnotations.SetIsAnnotated();

        var builder = ArrayBuilder<BoundExpression>.GetInstance();

        foreach (var item in expression.items) {
            var operand = item is BoundCastExpression c ? c.operand : item;
            var casted = GenerateConversionForAssignment(foundTypeWithAnnotations.type, operand, diagnostics);
            builder.Add(casted);
        }

        TypeSymbol type = ArrayTypeSymbol.CreateSZArray(foundTypeWithAnnotations);

        expression = new BoundInitializerList(
            expression.syntax,
            builder.ToImmutableAndFree(),
            type
        );

        if (shouldLiftIfPossible)
            type = new TypeWithAnnotations(type).SetIsAnnotated().type;

        return new BoundArrayCreationExpression(
            expression.syntax,
            [BoundFactory.Literal(
                expression.syntax,
                Convert.ToInt64(expression.items.Length),
                CorLibrary.GetSpecialType(SpecialType.Int)
            )],
            expression,
            type
        );
    }

    internal static bool IsAnyReadOnly(AddressKind addressKind) => addressKind >= AddressKind.ReadOnly;

    internal static bool HasHome(
        BoundExpression expression,
        AddressKind addressKind,
        Symbol containingSymbol,
        HashSet<DataContainerSymbol> stackLocals) {
        switch (expression.kind) {
            case BoundKind.ArrayAccessExpression:
                if (addressKind == AddressKind.ReadOnly && !expression.Type().isValueType)
                    return false;

                return true;
            case BoundKind.ThisExpression:
                var type = expression.Type();

                if (type.isReferenceType)
                    return true;

                if (!IsAnyReadOnly(addressKind) && containingSymbol is
                    MethodSymbol { containingSymbol: NamedTypeSymbol, isEffectivelyConst: true }) {
                    return false;
                }

                return true;
            case BoundKind.ThrowExpression:
                return true;
            case BoundKind.ParameterExpression:
                return IsAnyReadOnly(addressKind) ||
                    ((BoundParameterExpression)expression).parameter.refKind is not RefKind.RefConst;
            case BoundKind.DataContainerExpression:
                var local = ((BoundDataContainerExpression)expression).dataContainer;

                return !((CodeGenerator.IsStackLocal(local, stackLocals) && local.refKind == RefKind.None) ||
                    (!IsAnyReadOnly(addressKind) && local.refKind is RefKind.RefConst or RefKind.RefFinal));
            case BoundKind.CallExpression:
                var methodRefKind = ((BoundCallExpression)expression).method.refKind;

                return methodRefKind == RefKind.Ref ||
                    (IsAnyReadOnly(addressKind) && methodRefKind is RefKind.RefConst or RefKind.RefFinal);
            case BoundKind.FieldAccessExpression:
                return FieldAccessHasHome(
                    (BoundFieldAccessExpression)expression,
                    addressKind,
                    containingSymbol,
                    stackLocals
                );
            case BoundKind.AssignmentOperator:
                var assignment = (BoundAssignmentOperator)expression;

                if (!assignment.isRef)
                    return false;

                var lhsRefKind = assignment.left.GetRefKind();
                return lhsRefKind == RefKind.Ref ||
                    (IsAnyReadOnly(addressKind) && lhsRefKind is RefKind.RefConst or RefKind.RefFinal);
            case BoundKind.ConditionalOperator:
                var conditional = (BoundConditionalOperator)expression;

                if (!conditional.isRef)
                    return false;

                return HasHome(conditional.trueExpression, addressKind, containingSymbol, stackLocals)
                    && HasHome(conditional.falseExpression, addressKind, containingSymbol, stackLocals);
            default:
                return false;
        }
    }

    private static bool FieldAccessHasHome(
        BoundFieldAccessExpression fieldAccess,
        AddressKind addressKind,
        Symbol containingSymbol,
        HashSet<DataContainerSymbol> stackLocalsOpt) {
        var field = fieldAccess.field;

        if (field.isConstExpr)
            return false;

        if (field.refKind is RefKind.Ref)
            return true;

        if (addressKind == AddressKind.ReadOnlyStrict)
            return true;

        // TODO Equiv?
        // if (fieldAccess.IsByValue) {
        //     return false;
        // }

        if (field.refKind is RefKind.RefConst or RefKind.RefFinal)
            return false;

        if (!field.isConst)
            return true;

        if (!TypeSymbol.Equals(
            field.containingType,
            containingSymbol.containingSymbol as NamedTypeSymbol,
            TypeCompareKind.AllIgnoreOptions)) {
            return false;
        }

        if (field.isStatic) {
            return containingSymbol is MethodSymbol { methodKind: MethodKind.StaticConstructor } or
                FieldSymbol { isStatic: true };
        } else {
            // ? or MethodSymbol { isInitOnly: true }
            return (containingSymbol is MethodSymbol { methodKind: MethodKind.Constructor }
                or FieldSymbol { isStatic: false }) &&
                fieldAccess.receiver.kind == BoundKind.ThisExpression;
        }
    }

    private BoundExpression CheckValue(
        BoundExpression expression,
        BindValueKind kind,
        BelteDiagnosticQueue diagnostics) {
        switch (expression.kind) {
            // TODO
            // case BoundKind.IndexerAccessExpression:
            // expression = BindIndexerDefaultArgumentsAndParamsCollection((BoundIndexerAccess)expression, valueKind, diagnostics);
            // break;

            // case BoundKind.ImplicitIndexerAccess: {
            //         var implicitIndexer = (BoundImplicitIndexerAccess)expression;
            //         if (implicitIndexer.IndexerOrSliceAccess is BoundIndexerAccess indexerAccess) {
            //             var kind = GetIndexerAccessorKind(indexerAccess, valueKind);
            //             expression = implicitIndexer.Update(
            //                 implicitIndexer.Receiver,
            //                 implicitIndexer.Argument,
            //                 implicitIndexer.LengthOrCountAccess,
            //                 implicitIndexer.ReceiverPlaceholder,
            //                 indexerAccess.Update(kind),
            //                 implicitIndexer.ArgumentPlaceholders,
            //                 implicitIndexer.Type);
            //         }
            //     }
            //     break;

            case BoundKind.UnconvertedInitializerList:
                if (kind == BindValueKind.RValue)
                    return expression;

                break;
            case BoundKind.PointerIndirectionOperator:
                if ((kind & BindValueKind.RefersToLocation) == BindValueKind.RefersToLocation) {
                    var pointerIndirection = (BoundPointerIndirectionOperator)expression;
                    expression = pointerIndirection.Update(
                        pointerIndirection.operand,
                        refersToLocation: true,
                        pointerIndirection.type
                    );
                }

                break;
            case BoundKind.PointerIndexAccessExpression:
                if ((kind & BindValueKind.RefersToLocation) == BindValueKind.RefersToLocation) {
                    var elementAccess = (BoundPointerIndexAccessExpression)expression;
                    expression = elementAccess.Update(
                        elementAccess.receiver,
                        elementAccess.index,
                        refersToLocation: true,
                        elementAccess.type
                    );
                }

                break;
            case BoundKind.UnconvertedObjectCreationExpression:
            case BoundKind.UnconvertedConditionalOperator:
                if (kind == BindValueKind.RValue)
                    return expression;

                break;
            case BoundKind.DiscardExpression:
                return expression;
        }

        var hasResolutionErrors = false;

        var underlyingExpression = expression is BoundConditionalAccessExpression c
            ? c.accessExpression
            : expression;

        if (underlyingExpression.kind == BoundKind.MethodGroup && kind == BindValueKind.AddressOf)
            return expression;

        if (underlyingExpression.kind == BoundKind.MethodGroup && kind != BindValueKind.RValueOrMethodGroup) {
            var methodGroup = (BoundMethodGroup)underlyingExpression;
            var resolution = ResolveMethodGroup(methodGroup, analyzedArguments: null);
            Symbol otherSymbol = null;
            var resolvedToMethodGroup = resolution.methodGroup is not null;

            if (!expression.hasAnyErrors)
                diagnostics.PushRange(resolution.diagnostics);

            hasResolutionErrors = resolution.hasAnyErrors;

            if (hasResolutionErrors)
                otherSymbol = resolution.otherSymbol;

            resolution.Free();

            if (!resolvedToMethodGroup) {
                var receiver = methodGroup.receiver;

                return new BoundErrorExpression(
                    expression.syntax,
                    methodGroup.resultKind,
                    otherSymbol is null ? [] : [otherSymbol],
                    receiver == null ? [] : [receiver],
                    GetNonMethodMemberType(otherSymbol),
                    true
                );
            }
        }

        if (!hasResolutionErrors && CheckValueKind(expression.syntax, expression, kind, false, diagnostics) ||
            expression.hasAnyErrors && kind == BindValueKind.RValueOrMethodGroup) {
            return expression;
        }

        var resultKind = (kind == BindValueKind.RValue || kind == BindValueKind.RValueOrMethodGroup)
            ? LookupResultKind.NotAValue
            : LookupResultKind.NotADataContainer;

        return ToErrorExpression(expression, resultKind);
    }

    private static bool RequiresRValueOnly(BindValueKind kind) {
        return (kind & ValueKindSignificantBitsMask) == BindValueKind.RValue;
    }

    private static bool RequiresReferenceToLocation(BindValueKind kind) {
        return (kind & BindValueKind.RefersToLocation) != 0;
    }

    private static bool RequiresRefAssignableVariable(BindValueKind kind) {
        return (kind & BindValueKind.RefAssignable) != 0;
    }

    private static bool RequiresAssignableVariable(BindValueKind kind) {
        return (kind & BindValueKind.Assignable) != 0;
    }

    private static bool RequiresVariable(BindValueKind kind) {
        return !RequiresRValueOnly(kind);
    }

    private static bool RequiresRefOrOut(BindValueKind kind) {
        return (kind & BindValueKind.RefOrOut) == BindValueKind.RefOrOut;
    }

    private static BelteDiagnostic GetStandardLValueError(BindValueKind kind, TextLocation location) {
        switch (kind) {
            case BindValueKind.CompoundAssignment:
            case BindValueKind.Assignable:
                return Error.AssignableLValueExpected(location);
            case BindValueKind.IncrementDecrement:
                return Error.IncrementableLValueExpected(location);
            case BindValueKind.FixedReceiver:
                return Error.FixedNeedsLValue(location);
            case BindValueKind.RefReturn:
            case BindValueKind.RefConst:
                return Error.RefReturnLValueExpected(location);
            case BindValueKind.AddressOf:
                return Error.InvalidAddrOp(location);
            case BindValueKind.RefAssignable:
                return Error.RefLocalOrParameterExpected(location);
        }

        if (RequiresReferenceToLocation(kind))
            return Error.RefLValueExpected(location);

        throw ExceptionUtilities.UnexpectedValue(kind);
    }

    private static BelteDiagnostic GetThisLValueError(BindValueKind kind, bool isValueType, TextLocation location) {
        switch (kind) {
            case BindValueKind.CompoundAssignment:
            case BindValueKind.Assignable:
                return Error.ConstantAssignmentThis(location);
            case BindValueKind.RefOrOut:
                return Error.RefConstLocal(location);
            case BindValueKind.AddressOf:
                return Error.InvalidAddrOp(location);
            case BindValueKind.IncrementDecrement:
                return isValueType
                    ? Error.ConstantAssignmentThis(location)
                    : Error.IncrementableLValueExpected(location);
            case BindValueKind.RefReturn:
            case BindValueKind.RefConst:
                return Error.RefReturnThis(location);
            case BindValueKind.RefAssignable:
                return Error.RefLocalOrParameterExpected(location);
        }

        if (RequiresReferenceToLocation(kind))
            return Error.RefLValueExpected(location);

        throw ExceptionUtilities.UnexpectedValue(kind);
    }

    internal bool CheckValueKind(
        SyntaxNode node,
        BoundExpression expression,
        BindValueKind valueKind,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        if (expression.hasAnyErrors)
            return false;

        if (expression is BoundConditionalAccessExpression c)
            expression = c.accessExpression;

        if (RequiresRValueOnly(valueKind))
            return CheckNotNamespaceOrType(expression, diagnostics);

        if ((expression.constantValue is not null) || (expression.type.GetSpecialTypeSafe() == SpecialType.Void)) {
            diagnostics.Push(GetStandardLValueError(valueKind, node.location));
            return false;
        }

        switch (expression.kind) {
            case BoundKind.NamespaceExpression:
                var ns = (BoundNamespaceExpression)expression;

                diagnostics.Push(Error.BadSKKnown(
                    node.location,
                    ns.namespaceSymbol,
                    MessageID.IDS_SK_NAMESPACE.Localize(),
                    MessageID.IDS_SK_VARIABLE.Localize(
                )));

                return false;
            case BoundKind.TypeExpression:
                var type = (BoundTypeExpression)expression;

                diagnostics.Push(Error.BadSKKnown(
                    node.location,
                    type.type,
                    MessageID.IDS_SK_TYPE.Localize(),
                    MessageID.IDS_SK_VARIABLE.Localize(
                )));

                return false;
            case BoundKind.MethodGroup:
                var methodGroup = (BoundMethodGroup)expression;

                diagnostics.Push(GetMethodGroupLValueError(
                    valueKind,
                    node.location,
                    methodGroup.name,
                    MessageID.IDS_MethodGroup.Localize()
                ));

                return false;
            case BoundKind.CastExpression:
                break;
            case BoundKind.ParameterExpression:
                var parameter = (BoundParameterExpression)expression;
                return CheckParameterValueKind(node, parameter, valueKind, checkingReceiver, diagnostics);
            case BoundKind.DataContainerExpression:
                var local = (BoundDataContainerExpression)expression;
                return CheckLocalValueKind(node, local, valueKind, checkingReceiver, diagnostics);
            case BoundKind.UnconvertedAddressOfOperator:
                var unconvertedAddressOf = (BoundUnconvertedAddressOfOperator)expression;
                diagnostics.Push(GetMethodGroupOrFunctionPointerLvalueError(
                    valueKind,
                    node,
                    unconvertedAddressOf.operand.name,
                    MessageID.IDS_AddressOfMethodGroup.Localize()
                ));

                return false;
            case BoundKind.FunctionPointerCallExpression:
                return CheckMethodReturnValueKind(((BoundFunctionPointerCallExpression)expression).functionPointer.signature,
                    expression.syntax,
                    node,
                    valueKind,
                    checkingReceiver,
                    diagnostics
                );
            case BoundKind.ThisExpression:
                if (checkingReceiver)
                    return true;

                if (RequiresRefAssignableVariable(valueKind)) {
                    diagnostics.Push(Error.RefLocalOrParameterExpected(node.location));
                    return false;
                }

                var isValueType = ((BoundThisExpression)expression).type.isValueType;

                if (!isValueType || (RequiresAssignableVariable(valueKind) &&
                    containingMember is MethodSymbol { isEffectivelyConst: true })) {
                    ReportThisLValueError(node, valueKind, isValueType, diagnostics);
                    return false;
                }

                return true;
            case BoundKind.CallExpression:
                var call = (BoundCallExpression)expression;

                return CheckMethodReturnValueKind(
                    call.method,
                    call.syntax,
                    node,
                    valueKind,
                    checkingReceiver,
                    diagnostics
                );
            case BoundKind.IndexerAccessExpression:
                var index = (BoundIndexerAccessExpression)expression;

                if (CorLibrary.TryGetWellKnownType(WellKnownType.Array, compilation)
                    .Equals(index.receiver.StrippedType().originalDefinition)) {
                    return true;
                }

                if (index.method is not null) {
                    return CheckMethodReturnValueKind(
                        index.method,
                        index.syntax,
                        index.syntax,
                        valueKind,
                        checkingReceiver,
                        diagnostics
                    );
                }

                break;
            case BoundKind.PointerIndexAccessExpression: {
                    if (RequiresRefAssignableVariable(valueKind)) {
                        diagnostics.Push(Error.RefLocalOrParameterExpected(node.location));
                        return false;
                    }

                    var receiver = ((BoundPointerIndexAccessExpression)expression).receiver;

                    if (receiver is BoundFieldAccessExpression fieldAccess && fieldAccess.field.isFixedSizeBuffer)
                        return CheckValueKind(node, fieldAccess.receiver, valueKind, checkingReceiver: true, diagnostics);

                    return true;
                }
            case BoundKind.ConditionalOperator:
                if (RequiresRefAssignableVariable(valueKind)) {
                    diagnostics.Push(Error.RefLocalOrParameterExpected(node.location));
                    return false;
                }

                var conditional = (BoundConditionalOperator)expression;

                if (conditional.isRef &&
                    (CheckValueKind(
                        conditional.trueExpression.syntax,
                        conditional.trueExpression,
                        valueKind,
                        checkingReceiver: false,
                        diagnostics: diagnostics) &
                    CheckValueKind(
                        conditional.falseExpression.syntax,
                        conditional.falseExpression,
                        valueKind,
                        checkingReceiver: false,
                        diagnostics: diagnostics))) {
                    return true;
                }

                break;
            case BoundKind.FieldAccessExpression: {
                    var fieldAccess = (BoundFieldAccessExpression)expression;
                    return CheckFieldValueKind(node, fieldAccess, valueKind, checkingReceiver, diagnostics);
                }
            case BoundKind.AssignmentOperator:
                if (RequiresRefAssignableVariable(valueKind)) {
                    diagnostics.Push(Error.RefLocalOrParameterExpected(node.location));
                    return false;
                }

                var assignment = (BoundAssignmentOperator)expression;
                return CheckSimpleAssignmentValueKind(node, assignment, valueKind, diagnostics);
            case BoundKind.ArrayAccessExpression:
                return CheckArrayAccessValueKind(
                    node,
                    valueKind,
                    (BoundArrayAccessExpression)expression,
                    checkingReceiver,
                    diagnostics
                );
            case BoundKind.ValuePlaceholder:
                break;
            case BoundKind.PointerIndirectionOperator:
                if (RequiresRefAssignableVariable(valueKind)) {
                    diagnostics.Push(Error.RefLocalOrParameterExpected(node.location));
                    return false;
                }

                return true;
            case BoundKind.ObjectCreationExpression:
                if (node.parent.kind == SyntaxKind.CascadeExpression)
                    return true;

                break;
            case BoundKind.NullAssertOperator:
                return CheckValueKind(
                    node,
                    ((BoundNullAssertOperator)expression).operand,
                    valueKind,
                    checkingReceiver,
                    diagnostics
                );
        }

        diagnostics.Push(GetStandardLValueError(valueKind, node.location));
        return false;
    }

    private static BelteDiagnostic GetMethodGroupOrFunctionPointerLvalueError(
        BindValueKind valueKind,
        SyntaxNode node,
        string name,
        string text) {
        if (RequiresReferenceToLocation(valueKind))
            return Error.RefConstantLocalCause(node.location, name, text);

        return Error.AssignmentConstantLocalCause(node.location, name, text);
    }

    private bool CheckArrayAccessValueKind(
        SyntaxNode node,
        BindValueKind valueKind,
        BoundArrayAccessExpression arrayAccess,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        if (RequiresRefAssignableVariable(valueKind)) {
            diagnostics.Push(Error.RefLocalOrParameterExpected(node.location));
            return false;
        }

        return CheckIsValidReceiverForVariable(node, arrayAccess.receiver, valueKind, diagnostics);
    }

    private static BelteDiagnostic GetMethodGroupLValueError(
        BindValueKind valueKind,
        TextLocation location,
        string name,
        string kind) {
        if (RequiresReferenceToLocation(valueKind))
            return Error.RefConstantLocalCause(location, name, kind);

        return Error.AssignmentConstantLocalCause(location, name, kind);
    }

    private bool CheckParameterValueKind(
        SyntaxNode node,
        BoundParameterExpression parameter,
        BindValueKind valueKind,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        var parameterSymbol = parameter.parameter;

        if (RequiresAssignableVariable(valueKind)) {
            if (parameterSymbol.refKind == RefKind.RefConst) {
                ReportConstantError(parameterSymbol, node, valueKind, checkingReceiver, diagnostics);
                return false;
            } else if (parameterSymbol.refKind == RefKind.RefFinal || parameterSymbol.isConst) {
                // TODO Why are we calling GetStandardLValueError here but ReportConstantError above?
                // TODO CheckLocalValueKind combines both into GetStandardLValueError
                if (!checkingReceiver || parameterSymbol.refKind != RefKind.RefFinal) {
                    diagnostics.Push(GetStandardLValueError(valueKind, node.location));
                    return false;
                }
            }
        } else if (RequiresRefAssignableVariable(valueKind)) {
            if (parameterSymbol.refKind == RefKind.None) {
                diagnostics.Push(Error.RefLocalOrParameterExpected(node.location));
                return false;
            } else if (parameterSymbol.isConst) {
                diagnostics.Push(GetStandardLValueError(valueKind, node.location));
                return false;
            }
        }

        return true;
    }

    private static void ReportThisLValueError(
        SyntaxNode node,
        BindValueKind valueKind,
        bool isValueType,
        BelteDiagnosticQueue diagnostics) {
        diagnostics.Push(GetThisLValueError(valueKind, isValueType, node.location));
    }

    private bool CheckLocalValueKind(
        SyntaxNode node,
        BoundDataContainerExpression local,
        BindValueKind valueKind,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        var localSymbol = local.dataContainer;

        if (RequiresAssignableVariable(valueKind)) {
            if (localSymbol.refKind is RefKind.RefConst or RefKind.RefFinal ||
                (localSymbol.refKind == RefKind.None && !localSymbol.isWritableVariable)) {
                if (!checkingReceiver || (!localSymbol.isFinal && localSymbol.refKind != RefKind.RefFinal)) {
                    diagnostics.Push(GetStandardLValueError(valueKind, node.location));
                    return false;
                }

                if (checkingReceiver)
                    ReportTransientForEachAssignment(localSymbol);
            }
        } else if (RequiresRefAssignableVariable(valueKind)) {
            if (localSymbol.refKind == RefKind.None) {
                diagnostics.Push(Error.RefLocalOrParameterExpected(node.location));
                return false;
            } else if (!localSymbol.isWritableVariable) {
                diagnostics.Push(GetStandardLValueError(valueKind, node.location));
                return false;
            }
        }

        return true;

        void ReportTransientForEachAssignment(DataContainerSymbol symbol) {
            if (symbol.type.IsStructType() && symbol.declarationKind == DataContainerDeclarationKind.ForEachLocal)
                diagnostics.Push(Warning.TransientForEachAssignment(node.location));
        }
    }

    private protected bool CheckMethodReturnValueKind(
        MethodSymbol methodSymbol,
        SyntaxNode callSyntax,
        SyntaxNode node,
        BindValueKind valueKind,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        if (RequiresVariable(valueKind) && methodSymbol.refKind == RefKind.None) {
            if (checkingReceiver)
                diagnostics.Push(Error.ReturnNotLValue(callSyntax.location, methodSymbol));
            else
                diagnostics.Push(GetStandardLValueError(valueKind, node.location));

            return false;
        }

        if (RequiresAssignableVariable(valueKind) && methodSymbol.refKind == RefKind.RefConst) {
            ReportConstantError(methodSymbol, node, valueKind, checkingReceiver, diagnostics);
            return false;
        }

        if (RequiresRefAssignableVariable(valueKind)) {
            diagnostics.Push(Error.RefLocalOrParameterExpected(node.location));
            return false;
        }

        return true;
    }

    private static void ReportConstantError(
        Symbol symbol,
        SyntaxNode node,
        BindValueKind kind,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        var symbolKind = symbol.kind.Localize();
        var index = (checkingReceiver ? 3 : 0) +
            (kind == BindValueKind.RefReturn ? 0 : (RequiresRefOrOut(kind) ? 1 : 2));

        diagnostics.Push(index switch {
            0 => Error.RefReturnConstNotField(node.location, symbolKind, symbol),
            1 => Error.RefConstNotField(node.location, symbolKind, symbol),
            2 => Error.ConstantAssignmentNotField(node.location, symbolKind, symbol),
            3 => Error.RefReturnConstNotField2(node.location, symbolKind, symbol),
            4 => Error.RefConstNotField2(node.location, symbolKind, symbol),
            5 => Error.ConstantAssignmentNotField2(node.location, symbolKind, symbol),
            _ => throw ExceptionUtilities.Unreachable()
        });
    }

    private static void ReportConstantFieldError(
        FieldSymbol field,
        SyntaxNode node,
        BindValueKind kind,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        var index = (checkingReceiver ? 6 : 0) +
            (field.isStatic ? 3 : 0) +
            (kind == BindValueKind.RefReturn ? 0 : (RequiresRefOrOut(kind) ? 1 : 2));

        diagnostics.Push(index switch {
            0 => Error.RefReturnConstant(node.location),
            1 => Error.RefConstant(node.location),
            2 => Error.AssignmentConstantField(node.location),
            3 => Error.RefReturnConstantStatic(node.location),
            4 => Error.RefConstantStatic(node.location),
            5 => Error.AssignmentConstantStatic(node.location),
            6 => Error.RefReturnConstant2(node.location, field),
            7 => Error.RefConstant2(node.location, field),
            8 => Error.AssignmentConstantField2(node.location, field),
            9 => Error.RefReturnConstantStatic2(node.location, field),
            10 => Error.RefConstantStatic2(node.location, field),
            11 => Error.AssignmentConstantStatic2(node.location, field),
            _ => throw ExceptionUtilities.Unreachable()
        });
    }

    private bool CheckSimpleAssignmentValueKind(
        SyntaxNode node,
        BoundAssignmentOperator assignment,
        BindValueKind valueKind,
        BelteDiagnosticQueue diagnostics) {
        if (assignment.isRef)
            return CheckValueKind(node, assignment.left, valueKind, checkingReceiver: false, diagnostics);

        diagnostics.Push(GetStandardLValueError(valueKind, node.location));
        return false;
    }

    private bool CheckFieldValueKind(
        SyntaxNode node,
        BoundFieldAccessExpression fieldAccess,
        BindValueKind valueKind,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        var fieldSymbol = fieldAccess.field;

        if (fieldSymbol.isConst) {
            if ((fieldSymbol.refKind == RefKind.None
                ? RequiresAssignableVariable(valueKind)
                : RequiresRefAssignableVariable(valueKind)) &&
                !CanModifyReadonlyField(fieldAccess.receiver is BoundThisExpression, fieldSymbol)) {
                ReportConstantFieldError(fieldSymbol, node, valueKind, checkingReceiver, diagnostics);
                return false;
            }
        }

        if (flags.Includes(BinderFlags.ConstContext) && IsThisInstanceAccess(fieldAccess)) {
            diagnostics.Push(Error.AssignmentInConstMethod(node.location));
            return false;
        }

        if (RequiresAssignableVariable(valueKind)) {
            switch (fieldSymbol.refKind) {
                case RefKind.None:
                    break;
                case RefKind.Ref:
                    return true;
                case RefKind.RefConst:
                case RefKind.RefFinal:
                    ReportConstantError(fieldSymbol, node, valueKind, checkingReceiver, diagnostics);
                    return false;
                default:
                    throw ExceptionUtilities.UnexpectedValue(fieldSymbol.refKind);
            }

            if (fieldSymbol.isFixedSizeBuffer) {
                diagnostics.Push(GetStandardLValueError(valueKind, node.location));
                return false;
            }

            if (fieldAccess.receiver is not null &&
                (fieldAccess.receiver.type.IsNullableType() ||
                 fieldAccess.receiver.kind == BoundKind.NullAssertOperator) &&
                fieldAccess.receiver.type.StrippedType().IsStructType()) {
                diagnostics.Push(GetStandardLValueError(valueKind, node.location));
                return false;
            }
        }

        if (RequiresRefAssignableVariable(valueKind)) {
            switch (fieldSymbol.refKind) {
                case RefKind.None:
                    diagnostics.Push(Error.RefLocalOrParameterExpected(node.location));
                    return false;
                case RefKind.Ref:
                case RefKind.RefConst:
                case RefKind.RefFinal:
                    return CheckIsValidReceiverForVariable(
                        node,
                        fieldAccess.receiver,
                        BindValueKind.Assignable,
                        diagnostics
                    );
                default:
                    throw ExceptionUtilities.UnexpectedValue(fieldSymbol.refKind);
            }
        }

        if (RequiresReferenceToLocation(valueKind)) {
            switch (fieldSymbol.refKind) {
                case RefKind.None:
                    break;
                case RefKind.Ref:
                case RefKind.RefConst:
                case RefKind.RefFinal:
                    return true;
                default:
                    throw ExceptionUtilities.UnexpectedValue(fieldSymbol.refKind);
            }
        }

        if (fieldSymbol.isStatic ||
            (fieldSymbol.containingType.isReferenceType && node.parent.kind == SyntaxKind.CascadeExpression)) {
            return true;
        }

        return CheckIsValidReceiverForVariable(node, fieldAccess.receiver, valueKind, diagnostics);
    }

    internal static bool IsThisInstanceAccess(BoundExpression expression) {
        var left = expression;

        while (left is not null) {
            if (left.kind == BoundKind.ThisExpression)
                return true;
            else if (left is BoundFieldAccessExpression nested)
                left = nested.receiver;
            // We only check for this because some post-lowering passes ask this question
            else if (left is BoundFieldSlotExpression slot)
                left = slot.receiver;
            else
                break;
        }

        return false;
    }

    private bool CheckIsValidReceiverForVariable(
        SyntaxNode node,
        BoundExpression receiver,
        BindValueKind kind,
        BelteDiagnosticQueue diagnostics) {
        return CheckValueKind(node, receiver, kind, true, diagnostics);
    }

    private bool CanModifyReadonlyField(bool receiverIsThis, FieldSymbol fieldSymbol) {
        var fieldIsStatic = fieldSymbol.isStatic;
        var canModifyReadonly = false;
        var containing = containingMember;

        if (containing is not null &&
            fieldIsStatic == containing.isStatic &&
            (fieldIsStatic || receiverIsThis) &&
            (/* TODO Compilation.FeaturesStrict*/false
                ? TypeSymbol.Equals(fieldSymbol.containingType, containing.containingType, TypeCompareKind.AllIgnoreOptions)
                : true)) {
            if (containing.kind == SymbolKind.Method) {
                var containingMethod = (MethodSymbol)containing;
                var desiredMethodKind = fieldIsStatic ? MethodKind.StaticConstructor : MethodKind.Constructor;

                canModifyReadonly = (containingMethod.methodKind == desiredMethodKind) ||
                    IsAssignedFromInitOnlySetterOnThis(receiverIsThis);
            } else if (containing.kind == SymbolKind.Field) {
                canModifyReadonly = true;
            }
        }

        return canModifyReadonly;

        bool IsAssignedFromInitOnlySetterOnThis(bool receiverIsThis) {
            if (!receiverIsThis)
                return false;

            if (containingMember is not MethodSymbol method)
                return false;

            // TODO Is this a valid replacement?
            // return method.isInitOnly;
            return method.isEffectivelyConst;
        }
    }


    private BoundExpression BindConstructorInitializerCore(
        ArgumentListSyntax initializerArgumentList,
        MethodSymbol constructor,
        BelteDiagnosticQueue diagnostics) {
        var containingType = constructor.containingType;

        if ((containingType.typeKind is TypeKind.Struct or TypeKind.Enum) && initializerArgumentList is null)
            return null;

        var analyzedArguments = AnalyzedArguments.GetInstance();
        try {
            var constructorReturnType = constructor.returnType;

            if (initializerArgumentList is not null)
                BindArgumentsAndNames(initializerArgumentList, diagnostics, analyzedArguments);

            var initializerType = containingType;
            var isBaseConstructorInitializer = initializerArgumentList is null ||
                ((ConstructorInitializerSyntax)initializerArgumentList.parent).thisOrBaseKeyword.kind ==
                    SyntaxKind.BaseKeyword;

            if (isBaseConstructorInitializer) {
                initializerType = initializerType.baseType;

                if (initializerType is null || containingType.specialType == SpecialType.Object) {
                    if (initializerArgumentList is null) {
                        return null;
                    } else {
                        // ? This is an error with the standard library
                        throw ExceptionUtilities.Unreachable();
                    }
                }
            }

            BelteSyntaxNode nonNullSyntax;
            TextLocation errorLocation;
            bool enableCallerInfo;

            switch (initializerArgumentList?.parent) {
                case ConstructorInitializerSyntax initializerSyntax:
                    nonNullSyntax = initializerSyntax;
                    errorLocation = initializerSyntax.thisOrBaseKeyword.location;
                    enableCallerInfo = true;
                    break;
                default:
                    nonNullSyntax = constructor.GetNonNullSyntaxNode();
                    errorLocation = constructor.location;
                    enableCallerInfo = false;
                    break;
            }

            var found = TryPerformConstructorOverloadResolution(
                initializerType,
                analyzedArguments,
                WellKnownMemberNames.InstanceConstructorName,
                errorLocation,
                false,
                diagnostics,
                out var memberResolutionResult,
                out var candidateConstructors,
                allowProtectedConstructorsOfBaseType: true
            );

            return BindConstructorInitializerCoreContinued(
                found,
                initializerArgumentList,
                constructor,
                analyzedArguments,
                constructorReturnType,
                initializerType,
                isBaseConstructorInitializer,
                nonNullSyntax,
                errorLocation,
                enableCallerInfo,
                memberResolutionResult,
                candidateConstructors,
                diagnostics
            );
        } finally {
            analyzedArguments.Free();
        }
    }

    private BoundExpression BindConstructorInitializerCoreContinued(
        bool found,
        ArgumentListSyntax initializerArgumentListOpt,
        MethodSymbol constructor,
        AnalyzedArguments analyzedArguments,
        TypeSymbol constructorReturnType,
        NamedTypeSymbol initializerType,
        bool isBaseConstructorInitializer,
        BelteSyntaxNode nonNullSyntax,
        TextLocation errorLocation,
        bool enableCallerInfo,
        MemberResolutionResult<MethodSymbol> memberResolutionResult,
        ImmutableArray<MethodSymbol> candidateConstructors,
        BelteDiagnosticQueue diagnostics) {
        ImmutableArray<int> argsToParams;

        if (memberResolutionResult.isNotNull) {
            CheckAndCoerceArguments(
                nonNullSyntax,
                memberResolutionResult,
                analyzedArguments,
                diagnostics,
                receiver: null,
                out argsToParams
            );
        } else {
            argsToParams = memberResolutionResult.result.argsToParams;
        }

        var resultMember = memberResolutionResult.member;
        BoundExpression receiver = new BoundThisExpression(nonNullSyntax, initializerType);

        if (found) {
            var hasErrors = false;

            if (resultMember == constructor) {
                diagnostics.Push(Error.RecursiveConstructorCall(errorLocation, constructor));
                hasErrors = true;
            }

            BindDefaultArguments(
                nonNullSyntax,
                resultMember.parameters,
                analyzedArguments.arguments,
                analyzedArguments.refKinds,
                analyzedArguments.names,
                ref argsToParams,
                out var defaultArguments,
                enableCallerInfo,
                diagnostics
            );

            (var args, var argRefKinds) = RearrangeArguments(
                analyzedArguments.arguments,
                analyzedArguments.refKinds,
                argsToParams
            );

            return new BoundCallExpression(
                nonNullSyntax,
                receiver,
                // TODO Potentially useful to keep track of
                // initialBindingReceiverIsSubjectToCloning: ReceiverIsSubjectToCloning(receiver, resultMember),
                resultMember,
                args.Select(a => a.expression).ToImmutableArray(),
                argRefKinds,
                defaultArguments: defaultArguments,
                resultKind: LookupResultKind.Viable,
                type: constructorReturnType,
                hasErrors: hasErrors
            );
        } else {
            var result = CreateErrorCall(
                node: nonNullSyntax,
                name: WellKnownMemberNames.InstanceConstructorName,
                receiver: receiver,
                methods: candidateConstructors,
                resultKind: LookupResultKind.OverloadResolutionFailure,
                templateArguments: [],
                analyzedArguments: analyzedArguments
            );

            return result;
        }
    }

    private BoundExpression BindExpressionInternal(
        ExpressionSyntax node,
        BelteDiagnosticQueue diagnostics,
        bool called,
        bool indexed) {
        switch (node.kind) {
            case SyntaxKind.LiteralExpression:
                return BindLiteralExpression((LiteralExpressionSyntax)node, diagnostics);
            case SyntaxKind.DefaultLiteralExpression:
                return BindDefaultLiteralExpression((DefaultLiteralExpressionSyntax)node, diagnostics);
            case SyntaxKind.DefaultExpression:
                return BindDefaultExpression((DefaultExpressionSyntax)node, diagnostics);
            case SyntaxKind.ExtendedLiteralExpression:
                return BindExtendedLiteralExpression((ExtendedLiteralExpressionSyntax)node, diagnostics);
            case SyntaxKind.TupleExpression:
                return BindTupleExpression((TupleExpressionSyntax)node, diagnostics);
            case SyntaxKind.ThisExpression:
                return BindThisExpression((ThisExpressionSyntax)node, diagnostics);
            case SyntaxKind.BaseExpression:
                return BindBaseExpression((BaseExpressionSyntax)node, diagnostics);
            case SyntaxKind.CallExpression:
                return BindCallExpression((CallExpressionSyntax)node, diagnostics);
            case SyntaxKind.QualifiedName:
                return BindQualifiedName((QualifiedNameSyntax)node, diagnostics);
            case SyntaxKind.ReferenceType:
                return BindReferenceType((ReferenceTypeSyntax)node, diagnostics);
            case SyntaxKind.NonNullableType:
                return ErrorExpression(node);
            case SyntaxKind.ParenthesizedExpression:
                return BindParenthesisExpression((ParenthesisExpressionSyntax)node, diagnostics);
            case SyntaxKind.MemberAccessExpression:
                return BindMemberAccess((MemberAccessExpressionSyntax)node, called, indexed, diagnostics);
            case SyntaxKind.IdentifierName:
            case SyntaxKind.TemplateName:
                return BindIdentifier((SimpleNameSyntax)node, called, indexed, diagnostics);
            case SyntaxKind.AliasQualifiedName:
                return BindNamespaceOrType(node, diagnostics);
            case SyntaxKind.BinaryExpression:
                return BindBinaryExpression((BinaryExpressionSyntax)node, diagnostics);
            case SyntaxKind.IsPatternExpression:
                return BindIsPatternExpression((IsPatternExpressionSyntax)node, diagnostics);
            case SyntaxKind.UnaryExpression:
                return BindUnaryExpression((UnaryExpressionSyntax)node, diagnostics);
            case SyntaxKind.PrefixExpression:
                return BindIncrementOperator(node, ((PrefixExpressionSyntax)node).operand, ((PrefixExpressionSyntax)node).operatorToken, diagnostics);
            case SyntaxKind.PostfixExpression:
                return BindIncrementOrNullAssertOperator((PostfixExpressionSyntax)node, diagnostics);
            case SyntaxKind.TernaryExpression:
                return BindTernaryExpression((TernaryExpressionSyntax)node, diagnostics);
            case SyntaxKind.ClampExpression:
                return BindClampExpression((ClampExpressionSyntax)node, diagnostics);
            case SyntaxKind.AssignmentExpression:
                return BindAssignmentOperator((AssignmentExpressionSyntax)node, diagnostics);
            case SyntaxKind.ObjectCreationExpression:
                return BindObjectCreationExpression((ObjectCreationExpressionSyntax)node, diagnostics);
            case SyntaxKind.ArrayCreationExpression:
                return BindArrayCreationExpression((ArrayCreationExpressionSyntax)node, diagnostics);
            case SyntaxKind.NameOfExpression:
                return BindNameOfExpression((NameOfExpressionSyntax)node, diagnostics);
            case SyntaxKind.SizeOfExpression:
                return BindSizeOfExpression((SizeOfExpressionSyntax)node, diagnostics);
            case SyntaxKind.CastExpression:
                return BindCastExpression((CastExpressionSyntax)node, diagnostics);
            case SyntaxKind.BitCastExpression:
                return BindBitCastExpression((BitCastExpressionSyntax)node, diagnostics);
            case SyntaxKind.InitializerListExpression:
                return BindUnexpectedArrayInitializer((InitializerListExpressionSyntax)node, diagnostics, true);
            case SyntaxKind.InitializerDictionaryExpression:
                return BindInitializerDictionaryExpression((InitializerDictionaryExpressionSyntax)node, diagnostics);
            case SyntaxKind.ReferenceExpression:
                return BindReferenceExpression((ReferenceExpressionSyntax)node, diagnostics);
            case SyntaxKind.IndexExpression:
                return BindIndexExpression((IndexExpressionSyntax)node, diagnostics);
            case SyntaxKind.TypeOfExpression:
                return BindTypeOfExpression((TypeOfExpressionSyntax)node, diagnostics);
            case SyntaxKind.ThrowExpression:
                return BindThrowExpression((ThrowExpressionSyntax)node, diagnostics);
            case SyntaxKind.CascadeListExpression:
                return BindCascadeListExpression((CascadeListExpressionSyntax)node, diagnostics);
            case SyntaxKind.StackAllocExpression:
                return BindStackAllocExpression((StackAllocExpressionSyntax)node, diagnostics);
            case SyntaxKind.ImplicitEnumFieldExpression:
                return BindImplicitEnumFieldExpression((ImplicitEnumFieldExpressionSyntax)node, diagnostics);
            case SyntaxKind.InterpolatedStringExpression:
                return BindInterpolatedString((InterpolatedStringExpressionSyntax)node, diagnostics);
            case SyntaxKind.ParenthesizedLambdaExpression:
            case SyntaxKind.SimpleLambdaExpression:
                return BindAnonymousFunction((AnonymousFunctionExpressionSyntax)node, diagnostics);
            case SyntaxKind.WithExpression:
                return BindWithExpression((WithExpressionSyntax)node, diagnostics);
            case SyntaxKind.ReversibleExpression:
                return BindReversibleExpression((ReversibleExpressionSyntax)node, diagnostics);
            default:
                throw ExceptionUtilities.UnexpectedValue(node.kind);
        }
    }

    private BoundErrorExpression ErrorExpression(SyntaxNode syntax, BoundExpression expression) {
        return new BoundErrorExpression(syntax, LookupResultKind.Empty, [], [expression], CreateErrorType(), true);
    }

    private BoundErrorExpression ErrorExpression(
        SyntaxNode syntax,
        ImmutableArray<BoundExpression> childNodes) {
        return ErrorExpression(syntax, LookupResultKind.Empty, ImmutableArray<Symbol>.Empty, childNodes);
    }

    private BoundErrorExpression ErrorExpression(
        SyntaxNode syntax,
        LookupResultKind lookupResultKind,
        BoundExpression expression) {
        return new BoundErrorExpression(syntax, lookupResultKind, [], [expression], CreateErrorType(), true);
    }

    private BoundErrorExpression ErrorExpression(
        SyntaxNode syntax,
        LookupResultKind resultKind,
        ImmutableArray<Symbol> symbols,
        ImmutableArray<BoundExpression> childNodes) {
        return new BoundErrorExpression(
            syntax,
            resultKind,
            symbols,
            childNodes.SelectAsArray((e, self) => self.BindToTypeForErrorRecovery(e), this),
            CreateErrorType(),
            true
        );
    }

    private BoundErrorExpression ErrorExpression(SyntaxNode syntax) {
        return new BoundErrorExpression(syntax, LookupResultKind.Empty, [], [], CreateErrorType(), true);
    }

    private BoundExpression ErrorIndexerExpression(
        SyntaxNode node,
        BoundExpression expression,
        AnalyzedArguments analyzedArguments,
        BelteDiagnostic error,
        BelteDiagnosticQueue diagnostics) {
        if (!expression.hasAnyErrors)
            diagnostics.Push(error);

        var childBoundNodes = BuildArgumentsForErrorRecovery(analyzedArguments).Add(expression);

        return new BoundErrorExpression(
            node,
            LookupResultKind.Empty,
            [],
            childBoundNodes,
            CreateErrorType(),
            hasErrors: true
        );
    }

    private BoundExpression ToErrorExpression(
        BoundExpression expression,
        LookupResultKind resultKind = LookupResultKind.Empty) {
        var resultType = expression.Type();
        var expressionKind = expression.kind;

        if (expression.hasAnyErrors && resultType is not null ||
            expressionKind is BoundKind.DefaultLiteral or BoundKind.UnboundLambda) {
            return expression;
        }

        if (expressionKind == BoundKind.ErrorExpression) {
            var errorExpression = (BoundErrorExpression)expression;

            return errorExpression.Update(
                resultKind,
                errorExpression.symbols,
                errorExpression.childBoundNodes,
                resultType
            );
        } else {
            var symbols = ArrayBuilder<Symbol>.GetInstance();
            expression.GetExpressionSymbols(symbols);

            return new BoundErrorExpression(
                expression.syntax,
                resultKind,
                symbols.ToImmutableAndFree(),
                [BindToTypeForErrorRecovery(expression)],
                resultType ?? CreateErrorType(),
                true
            );
        }
    }

    private BoundExpression BindReversibleExpression(
        ReversibleExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var token = LocateDeclaredTokenSymbol(node.identifier);

        var nameConflict = token.scopeBinder.ValidateDeclarationNameConflictsInScope(token, diagnostics);

        var hasError = BindReverseExpression(
            node.expression,
            diagnostics,
            out var boundExpression,
            out var conversion,
            out var call
        );

        if (hasError) {
            diagnostics.Push(Error.ReversibleExpressionNotReversible(node.expression.location));
            return new BoundReversibleExpression(node, null, token, conversion, boundExpression.type, true);
        }

        return new BoundReversibleExpression(node, call, token, conversion, boundExpression.type, nameConflict);
    }

    private bool BindReverseExpression(
        ExpressionSyntax expression,
        BelteDiagnosticQueue diagnostics,
        out BoundExpression boundExpression,
        out Conversion conversion,
        out BoundCallExpression call) {
        boundExpression = BindExpression(expression, diagnostics);
        conversion = default;

        if (boundExpression is BoundCallExpression c && c.method.isReversible) {
            call = c;
            var reverseMethod = call.method.reverseMethod;

            if (reverseMethod.parameterCount > 0) {
                var parameter = reverseMethod.parameters[0];
                var dummy = boundExpression;

                if (call.method.hasReversalState) {
                    dummy = call.Update(
                        call.receiver,
                        call.method,
                        call.arguments,
                        call.argumentRefKinds,
                        call.defaultArguments,
                        call.resultKind,
                        call.method.stateMethod.returnType.tupleElementTypes[1].type.type
                    );
                }

                GenerateConversionForAssignment(
                    parameter.type,
                    dummy,
                    diagnostics,
                    out conversion,
                    parameter.refKind != RefKind.None
                        ? ConversionForAssignmentFlags.RefAssignment
                        : ConversionForAssignmentFlags.None
                );
            }
        } else {
            call = null;
            return true;
        }

        return false;
    }

    private SourceTokenSymbol LocateDeclaredTokenSymbol(SyntaxToken identifier) {
        return LookupToken(identifier) ?? new SourceTokenSymbol(containingMember, identifier, this);
    }

    private BoundExpression BindDeclarationExpressionAsError(
        DeclarationExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var isConst = false;
        var isConstExpr = false;

        // TODO Use the out info?
        var declType = BindVariableTypeWithAnnotations(
            node,
            diagnostics,
            node.type.SkipRef(out _),
            ref isConst,
            ref isConstExpr,
            out var isImplicitlyTyped,
            out var isNonNullable,
            out var isNullable,
            out var alias
        );

        diagnostics.Push(Error.InvalidDeclarationExpression(node.location));

        return BindDeclarationVariablesForErrorRecovery(declType, node, node, diagnostics);
    }

    private BoundExpression BindDeclarationVariablesForErrorRecovery(
        TypeWithAnnotations declTypeWithAnnotations,
        DeclarationExpressionSyntax designation,
        BelteSyntaxNode syntax,
        BelteDiagnosticQueue diagnostics) {
        declTypeWithAnnotations = declTypeWithAnnotations.hasType
            ? declTypeWithAnnotations
            : new TypeWithAnnotations(CreateErrorType("var"));

        var result = BindDeconstructionVariable(declTypeWithAnnotations, designation, syntax, diagnostics);
        return BindToTypeForErrorRecovery(result);
    }

    private BoundExpression BindDeconstructionVariable(
        TypeWithAnnotations declTypeWithAnnotations,
        DeclarationExpressionSyntax designation,
        BelteSyntaxNode syntax,
        BelteDiagnosticQueue diagnostics) {
        var localSymbol = LookupLocal(designation.identifier);

        if (localSymbol is not null) {
            var typeSyntax = designation.type;

            if (typeSyntax is ReferenceTypeSyntax refType)
                diagnostics.Push(Error.DeconstructVariableCannotBeRef(refType.refKeyword.location));

            var hasErrors = localSymbol.scopeBinder.ValidateDeclarationNameConflictsInScope(localSymbol, diagnostics);

            if (declTypeWithAnnotations.hasType) {
                return new BoundDataContainerExpression(
                    syntax,
                    localSymbol,
                    constantValue: null,
                    type: declTypeWithAnnotations.type,
                    hasErrors: hasErrors
                );
            }

            return new DeconstructionVariablePendingInference(syntax, localSymbol, receiver: null);
        } else {
            var field = LookupDeclaredField(designation) ?? throw ExceptionUtilities.Unreachable();
            var typeSyntax = designation.type;

            if (typeSyntax is ReferenceTypeSyntax refType)
                diagnostics.Push(Error.UnexpectedToken(refType.refKeyword.location, refType.refKeyword.kind));

            var receiver = new BoundThisExpression(designation, containingType);

            if (declTypeWithAnnotations.hasType) {
                var fieldType = field.GetFieldType(fieldsBeingBound);

                return new BoundFieldAccessExpression(
                    syntax,
                    receiver,
                    field,
                    constantValue: null,
                    type: fieldType.type
                );
            }

            return new DeconstructionVariablePendingInference(syntax, field, receiver);
        }
    }

    private BoundExpression BindTupleExpression(TupleExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var arguments = node.arguments;
        var numElements = arguments.Count;

        if (numElements < 2) {
            var args = numElements == 1
                ? ImmutableArray.Create(BindValue(arguments[0], diagnostics, BindValueKind.RValue))
                : ImmutableArray<BoundExpression>.Empty;

            return ErrorExpression(node, args);
        }

        var hasNaturalType = true;

        var boundArguments = ArrayBuilder<BoundExpression>.GetInstance(arguments.Count);
        var elementTypes = ArrayBuilder<TypeWithAnnotations>.GetInstance(arguments.Count);
        var elementLocations = ArrayBuilder<TextLocation>.GetInstance(arguments.Count);

        for (var i = 0; i < numElements; i++) {
            var argumentSyntax = arguments[i];
            elementLocations.Add(argumentSyntax.location);
            var boundArgument = BindValue(argumentSyntax, diagnostics, BindValueKind.RValue);

            if (boundArgument.type?.specialType == SpecialType.Void) {
                diagnostics.Push(Error.VoidInTuple(argumentSyntax.location));

                boundArgument = new BoundErrorExpression(
                    argumentSyntax,
                    LookupResultKind.Empty,
                    [],
                    [boundArgument],
                    CreateErrorType("void")
                );
            }

            boundArguments.Add(boundArgument);
            var elementType = boundArgument.type;
            elementTypes.Add(new TypeWithAnnotations(elementType));

            if (elementType is null)
                hasNaturalType = false;
        }

        NamedTypeSymbol tupleType = null;
        var elements = elementTypes.ToImmutableAndFree();
        var locations = elementLocations.ToImmutableAndFree();

        if (hasNaturalType) {
            tupleType = NamedTypeSymbol.CreateTuple(
                node.location,
                elements,
                locations,
                default,
                compilation,
                syntax: node,
                diagnostics: diagnostics,
                shouldCheckConstraints: true,
                errorPositions: []
            );
        }

        if (numElements > 7)
            diagnostics.Push(Warning.LongTuple(node.location, numElements));

        return new BoundTupleLiteral(node, boundArguments.ToImmutableAndFree(), tupleType);
    }

    private BoundWithExpression BindWithExpression(WithExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var assignments = BindWithExpressionList(node.expressions, diagnostics, out var hasErrors);
        var body = BindExpression(node.body, diagnostics);
        return new BoundWithExpression(node, assignments, body, body.type, hasErrors);
    }

    private ImmutableArray<BoundExpression> BindWithExpressionList(
        SeparatedSyntaxList<ExpressionSyntax> expressions,
        BelteDiagnosticQueue diagnostics,
        out bool hasErrors) {
        hasErrors = false;
        var builder = ArrayBuilder<BoundExpression>.GetInstance();

        foreach (var expression in expressions) {
            var boundExpression = BindExpression(expression, diagnostics);

            if (expression.kind != SyntaxKind.AssignmentExpression) {
                if (boundExpression is BoundCallExpression call && call.method.isReversible) {
                    var reverseMethod = call.method.reverseMethod;

                    if (reverseMethod.parameterCount > 0) {
                        var parameter = reverseMethod.parameters[0];

                        if (call.method.hasReversalState) {
                            boundExpression = call.Update(
                                call.receiver,
                                call.method,
                                call.arguments,
                                call.argumentRefKinds,
                                call.defaultArguments,
                                call.resultKind,
                                call.method.stateMethod.returnType.tupleElementTypes[1].type.type
                            );
                        }

                        boundExpression = GenerateConversionForAssignment(
                            parameter.type,
                            boundExpression,
                            diagnostics,
                            parameter.refKind != RefKind.None
                                ? ConversionForAssignmentFlags.RefAssignment
                                : ConversionForAssignmentFlags.None
                        );
                    }
                } else {
                    diagnostics.Push(Error.WithExpressionNotAssignment(expression.location));
                    hasErrors = true;
                }
            }

            builder.Add(boundExpression);
        }

        return builder.ToImmutableAndFree();
    }

    private UnboundLambda BindAnonymousFunction(
        AnonymousFunctionExpressionSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        var lambda = AnalyzeAnonymousFunction(syntax, diagnostics);
        var data = lambda.data;

        // TODO
        // if (data.HasExplicitlyTypedParameterList) {
        //     var firstDefault = -1;

        //     for (var i = 0; i < lambda.ParameterCount; i++) {
        //         var paramSyntax = lambda.ParameterSyntax(i);

        //         if (paramSyntax.Default is not null && firstDefault == -1)
        //             firstDefault = i;

        //         ParameterHelpers.GetModifiers(paramSyntax.Modifiers, refnessKeyword: out _, out var paramsKeyword, thisKeyword: out _, scope: out _);
        //         var isParams = paramsKeyword.Kind() != SyntaxKind.None;

        //         ParameterHelpers.ReportParameterErrors(
        //             owner: null,
        //             paramSyntax,
        //             ordinal: i,
        //             lastParameterIndex:
        //             lambda.ParameterCount - 1,
        //             isParams: isParams,
        //             lambda.ParameterTypeWithAnnotations(i),
        //             lambda.RefKind(i),
        //             containingSymbol: null,
        //             thisKeyword: default,
        //             paramsKeyword: paramsKeyword,
        //             firstDefault,
        //             diagnostics
        //         );
        //     }
        // }

        // TODO We don't allow modifiers on lambdas currently
        // ModifierHelpers.TodEcl(syntax.modifiers, isForTypeDeclaration: false);

        // if (data.HasSignature) {
        //     var binder = new LocalScopeBinder(this);
        //     bool allowShadowingNames = binder.Compilation.IsFeatureEnabled(MessageID.IDS_FeatureNameShadowingInNestedFunctions);
        //     var pNames = PooledHashSet<string>.GetInstance();
        //     bool seenDiscard = false;

        //     for (int i = 0; i < lambda.ParameterCount; i++) {
        //         var name = lambda.ParameterName(i);

        //         if (string.IsNullOrEmpty(name)) {
        //             continue;
        //         }

        //         if (lambda.ParameterIsDiscard(i)) {
        //             if (seenDiscard) {
        //                 // We only report the diagnostic on the second and subsequent underscores
        //                 MessageID.IDS_FeatureLambdaDiscardParameters.CheckFeatureAvailability(
        //                     diagnostics,
        //                     binder.Compilation,
        //                     lambda.ParameterLocation(i));
        //             }

        //             seenDiscard = true;
        //             continue;
        //         }

        //         if (!pNames.Add(name)) {
        //             // The parameter name '{0}' is a duplicate
        //             diagnostics.Add(ErrorCode.ERR_DuplicateParamName, lambda.ParameterLocation(i), name);
        //         } else if (!allowShadowingNames) {
        //             binder.ValidateLambdaParameterNameConflictsInScope(lambda.ParameterLocation(i), name, diagnostics);
        //         }
        //     }
        //     pNames.Free();
        // }

        return lambda;
    }

    private UnboundLambda AnalyzeAnonymousFunction(
        AnonymousFunctionExpressionSyntax syntax,
        BelteDiagnosticQueue diagnostics) {
        // TODO
        return null;
    }

    private BoundExpression BindImplicitEnumFieldExpression(
        ImplicitEnumFieldExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        return new BoundUnconvertedImplicitEnumFieldExpression(node, node.identifier.valueText);
    }

    private BoundExpression BindStackAllocExpression(
        StackAllocExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var typeSyntax = node.type;

        if (typeSyntax.kind != SyntaxKind.ArrayType) {
            diagnostics.Push(Error.BadStackAllocExpression(typeSyntax.location));

            return new BoundErrorExpression(
                node,
                LookupResultKind.NotCreatable,
                [],
                [],
                new PointerTypeSymbol(BindType(typeSyntax, diagnostics))
            );
        }

        var arrayTypeSyntax = (ArrayTypeSyntax)typeSyntax;
        var arrayType = (ArrayTypeSymbol)BindArrayType(
            arrayTypeSyntax,
            diagnostics,
            permitDimensions: true,
            basesBeingResolved: null,
            useFatArray: false
        ).type;

        var elementType = arrayType.elementTypeWithAnnotations;

        var type = GetStackAllocType(node, elementType, diagnostics, out var hasErrors);

        var rankSpecifiers = arrayTypeSyntax.rankSpecifiers;

        if (rankSpecifiers.Count != 1) {
            diagnostics.Push(Error.BadStackAllocExpression(typeSyntax.location));

            var builder = ArrayBuilder<BoundExpression>.GetInstance();

            foreach (var rankSpecifier in rankSpecifiers) {
                builder.Add(
                    BindToTypeForErrorRecovery(BindExpression(rankSpecifier.size, BelteDiagnosticQueue.Discarded))
                );
            }

            return new BoundErrorExpression(
                node,
                LookupResultKind.Empty,
                [],
                builder.ToImmutableAndFree(),
                new PointerTypeSymbol(elementType)
            );
        }

        var countSyntax = rankSpecifiers[0].size;

        var int32 = CorLibrary.GetSpecialType(SpecialType.Int32);
        var count = BindValue(countSyntax, diagnostics, BindValueKind.RValue);
        count = ReduceNumericIfApplicable(int32, count);
        count = GenerateConversionForAssignment(int32, count, diagnostics);

        if ((int)count.constantValue.value < 0) {
            diagnostics.Push(Error.NegativeStackAllocSize(countSyntax.location));
            hasErrors = true;
        }

        return new BoundStackAllocExpression(
            node,
            elementType.type,
            count,
            type,
            hasErrors
        );
    }

    private TypeSymbol GetStackAllocType(
        SyntaxNode node,
        TypeWithAnnotations elementTypeWithAnnotations,
        BelteDiagnosticQueue diagnostics,
        out bool hasErrors) {
        var inLegalPosition = ReportBadStackAllocPosition(node, diagnostics);
        hasErrors = !inLegalPosition;

        // TODO Use Span<T> eventually for non-lowlevel contexts?
        if (inLegalPosition && !IsStackallocTargetTyped(node))
            diagnostics.Push(Error.NoStackAllocTarget(node.location));

        return new PointerTypeSymbol(elementTypeWithAnnotations);

        static bool IsStackallocTargetTyped(SyntaxNode node) {
            var equalsValueClause = node.parent;

            if (equalsValueClause.kind != SyntaxKind.EqualsValueClause)
                return false;

            var variableDeclaration = equalsValueClause.parent;

            if (variableDeclaration.kind != SyntaxKind.VariableDeclaration)
                return false;

            return variableDeclaration.parent.kind is SyntaxKind.LocalDeclarationStatement or SyntaxKind.ForStatement;
        }
    }

    private bool ReportBadStackAllocPosition(SyntaxNode node, BelteDiagnosticQueue diagnostics) {
        var inLegalPosition = true;
        // TODO Theres something to say about nested stack allocs

        if (flags.Includes(BinderFlags.InCatchBlock) || flags.Includes(BinderFlags.InFinallyBlock))
            diagnostics.Push(Error.StackAllocInCatchFinally(node.location));

        return inLegalPosition;
    }

    private BoundExpression BindCascadeListExpression(
        CascadeListExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var receiver = BindExpression(node.expression, diagnostics);
        var cascadeBuilder = ArrayBuilder<BoundExpression>.GetInstance();
        var conditionalsBuilder = ArrayBuilder<bool>.GetInstance();
        var hasErrors = false;

        foreach (var cascadeExpression in node.cascades) {
            var expression = cascadeExpression.expression;
            var isConditional = cascadeExpression.op.kind == SyntaxKind.QuestionPeriodPeriodToken;

            switch (expression.kind) {
                case SyntaxKind.CallExpression: {
                        var call = (CallExpressionSyntax)expression;

                        if (call.expression is not SimpleNameSyntax name) {
                            diagnostics.Push(Error.NestedCascadeExpression(call.expression.location));
                            hasErrors = true;
                            continue;
                        }

                        var access = BindMemberAccessWithBoundLeft(
                            expression,
                            receiver,
                            name,
                            cascadeExpression.op,
                            true,
                            false,
                            diagnostics
                        );

                        var analyzedArguments = AnalyzedArguments.GetInstance();
                        var result = BindArgumentsAndInvocation(call, access, analyzedArguments, diagnostics);

                        hasErrors |= result.hasErrors;
                        conditionalsBuilder.Add(isConditional);
                        cascadeBuilder.Add(result);
                    }

                    break;
                case SyntaxKind.AssignmentExpression: {
                        var assignment = (AssignmentExpressionSyntax)expression;

                        if (assignment.left is not SimpleNameSyntax name) {
                            diagnostics.Push(Error.NestedCascadeExpression(assignment.left.location));
                            hasErrors = true;
                            continue;
                        }

                        var access = BindMemberAccessWithBoundLeft(
                            expression,
                            receiver,
                            name,
                            cascadeExpression.op,
                            false,
                            false,
                            diagnostics
                        );

                        BoundExpression result;

                        if (assignment.assignmentToken.kind is SyntaxKind.QuestionQuestionEqualsToken or
                                                               SyntaxKind.QuestionExclamationEqualsToken) {
                            var leftOperand = CheckValue(access, BindValueKind.CompoundAssignment, diagnostics);
                            result = BindNullCoalescingCompoundAssignmentWithBoundLeft(
                                assignment,
                                leftOperand,
                                diagnostics
                            );
                        } else if (assignment.assignmentToken.kind != SyntaxKind.EqualsToken) {
                            var leftOperand = CheckValue(
                                access,
                                GetBinaryAssignmentKind(assignment.assignmentToken.kind),
                                diagnostics
                            );

                            result = BindCompoundAssignmentWithBoundLeft(assignment, leftOperand, diagnostics);
                        } else {
                            result = BindSimpleAssignmentWithUncheckedBoundLeft(assignment, access, diagnostics);
                        }

                        hasErrors |= result.hasErrors;
                        conditionalsBuilder.Add(isConditional);
                        cascadeBuilder.Add(result);
                    }

                    break;
                default:
                    hasErrors = true;
                    diagnostics.Push(Error.InvalidCascadeExpression(expression.location));
                    break;
            }
        }

        return new BoundCascadeListExpression(
            node,
            receiver,
            cascadeBuilder.ToImmutableAndFree(),
            conditionalsBuilder.ToImmutableAndFree(),
            receiver.type,
            hasErrors
        );
    }

    private BoundExpression BindThrowExpression(ThrowExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var hasErrors = node.containsDiagnostics;

        if (!IsThrowExpressionInProperContext(node)) {
            diagnostics.Push(Error.ThrowMisplaced(node.throwKeyword.location));
            hasErrors = true;
        }

        var boundExpression = BindValue(node.expression, diagnostics, BindValueKind.RValue);
        var thrownExpression = GenerateConversionForAssignment(
            CorLibrary.GetWellKnownType(WellKnownType.Exception),
            boundExpression,
            diagnostics
        );

        return new BoundThrowExpression(node, thrownExpression, null, hasErrors);
    }

    private static bool IsThrowExpressionInProperContext(ThrowExpressionSyntax node) {
        var parent = node.parent;

        if (parent is null || node.containsDiagnostics)
            return true;

        switch (parent.kind) {
            case SyntaxKind.TernaryExpression:
                var conditionalParent = (TernaryExpressionSyntax)parent;
                return node == conditionalParent.center || node == conditionalParent.right;
            case SyntaxKind.BinaryExpression:
                var binaryParent = (BinaryExpressionSyntax)parent;
                return binaryParent.operatorToken.kind is SyntaxKind.QuestionQuestionToken or
                                                          SyntaxKind.QuestionExclamationToken &&
                    node == binaryParent.right;
            case SyntaxKind.ExpressionStatement:
                return true;
            default:
                return false;
        }
    }

    private BoundExpression BindTypeOfExpression(TypeOfExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var typeSyntax = node.type;

        var typeofBinder = new TypeofBinder(typeSyntax, this);
        var typeWithAnnotations = typeofBinder.BindType(typeSyntax, diagnostics);
        var type = typeWithAnnotations.type;

        var hasError = false;

        // if (typeWithAnnotations.isNullable && type.isReferenceType) {
        // TODO Do we want this restriction?
        // error: cannot take the `typeof` a nullable reference type.
        // diagnostics.Add(ErrorCode.ERR_BadNullableTypeof, node.Location);
        // hasError = true;
        // }

        var boundType = new BoundTypeExpression(typeSyntax, typeWithAnnotations, null, type, type.IsErrorType());
        return new BoundTypeOfExpression(node, boundType, CorLibrary.GetSpecialType(SpecialType.Type), hasError);
    }

    private BoundExpression BindSizeOfExpression(SizeOfExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        ExpressionSyntax typeSyntax = node.type;
        var typeWithAnnotations = BindType(typeSyntax, diagnostics, out var alias);
        var type = typeWithAnnotations.type;

        if (type.IsVoidType())
            diagnostics.Push(Error.VoidUsedAsType(node.type.location));

        var typeHasErrors = type.IsErrorType();

        var boundType = new BoundTypeExpression(typeSyntax, typeWithAnnotations, alias, type, typeHasErrors);
        var int32 = CorLibrary.GetSpecialType(SpecialType.Int32);
        var sizeInBytes = boundType.type.specialType.SizeInBytes();
        var constantValue = sizeInBytes > 0 ? new ConstantValue(sizeInBytes, SpecialType.Int32) : null;

        return new BoundSizeOfOperator(
            node,
            boundType,
            constantValue,
            int32,
            typeHasErrors
        );
    }

    private BoundExpression BindNameOfExpression(NameOfExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var binder = GetBinder(node);
        return binder.BindNameOfInternal(node, diagnostics);
    }

    private BoundExpression BindNameOfInternal(NameOfExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var nameSyntax = node.name;
        var name = GetNameFromSyntax(nameSyntax);
        // This is just to collect diagnostics
        BindExpression(nameSyntax, diagnostics);

        return new BoundLiteralExpression(
            node,
            new ConstantValue(name, SpecialType.String),
            CorLibrary.GetSpecialType(SpecialType.String)
        );
    }

    private string GetNameFromSyntax(NameSyntax name) {
        return name switch {
            IdentifierNameSyntax identifier => identifier.identifier.valueText,
            TemplateNameSyntax template => template.identifier.valueText,
            QualifiedNameSyntax qualified => qualified.right.identifier.valueText,
            _ => throw ExceptionUtilities.UnexpectedValue(name.kind),
        };
    }

    private BoundExpression BindIndexExpression(IndexExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var receiver = BindExpressionInternal(node.expression, diagnostics, false, true);

        var analyzedArguments = AnalyzedArguments.GetInstance();
        try {
            BindArgumentsAndNames(node.argumentList, diagnostics, analyzedArguments);
            receiver = CheckValue(receiver, BindValueKind.RValue, diagnostics);
            receiver = BindToNaturalType(receiver, diagnostics);
            var isConditional = node.argumentList.openBracket.kind == SyntaxKind.QuestionOpenBracketToken;
            return BindArrayAccessOrIndexer(node, isConditional, receiver, analyzedArguments, diagnostics);
        } finally {
            analyzedArguments.Free();
        }
    }

    private BoundExpression BindArrayAccessOrIndexer(
        SyntaxNode node,
        bool isConditional,
        BoundExpression expression,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        if (expression.type is null)
            return ErrorIndexerExpression(node, expression, analyzedArguments, null, diagnostics);

        if (analyzedArguments.anyErrors || expression.hasAnyErrors)
            diagnostics = BelteDiagnosticQueue.Discarded;

        return BindArrayAccessOrIndexerCore(node, isConditional, expression, analyzedArguments, diagnostics);
    }

    private BoundExpression BindArrayAccessOrIndexerCore(
        SyntaxNode node,
        bool isConditional,
        BoundExpression expression,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        switch (expression.StrippedType().typeKind) {
            case TypeKind.Array: {
                    var access = BindArrayAccess(node, expression, analyzedArguments, diagnostics);
                    return CreateConditionalAccess(node, isConditional, expression, access, diagnostics);
                }
            case TypeKind.Class:
            case TypeKind.Struct:
            case TypeKind.Interface:
            case TypeKind.Primitive:
            case TypeKind.TemplateParameter: {
                    var access = BindIndexerAccess(node, expression, analyzedArguments, diagnostics);
                    return CreateConditionalAccess(node, isConditional, expression, access, diagnostics);
                }
            case TypeKind.Pointer:
                return BindPointerIndexAccess(node, expression, analyzedArguments, diagnostics);
            default:
                return ErrorIndexerExpression(
                    node,
                    expression,
                    analyzedArguments,
                    Error.CannotApplyIndexing(node.location, expression.Type()),
                    diagnostics
                );
        }
    }

    private BoundExpression BindPointerIndexAccess(
        SyntaxNode node,
        BoundExpression expression,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        var argument = analyzedArguments.arguments[0].expression;

        if (expression.hasErrors) {
            expression = BindToTypeForErrorRecovery(expression);

            return new BoundPointerIndexAccessExpression(
                node,
                expression,
                argument,
                false,
                CreateErrorType(),
                true
            );
        }

        var intType = CorLibrary.GetSpecialType(SpecialType.Int);
        var conversion = conversions.ClassifyImplicitConversionFromExpression(argument, intType);

        if (!conversion.exists)
            GenerateImplicitConversionError(diagnostics, node, conversion, argument, intType);

        var boundConversion = CreateConversion(argument, conversion, intType, diagnostics);
        var resultType = ((PointerTypeSymbol)expression.Type()).pointedAtType;

        return new BoundPointerIndexAccessExpression(
            node,
            expression,
            boundConversion,
            false,
            resultType
        );
    }

    private BoundExpression BindIndexerAccess(
        SyntaxNode node,
        BoundExpression expression,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        if (analyzedArguments.arguments.Count != 1) {
            diagnostics.Push(Error.BadIndexCount(node.location, 1));
            var errorArguments = BuildArgumentsForErrorRecovery(analyzedArguments);
            return new BoundErrorExpression(node, default, [], [expression], CreateErrorType(), true);
        }

        var argument = analyzedArguments.arguments[0].expression;

        if (expression.hasErrors) {
            expression = BindToTypeForErrorRecovery(expression);

            return new BoundIndexerAccessExpression(
                node,
                expression,
                argument,
                null,
                null,
                CreateErrorType(),
                true
            );
        }

        if (expression.StrippedType().specialType == SpecialType.String) {
            var intType = CorLibrary.GetSpecialType(SpecialType.Int);
            var charType = CorLibrary.GetSpecialType(SpecialType.Char);

            // TODO Allow nullable indexing here? Would necessitate a runtime wrapper around System.String.get_Chars
            // if (argument.type is not null && argument.type.IsNullableType())
            //     intType = CorLibrary.GetNullableType(SpecialType.Int);

            var conversion = conversions.ClassifyImplicitConversionFromExpression(argument, intType);

            if (!conversion.exists)
                GenerateImplicitConversionError(diagnostics, node, conversion, argument, intType);

            var boundConversion = CreateConversion(argument, conversion, intType, diagnostics);
            var constantValue = ConstantFolding.FoldIndex(expression, boundConversion, charType);

            return new BoundIndexerAccessExpression(
                node,
                expression,
                boundConversion,
                null,
                constantValue,
                charType,
                false
            );
        } else if (expression.StrippedType().typeKind == TypeKind.Primitive) {
            diagnostics.Push(Error.CannotApplyIndexing(node.location, expression.Type()));
            return ErrorIndexerExpression(node, expression, analyzedArguments, null, diagnostics);
        }

        var fatArray = CorLibrary.TryGetWellKnownType(WellKnownType.Array, compilation);

        if (expression.StrippedType().originalDefinition.Equals(fatArray)) {
            var intType = CorLibrary.GetSpecialType(SpecialType.Int);
            var namedType = (NamedTypeSymbol)expression.StrippedType();
            var resultType = namedType.templateArguments[0].type.type;

            var conversion = conversions.ClassifyImplicitConversionFromExpression(argument, intType);

            if (!conversion.exists)
                GenerateImplicitConversionError(diagnostics, node, conversion, argument, intType);

            var boundConversion = CreateConversion(argument, conversion, intType, diagnostics);

            var method = CorLibrary.GetWellKnownMethod(WellKnownMember.Array_Get).AsMember(namedType);

            return new BoundIndexerAccessExpression(
                node,
                expression,
                boundConversion,
                method,
                null,
                resultType,
                false
            );
        }

        var lookupResult = LookupResult.GetInstance();
        var lookupOptions = expression.kind == BoundKind.BaseExpression
            ? LookupOptions.UseBaseReferenceAccessibility
            : LookupOptions.Default;

        LookupMembersWithFallback(
            lookupResult,
            expression.Type(),
            WellKnownMemberNames.IndexOperatorName,
            arity: 0,
            node.location,
            options: lookupOptions
        );

        BoundExpression indexerAccessExpression;
        // ? This is a hack to reuse the same overload resolution logic as ordinary methods...but this is only temporary anyways
        analyzedArguments.arguments.Insert(0, new BoundExpressionOrTypeOrConstant(expression));
        analyzedArguments.hasErrors.Insert(0, false);
        analyzedArguments.refKinds.Add(RefKind.None);
        analyzedArguments.refKinds.Add(RefKind.None);
        analyzedArguments.types.Insert(0, expression.Type());
        analyzedArguments.names.Add(null);
        analyzedArguments.names.Add(null);

        if (!lookupResult.isMultiViable) {
            if (TryBindIndexOperator(
                node,
                null,
                analyzedArguments,
                diagnostics,
                out var implicitIndexerAccess)) {
                indexerAccessExpression = implicitIndexerAccess;
            } else {
                var allMembers = expression.Type().GetMembers();
                indexerAccessExpression = ErrorIndexerExpression(
                    node,
                    expression,
                    analyzedArguments,
                    lookupResult.error,
                    diagnostics
                );
            }
        } else {
            var operatorGroup = ArrayBuilder<MethodSymbol>.GetInstance();

            foreach (var symbol in lookupResult.symbols)
                operatorGroup.Add((MethodSymbol)symbol);

            indexerAccessExpression = BindIndexerAccess(
                node,
                null,
                operatorGroup,
                analyzedArguments,
                diagnostics
            );

            operatorGroup.Free();
        }

        lookupResult.Free();
        return indexerAccessExpression;
    }

    private BoundExpression BindIndexerAccess(
        SyntaxNode syntax,
        BoundExpression receiver,
        ArrayBuilder<MethodSymbol> operatorGroup,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        var overloadResolutionResult = OverloadResolutionResult<MethodSymbol>.GetInstance();

        overloadResolution.MethodOverloadResolution(
            operatorGroup,
            [],
            receiver,
            analyzedArguments,
            overloadResolutionResult
        );

        BoundExpression indexerAccess;
        var argumentNames = analyzedArguments.GetNames();
        var argumentRefKinds = analyzedArguments.refKinds.ToImmutableOrNull();

        if (!overloadResolutionResult.succeeded) {
            var candidates = operatorGroup.ToImmutable();

            if (TryBindIndexOperator(
                syntax,
                receiver,
                analyzedArguments,
                diagnostics,
                out var implicitIndexerAccess)) {
                return implicitIndexerAccess;
            } else {
                var candidate = candidates[0];

                overloadResolutionResult.ReportDiagnostics(
                    binder: this,
                    location: syntax.location,
                    node: syntax,
                    diagnostics: diagnostics,
                    name: candidate.name,
                    receiver: null,
                    invokedExpression: null,
                    arguments: analyzedArguments,
                    memberGroup: candidates,
                    typeContainingConstructor: null
                );
            }

            var arguments = BuildArgumentsForErrorRecovery(analyzedArguments, candidates);
            var method = (candidates.Length == 1) ? candidates[0] : CreateErrorMethodSymbol(candidates);

            indexerAccess = new BoundIndexerAccessExpression(
                syntax,
                arguments[0],
                arguments[1],
                method,
                null,
                CreateErrorType(),
                true
            );
        } else {
            var resolutionResult = overloadResolutionResult.bestResult;
            var method = resolutionResult.member;

            var gotError = MemberGroupFinalValidationAccessibilityChecks(receiver, method, syntax, diagnostics);

            CheckAndCoerceArguments(
                syntax,
                resolutionResult,
                analyzedArguments,
                diagnostics,
                receiver,
                out var argsToParams
            );

            // TODO Compiler generated?
            if (!gotError && receiver is not null && receiver.kind == BoundKind.ThisExpression /* && receiver.WasCompilerGenerated */) {
                gotError = IsRefOrOutThisParameterCaptured(syntax, diagnostics);
            }

            var arguments = analyzedArguments.arguments.ToImmutable();
            var expression = arguments[0].expression;
            var index = arguments[1].expression;

            // TODO Do we need any of this extra information?
            indexerAccess = new BoundIndexerAccessExpression(
                syntax,
                expression,
                // initialBindingReceiverIsSubjectToCloning: ReceiverIsSubjectToCloning(receiver, property),
                index,
                method,
                // arguments,
                // argumentNames,
                // argumentRefKinds,
                // argsToParams,
                null,
                method.returnType,
                gotError
            );
        }

        overloadResolutionResult.Free();
        return indexerAccess;
    }

    private bool TryBindIndexOperator(
        SyntaxNode syntax,
        BoundExpression receiver,
        AnalyzedArguments arguments,
        BelteDiagnosticQueue diagnostics,
        out BoundIndexerAccessExpression implicitIndexerAccess) {
        // TODO Maybe move the string intrinsic into here
        implicitIndexerAccess = null;
        return false;
    }

    private ErrorMethodSymbol CreateErrorMethodSymbol(ImmutableArray<MethodSymbol> methodGroup) {
        var returnType = GetCommonTypeOrReturnType(methodGroup) ?? CreateErrorType();
        var candidate = methodGroup[0];
        return new ErrorMethodSymbol(candidate.containingType, returnType, candidate.name);
    }

    private BoundArrayAccessExpression BindArrayAccess(
        SyntaxNode node,
        BoundExpression expression,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        var arrayType = (ArrayTypeSymbol)expression.StrippedType();
        var elementType = arrayType.isSZArray
            ? arrayType.elementType
            : ArrayTypeSymbol.CreateArray(arrayType.elementTypeWithAnnotations, arrayType.rank - 1);

        if (analyzedArguments.arguments.Count != 1) {
            diagnostics.Push(Error.BadIndexCount(node.location, 1));
            var errorArguments = BuildArgumentsForErrorRecovery(analyzedArguments);

            return new BoundArrayAccessExpression(
                node,
                expression,
                errorArguments.FirstOrDefault(),
                null,
                elementType,
                true
            );
        }

        var argument = analyzedArguments.arguments[0];
        var intType = CorLibrary.GetSpecialType(SpecialType.Int);

        // TODO Do we want to allow nullable indexing? Probably not
        // if (argument.type is not null && argument.type.IsNullableType())
        //     intType = CorLibrary.GetNullableType(SpecialType.Int);

        var conversion = conversions.ClassifyImplicitConversionFromExpression(argument.expression, intType);

        if (!conversion.exists)
            GenerateImplicitConversionError(diagnostics, node, conversion, argument.expression, intType);

        var boundConversion = CreateConversion(argument.expression, conversion, intType, diagnostics);
        var hasErrors = false;

        var constantValue = ConstantFolding.FoldIndex(expression, boundConversion, elementType);
        return new BoundArrayAccessExpression(node, expression, boundConversion, constantValue, elementType, hasErrors);
    }

    private BoundUnconvertedInitializerList BindInitializerListExpression(
        InitializerListExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var items = node.items;
        var builder = ArrayBuilder<BoundExpression>.GetInstance(items.Count);

        foreach (var element in items)
            builder.Add(BindElement(element, diagnostics, this));

        return new BoundUnconvertedInitializerList(node, builder.ToImmutableAndFree());

        static BoundExpression BindElement(ExpressionSyntax syntax, BelteDiagnosticQueue diagnostics, Binder @this) {
            return syntax switch {
                InitializerListExpressionSyntax nestedList
                    => @this.BindInitializerListExpression(nestedList, diagnostics),
                ExpressionSyntax expression => @this.BindValue(expression, diagnostics, BindValueKind.RValue),
                _ => throw ExceptionUtilities.UnexpectedValue(syntax.kind)
            };
        }
    }

    private BoundExpression BindBitCastExpression(BitCastExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var operand = BindToNaturalType(BindValue(node.expression, diagnostics, BindValueKind.RValue), diagnostics);
        var targetTypeWithAnnotations = BindType(node.type, diagnostics);
        var targetType = targetTypeWithAnnotations.type;

        if (operand.type is not null && operand.type.specialType.IsNumeric()) {
            if (CanReduceOperand(operand, targetType, out var reducedType)) {
                // TODO Is there a real concern of losing data here?
                if (LiteralUtilities.TrySpecialCastCore(
                    operand.constantValue.value,
                    operand.constantValue.specialType,
                    reducedType.specialType,
                    out var newValue)) {
                    operand = BoundFactory.Literal(operand.syntax, newValue, reducedType);
                }
            }
        }

        var hasErrors = operand.hasAnyErrors;

        if (!hasErrors && (operand.type is null || operand.type.IsNullableType())) {
            diagnostics.Push(Error.CannotBitCastFromNullable(node.expression.location, operand.type));
            hasErrors = true;
        } else if (!hasErrors && targetType.IsNullableType()) {
            diagnostics.Push(Error.CannotBitCastToNullable(node.type.location, targetType));
            hasErrors = true;
        } else if (!hasErrors) {
            var fromTypeSize = operand.type.specialType.SizeInBytes();
            var toTypeSize = targetType.specialType.SizeInBytes();

            if (fromTypeSize == 0) {
                diagnostics.Push(Error.UnknownBitCastSize(
                    node.location,
                    operand.type,
                    operand.type,
                    targetType,
                    operand
                ));

                hasErrors = true;
            }

            if (toTypeSize == 0) {
                diagnostics.Push(Error.UnknownBitCastSize(
                    node.location,
                    targetType,
                    operand.type,
                    targetType,
                    operand
                ));

                hasErrors = true;
            }

            if (!hasErrors && fromTypeSize != toTypeSize) {
                diagnostics.Push(Error.DifferentSizesInBitCast(node.location, operand.type, targetType));
                hasErrors = true;
            }
        }

        var constantValue = hasErrors ? null : ConstantFolding.FoldBitCast(operand, targetType);
        return new BoundBitCastExpression(node, operand, operand.type, constantValue, targetType);

        static bool CanReduceOperand(BoundExpression operand, TypeSymbol target, out TypeSymbol reducedType) {
            reducedType = null;

            if (!ShouldTryToReduce(operand, target.specialType))
                return false;

            var targetSize = target.specialType.SizeInBytes();
            var operandSpecialType = operand.type.specialType;

            if (targetSize == 0)
                return false;

            if (operandSpecialType.IsIntegral()) {
                var isUnsigned = operandSpecialType.IsUnsigned();

                switch (targetSize) {
                    case 1:
                        reducedType = CorLibrary.GetSpecialType(isUnsigned ? SpecialType.UInt8 : SpecialType.Int8);
                        return true;
                    case 2:
                        reducedType = CorLibrary.GetSpecialType(isUnsigned ? SpecialType.UInt16 : SpecialType.Int16);
                        return true;
                    case 4:
                        reducedType = CorLibrary.GetSpecialType(isUnsigned ? SpecialType.UInt32 : SpecialType.Int32);
                        return true;
                    case 8:
                        reducedType = CorLibrary.GetSpecialType(isUnsigned ? SpecialType.UInt64 : SpecialType.Int64);
                        return true;
                }
            } else if (operandSpecialType.IsFloatingPoint()) {
                switch (targetSize) {
                    case 4:
                        reducedType = CorLibrary.GetSpecialType(SpecialType.Float32);
                        return true;
                    case 8:
                        reducedType = CorLibrary.GetSpecialType(SpecialType.Float64);
                        return true;
                }
            }

            return false;
        }
    }

    private BoundExpression BindCastExpression(CastExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var operand = BindValue(node.expression, diagnostics, BindValueKind.RValue);
        var targetTypeWithAnnotations = BindType(node.type, diagnostics);
        var targetType = targetTypeWithAnnotations.type;

        if (targetType.IsNullableType() &&
            !operand.hasAnyErrors &&
            operand.Type() is not null &&
            !operand.Type().IsNullableType() &&
            !TypeSymbol.Equals(
                targetType.GetNullableUnderlyingType(),
                operand.Type(),
                TypeCompareKind.ConsiderEverything)) {
            return BindExplicitNullableCastFromNonNullable(node, operand, targetTypeWithAnnotations, diagnostics);
        }

        return BindCastCore(node, operand, targetTypeWithAnnotations, diagnostics);
    }

    private BoundExpression BindExplicitNullableCastFromNonNullable(
        ExpressionSyntax node,
        BoundExpression operand,
        TypeWithAnnotations targetTypeWithAnnotations,
        BelteDiagnosticQueue diagnostics) {
        var underlyingTargetTypeWithAnnotations = targetTypeWithAnnotations.type
            .GetNullableUnderlyingTypeWithAnnotations();

        var underlyingConversion = conversions.ClassifyBuiltInConversion(
            operand.Type(),
            underlyingTargetTypeWithAnnotations.type
        );

        if (!underlyingConversion.exists)
            return BindCastCore(node, operand, targetTypeWithAnnotations, diagnostics);

        var queue1 = BelteDiagnosticQueue.GetInstance();

        try {
            var underlyingExpression = BindCastCore(node, operand, underlyingTargetTypeWithAnnotations, queue1);

            if (underlyingExpression.constantValue is not null &&
                !underlyingExpression.hasErrors && !queue1.AnyErrors()) {
                diagnostics.PushRange(queue1);
                return BindCastCore(node, underlyingExpression, targetTypeWithAnnotations, diagnostics);
            }

            var queue2 = BelteDiagnosticQueue.GetInstance();

            var result = BindCastCore(node, operand, targetTypeWithAnnotations, queue2);

            if (queue1.AnyErrors() && !queue2.AnyErrors())
                diagnostics.PushRange(queue1);

            diagnostics.PushRangeAndFree(queue2);
            return result;
        } finally {
            queue1.Free();
        }
    }

    private BoundExpression BindCastCore(
        ExpressionSyntax node,
        BoundExpression operand,
        TypeWithAnnotations targetTypeWithAnnotations,
        BelteDiagnosticQueue diagnostics) {
        var targetType = targetTypeWithAnnotations.type;
        var conversion = conversions.ClassifyConversionFromExpression(operand, targetType);
        var suppressErrors = operand.hasAnyErrors || targetType.IsErrorType();
        var hasErrors = !conversion.exists || targetType.isStatic;

        if (hasErrors && !suppressErrors)
            GenerateExplicitConversionErrors(diagnostics, node, conversion, operand, targetType);

        return CreateConversion(
            node,
            operand,
            conversion,
            isCast: true,
            destination: targetType,
            diagnostics: diagnostics,
            hasErrors: hasErrors | suppressErrors
        );
    }

    private void GenerateExplicitConversionErrors(
        BelteDiagnosticQueue diagnostics,
        SyntaxNode syntax,
        Conversion conversion,
        BoundExpression operand,
        TypeSymbol targetType) {
        if (operand.hasAnyErrors || targetType.IsErrorType())
            return;

        if (targetType.StrippedType().isStatic) {
            diagnostics.Push(Error.CannotConvertToStatic(syntax.location, targetType));
            return;
        }

        if (!targetType.IsNullableType() && operand.IsLiteralNull()) {
            if (!targetType.isStatic)
                diagnostics.Push(Error.ValueCannotBeNull(syntax.location, targetType));

            return;
        }

        switch (operand.kind) {
            case BoundKind.UnconvertedInitializerList:
                GenerateImplicitConversionErrorForList(
                    (BoundUnconvertedInitializerList)operand,
                    targetType,
                    diagnostics
                );

                break;
        }

        diagnostics.Push(Error.CannotConvert(syntax.location, operand.Type(), targetType));
    }

    private BoundArrayCreationExpression BindArrayCreationWithInitializer(
        BelteDiagnosticQueue diagnostics,
        ExpressionSyntax creationSyntax,
        InitializerListExpressionSyntax initSyntax,
        TypeSymbol type,
        ImmutableArray<BoundExpression> sizes,
        bool hasErrors = false) {
        var rank = type is ArrayTypeSymbol array ? array.rank : 1;
        var numSizes = sizes.Length;
        var knownSizes = new long?[Math.Max(rank, numSizes)];

        for (var i = 0; i < numSizes; ++i) {
            var size = sizes[i];
            knownSizes[i] = size.constantValue?.value is long l ? l : null;

            if (!size.hasAnyErrors && knownSizes[i] is null) {
                diagnostics.Push(Error.ConstantExpected(size.syntax.location));
                hasErrors = true;
            }
        }

        var initializer = BindArrayInitializerList(
            diagnostics,
            initSyntax,
            type,
            knownSizes,
            1,
            false,
            default
        );

        hasErrors = hasErrors || initializer.hasAnyErrors;
        var nonNullSyntax = (SyntaxNode)creationSyntax ?? initSyntax;

        if (numSizes == 0) {
            var sizeArray = new BoundExpression[rank];

            for (var i = 0; i < rank; i++) {
                sizeArray[i] = BoundFactory.Literal(
                    nonNullSyntax,
                    knownSizes[i] ?? 0,
                    CorLibrary.GetSpecialType(SpecialType.Int)
                );
            }

            sizes = sizeArray.AsImmutableOrNull();
        } else if (!hasErrors && rank != numSizes) {
            diagnostics.Push(Error.BadIndexCount(nonNullSyntax.location, rank));
            hasErrors = true;
        }

        return new BoundArrayCreationExpression(nonNullSyntax, sizes, initializer, type, hasErrors);
    }

    private BoundInitializerList BindArrayInitializerList(
        BelteDiagnosticQueue diagnostics,
        InitializerListExpressionSyntax node,
        TypeSymbol type,
        long?[] knownSizes,
        int dimension,
        bool isInferred,
        ImmutableArray<BoundExpression> boundInitExprOpt = default) {
        if (boundInitExprOpt.IsDefault)
            boundInitExprOpt = BindArrayInitializerExpressions(node, diagnostics, dimension, type);

        var boundInitExprIndex = 0;

        return ConvertAndBindArrayInitialization(
            diagnostics,
            node,
            type,
            knownSizes,
            dimension,
            boundInitExprOpt,
            ref boundInitExprIndex,
            isInferred
        );
    }

    private BoundInitializerList ConvertAndBindArrayInitialization(
        BelteDiagnosticQueue diagnostics,
        InitializerListExpressionSyntax node,
        TypeSymbol type,
        long?[] knownSizes,
        int dimension,
        ImmutableArray<BoundExpression> boundInitExpr,
        ref int boundInitExprIndex,
        bool isInferred) {
        var initializers = ArrayBuilder<BoundExpression>.GetInstance();
        var (rank, elementType) = GetArrayRankAndElementType(type);

        if (dimension == rank) {
            var elemType = elementType;

            foreach (var expressionSyntax in node.items) {
                var boundExpression = boundInitExpr[boundInitExprIndex];
                boundInitExprIndex++;

                var convertedExpression = GenerateConversionForAssignment(elemType, boundExpression, diagnostics);
                initializers.Add(convertedExpression);
            }
        } else {
            foreach (var expr in node.items) {
                BoundExpression init = null;

                if (expr.kind == SyntaxKind.InitializerListExpression) {
                    init = ConvertAndBindArrayInitialization(
                        diagnostics,
                        (InitializerListExpressionSyntax)expr,
                        type,
                        knownSizes,
                        dimension + 1,
                        boundInitExpr,
                        ref boundInitExprIndex,
                        isInferred
                    );
                } else {
                    init = boundInitExpr[boundInitExprIndex];
                    boundInitExprIndex++;
                }

                initializers.Add(init);
            }
        }

        var hasErrors = false;
        var knownSizeOpt = knownSizes[dimension - 1];

        if (knownSizeOpt is null) {
            knownSizes[dimension - 1] = initializers.Count;
        } else if (knownSizeOpt != initializers.Count) {
            if (knownSizeOpt >= 0) {
                diagnostics.Push(Error.ArrayInitWrongLength(node.location, knownSizeOpt.Value));
                hasErrors = true;
            }
        }

        return new BoundInitializerList(node, initializers.ToImmutableAndFree(), type, hasErrors: hasErrors);
    }

    private ImmutableArray<BoundExpression> BindArrayInitializerExpressions(
        InitializerListExpressionSyntax initializer,
        BelteDiagnosticQueue diagnostics,
        int dimension,
        TypeSymbol type) {
        var exprBuilder = ArrayBuilder<BoundExpression>.GetInstance();
        BindArrayInitializerExpressions(initializer, exprBuilder, diagnostics, dimension, type);
        return exprBuilder.ToImmutableAndFree();
    }

    internal static (int rank, TypeSymbol elementType) GetArrayRankAndElementType(TypeSymbol type) {
        if (type is ArrayTypeSymbol array)
            return (array.rank, array.elementType);
        else
            return (1, ((NamedTypeSymbol)type).templateArguments[0].type.type);
    }

    private void BindArrayInitializerExpressions(
        InitializerListExpressionSyntax initializer,
        ArrayBuilder<BoundExpression> exprBuilder,
        BelteDiagnosticQueue diagnostics,
        int dimension,
        TypeSymbol type) {
        var (rank, elementType) = GetArrayRankAndElementType(type);

        if (dimension == rank) {
            foreach (var expression in initializer.items) {
                var boundExpression = BindPossibleArrayInitializer(
                    expression,
                    elementType,
                    BindValueKind.RValue,
                    diagnostics
                );

                exprBuilder.Add(boundExpression);
            }
        } else {
            foreach (var expression in initializer.items) {
                if (expression.kind == SyntaxKind.InitializerListExpression) {
                    BindArrayInitializerExpressions(
                        (InitializerListExpressionSyntax)expression,
                        exprBuilder,
                        diagnostics,
                        dimension + 1,
                        type
                    );
                } else {
                    var boundExpression = BindValue(expression, diagnostics, BindValueKind.RValue);

                    if (boundExpression.type is null || !boundExpression.type.IsErrorType()) {
                        if (!boundExpression.hasAnyErrors)
                            diagnostics.Push(Error.ArrayInitExpected(expression.location));

                        boundExpression = ErrorExpression(
                            expression,
                            LookupResultKind.Empty,
                            ImmutableArray.Create(boundExpression.expressionSymbol),
                            ImmutableArray.Create(boundExpression)
                        );
                    }

                    exprBuilder.Add(boundExpression);
                }
            }
        }
    }

    private BoundExpression BindBufferCreation(
        ObjectCreationExpressionSyntax node,
        TypeSymbol type,
        BelteDiagnosticQueue diagnostics) {
        var arguments = node.argumentList.arguments;

        if (arguments.Count >= 1) {
            var argument1 = arguments[0];

            if (argument1.kind == SyntaxKind.OmittedArgument)
                return ReportOmittedArgument(node, argument1, type);

            var normal1 = (ArgumentSyntax)argument1;

            if (normal1.refKindKeyword is not null)
                return ReportRefKind(node, normal1, type);

            if (normal1.expression is InitializerListExpressionSyntax init1 && arguments.Count == 1)
                return BindArrayCreationExpressionCore(node, init1, [(null, null)], type, diagnostics);

            ImmutableArray<(TextLocation, ExpressionSyntax)> rankSpecifiers = [(normal1.location, normal1.expression)];

            InitializerListExpressionSyntax initializer = null;

            if (arguments.Count >= 2) {
                var argument2 = arguments[1];

                if (argument2.kind == SyntaxKind.OmittedArgument)
                    return ReportOmittedArgument(node, argument2, type);

                var normal2 = (ArgumentSyntax)argument2;

                if (normal2.refKindKeyword is not null)
                    return ReportRefKind(node, normal2, type);

                if (normal2.expression is not InitializerListExpressionSyntax init2) {
                    diagnostics.Push(Error.InvalidBufferCreationArgument(normal2.expression.location));
                    return CreateErrorBufferCreation(node, type);
                }

                initializer = init2;

                if (arguments.Count > 2)
                    return ReportWrongArgumentCount(node, type);
            }

            return BindArrayCreationExpressionCore(node, initializer, rankSpecifiers, type, diagnostics);
        } else {
            return ReportWrongArgumentCount(node, type);
        }

        BoundExpression ReportWrongArgumentCount(SyntaxNode node, TypeSymbol type) {
            diagnostics.Push(Error.InvalidBufferCreation(node.location));
            return CreateErrorBufferCreation(node, type);
        }

        BoundExpression ReportRefKind(SyntaxNode node, BaseArgumentSyntax argumentSyntax, TypeSymbol type) {
            diagnostics.Push(Error.InvalidRefKindInBufferCreation(argumentSyntax.location));
            return CreateErrorBufferCreation(node, type);
        }

        BoundExpression ReportOmittedArgument(SyntaxNode node, BaseArgumentSyntax argumentSyntax, TypeSymbol type) {
            diagnostics.Push(Error.OmittedArgumentInBufferCreation(argumentSyntax.location));
            return CreateErrorBufferCreation(node, type);
        }

        static BoundExpression CreateErrorBufferCreation(SyntaxNode syntax, TypeSymbol type) {
            return new BoundErrorExpression(syntax, default, [], [], type, true);
        }
    }

    private BoundExpression BindArrayCreationExpression(
        ArrayCreationExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var arrayType = GetArrayType(node.type, diagnostics);
        var type = BindArrayType(arrayType, diagnostics, true, null).type;

        return BindArrayCreationExpressionCore(
            node,
            node.initializer,
            arrayType.rankSpecifiers.SelectAsArray(r => (r.location, r.size)),
            type,
            diagnostics
        );

        static ArrayTypeSyntax GetArrayType(TypeSyntax syntax, BelteDiagnosticQueue diagnostics) {
            if (syntax is ArrayTypeSyntax a) {
                return a;
            } else if (syntax is NonNullableTypeSyntax nn) {
                diagnostics.Push(Error.AnnotationsDisallowedInObjectCreation(nn.location));
                return GetArrayType(nn.type, diagnostics);
            } else if (syntax is NullableTypeSyntax n) {
                diagnostics.Push(Error.AnnotationsDisallowedInObjectCreation(n.location));
                return GetArrayType(n.type, diagnostics);
            } else if (syntax is ReferenceTypeSyntax r) {
                return GetArrayType(r.type, diagnostics);
            } else {
                throw ExceptionUtilities.Unreachable();
            }
        }
    }

    private BoundExpression BindArrayCreationExpressionCore(
        ExpressionSyntax node,
        InitializerListExpressionSyntax initializer,
        ImmutableArray<(TextLocation location, ExpressionSyntax size)> rankSpecifiers,
        TypeSymbol type,
        BelteDiagnosticQueue diagnostics) {
        var (rank, _) = GetArrayRankAndElementType(type);
        var sizes = ArrayBuilder<BoundExpression>.GetInstance();
        var hasErrors = false;
        var indexType = CorLibrary.GetSpecialType(SpecialType.Int);

        for (var i = 0; i < rank; i++) {
            var rankSpecifier = rankSpecifiers[i];
            var size = rankSpecifier.size;

            if (size is not null) {
                var boundSize = BindExpression(size, diagnostics);
                var sizeConversion = conversions.ClassifyImplicitConversionFromExpression(boundSize, indexType);

                if (!sizeConversion.exists)
                    diagnostics.Push(Error.NonIntArraySize(rankSpecifier.location));
                else
                    boundSize = CreateConversion(boundSize, sizeConversion, indexType, diagnostics);

                sizes.Add(boundSize);
            } else if (initializer is null && i == 0) {
                diagnostics.Push(Error.MissingArraySize(rankSpecifier.location));
                hasErrors = true;
            }
        }

        return initializer is null
            ? new BoundArrayCreationExpression(node, sizes.ToImmutable(), null, type, hasErrors)
            : BindArrayCreationWithInitializer(
                diagnostics,
                node,
                initializer,
                type,
                sizes.ToImmutable(),
                hasErrors
            );
    }

    private protected BoundExpression BindObjectCreationExpression(
        ObjectCreationExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        if (node.type is null)
            return BindImplicitObjectCreationExpression(node, diagnostics);

        var typeWithAnnotations = BindTypeWithoutBufferRewrite(node.type, diagnostics);
        var type = typeWithAnnotations.nullableUnderlyingTypeOrSelf;
        var originalType = type;

        if (node.type.kind is SyntaxKind.NonNullableType or SyntaxKind.NullableType)
            diagnostics.Push(Error.AnnotationsDisallowedInObjectCreation(node.location));

        if (type.originalDefinition.specialType == SpecialType.Buffer)
            return BindBufferCreation(node, RewriteBufferType(type), diagnostics);

        switch (type.typeKind) {
            case TypeKind.Struct:
                // if (!flags.Includes(BinderFlags.LowLevelContext))
                //     diagnostics.Push(Error.CannotUseStruct(node.type.location));

                goto case TypeKind.Class;
            case TypeKind.Class:
            case TypeKind.Enum:
            case TypeKind.Error:
                return BindClassCreationExpression(
                    node,
                    (NamedTypeSymbol)type,
                    GetName(node.type),
                    diagnostics,
                    originalType
                );
            case TypeKind.TemplateParameter:
                return BindTemplateParameterCreationExpression(node, (TemplateParameterSymbol)type, diagnostics);
            case TypeKind.Interface:
                return BindInterfaceCreationExpression(node, (NamedTypeSymbol)type, diagnostics);
            case TypeKind.Pointer:
            case TypeKind.FunctionPointer:
            case TypeKind.Array: {
                    var error = Error.InvalidObjectCreation(node.type.location);
                    diagnostics.Push(error);
                    type = new ExtendedErrorTypeSymbol(type, LookupResultKind.NotCreatable, error);
                }

                goto case TypeKind.Class;
            case TypeKind.Primitive:
                if (!type.isTupleType) {
                    var error = Error.CannotConstructPrimitive(node.type.location);
                    diagnostics.Push(error);
                    type = new ExtendedErrorTypeSymbol(type, LookupResultKind.NotCreatable, error);
                }

                goto case TypeKind.Class;
            default:
                throw ExceptionUtilities.UnexpectedValue(type.typeKind);
        }
    }

    private BoundExpression BindInterfaceCreationExpression(
        ObjectCreationExpressionSyntax node,
        NamedTypeSymbol type,
        BelteDiagnosticQueue diagnostics) {
        var analyzedArguments = AnalyzedArguments.GetInstance();
        BindArgumentsAndNames(node.argumentList, diagnostics, analyzedArguments);

        var result = BindInterfaceCreationExpression(
            node,
            type,
            diagnostics,
            node.type,
            analyzedArguments,
            wasTargetTyped: false
        );

        analyzedArguments.Free();
        return result;
    }

    private BoundExpression BindImplicitObjectCreationExpression(
        ObjectCreationExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var arguments = AnalyzedArguments.GetInstance();
        BindArgumentsAndNames(node.argumentList, diagnostics, arguments);

        var result = new BoundUnconvertedObjectCreationExpression(
            node,
            arguments.arguments.Select(a => a.expression).ToImmutableArray(),
            arguments.names.ToImmutableOrNull(),
            arguments.refKinds.ToImmutableOrNull(),
            binder: this
        );

        arguments.Free();
        return result;
    }

    private BoundExpression BindTemplateParameterCreationExpression(
        ObjectCreationExpressionSyntax node,
        TemplateParameterSymbol templateParameter,
        BelteDiagnosticQueue diagnostics) {
        var analyzedArguments = AnalyzedArguments.GetInstance();
        BindArgumentsAndNames(node.argumentList, diagnostics, analyzedArguments);
        var result = BindTemplateParameterCreationExpression(
            node,
            templateParameter,
            analyzedArguments,
            node.type,
            false,
            diagnostics
        );

        analyzedArguments.Free();
        return result;
    }

    private BoundExpression BindTemplateParameterCreationExpression(
        SyntaxNode node,
        TemplateParameterSymbol templateParameter,
        AnalyzedArguments analyzedArguments,
        SyntaxNode typeSyntax,
        bool wasTargetTyped,
        BelteDiagnosticQueue diagnostics) {
        if (TemplateParameterHasParameterlessConstructor(node, templateParameter, diagnostics)) {
            if (analyzedArguments.arguments.Count > 0)
                diagnostics.Push(Error.NewTemplateWithArguments(node.location, templateParameter));
            else
                return new BoundNewT(node, wasTargetTyped, templateParameter);
        }

        return MakeErrorExpressionForObjectCreation(
            node,
            templateParameter,
            analyzedArguments,
            typeSyntax,
            diagnostics
        );
    }

    private static bool TemplateParameterHasParameterlessConstructor(
        SyntaxNode node,
        TemplateParameterSymbol templateParameter,
        BelteDiagnosticQueue diagnostics) {
        if (!templateParameter.hasConstructorConstraint) {
            diagnostics.Push(Error.NoNewTypeVar(node.location, templateParameter));
            return false;
        }

        return true;
    }

    private BoundExpression BindClassCreationExpression(
        ObjectCreationExpressionSyntax node,
        NamedTypeSymbol type,
        string typeName,
        BelteDiagnosticQueue diagnostics,
        TypeSymbol initializerType = null) {
        var analyzedArguments = AnalyzedArguments.GetInstance();

        try {
            BindArgumentsAndNames(node.argumentList, diagnostics, analyzedArguments);

            if (type.isStatic) {
                diagnostics.Push(Error.CannotCreateStatic(node.location, type));
                return MakeErrorExpressionForObjectCreation(node, type, analyzedArguments, node.type, diagnostics);
            } else if (node.type.kind == SyntaxKind.TupleType) {
                diagnostics.Push(Error.CannotCreateTuple(node.location, node.argumentList));
                return MakeErrorExpressionForObjectCreation(node, type, analyzedArguments, node.type, diagnostics);
            }

            return BindClassCreationExpression(node, typeName, node.type, type, analyzedArguments, diagnostics);
        } finally {
            analyzedArguments.Free();
        }
    }

    private BoundExpression MakeErrorExpressionForObjectCreation(
        SyntaxNode node,
        TypeSymbol type,
        AnalyzedArguments analyzedArguments,
        SyntaxNode typeSyntax,
        BelteDiagnosticQueue diagnostics) {
        return new BoundErrorExpression(
            node,
            LookupResultKind.NotCreatable,
            [type],
            BuildArgumentsForErrorRecovery(analyzedArguments),
            type,
            true
        );
    }

    private protected BoundExpression BindClassCreationExpression(
        SyntaxNode node,
        string typeName,
        SyntaxNode typeNode,
        NamedTypeSymbol type,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        var hasErrors = type.IsErrorType();

        if (type.isAbstract) {
            diagnostics.Push(Error.CannotCreateAbstract(node.location, type));
            hasErrors = true;
        }

        if (TryPerformConstructorOverloadResolution(
                type,
                analyzedArguments,
                typeName,
                typeNode.location,
                hasErrors,
                diagnostics,
                out var memberResolutionResult,
                out var candidateConstructors,
                allowProtectedConstructorsOfBaseType: false) &&
            !type.isAbstract) {
            return BindClassCreationExpressionContinued(
                node,
                typeNode,
                type,
                analyzedArguments,
                memberResolutionResult,
                candidateConstructors,
                diagnostics
            );
        }

        return CreateErrorClassCreationExpression(
            node,
            typeNode,
            type,
            analyzedArguments,
            memberResolutionResult,
            candidateConstructors,
            diagnostics
        );
    }

    internal bool TryPerformConstructorOverloadResolution(
        NamedTypeSymbol typeContainingConstructors,
        AnalyzedArguments analyzedArguments,
        string errorName,
        TextLocation errorLocation,
        bool suppressResultDiagnostics,
        BelteDiagnosticQueue diagnostics,
        out MemberResolutionResult<MethodSymbol> memberResolutionResult,
        out ImmutableArray<MethodSymbol> candidateConstructors,
        bool allowProtectedConstructorsOfBaseType,
        bool isParamsModifierValidation = false) {
        candidateConstructors = GetAccessibleConstructorsForOverloadResolution(
            typeContainingConstructors,
            allowProtectedConstructorsOfBaseType,
            out var allInstanceConstructors
        );

        var result = OverloadResolutionResult<MethodSymbol>.GetInstance();
        var succeededConsideringAccessibility = false;

        if (candidateConstructors.Any()) {
            overloadResolution.ObjectCreationOverloadResolution(
                candidateConstructors,
                analyzedArguments,
                result
            );

            if (result.succeeded)
                succeededConsideringAccessibility = true;
        }

        if (!succeededConsideringAccessibility && allInstanceConstructors.Length > candidateConstructors.Length) {
            var inaccessibleResult = OverloadResolutionResult<MethodSymbol>.GetInstance();
            overloadResolution.ObjectCreationOverloadResolution(
                allInstanceConstructors,
                analyzedArguments,
                inaccessibleResult
            );

            if (inaccessibleResult.succeeded) {
                candidateConstructors = allInstanceConstructors;
                result.Free();
                result = inaccessibleResult;
            } else {
                inaccessibleResult.Free();
            }
        }

        memberResolutionResult = result.succeeded ? result.bestResult : default;

        if (!succeededConsideringAccessibility && !suppressResultDiagnostics) {
            if (result.succeeded) {
                diagnostics.Push(Error.MemberIsInaccessible(errorLocation, result.bestResult.member));
            } else {
                result.ReportDiagnostics(
                    binder: this,
                    location: errorLocation,
                    node: null,
                    diagnostics,
                    name: errorName,
                    receiver: null,
                    invokedExpression: null,
                    analyzedArguments,
                    memberGroup: candidateConstructors,
                    typeContainingConstructors
                );
            }
        }

        result.Free();
        return succeededConsideringAccessibility;
    }

    private ImmutableArray<MethodSymbol> GetAccessibleConstructorsForOverloadResolution(NamedTypeSymbol type) {
        return GetAccessibleConstructorsForOverloadResolution(type, false, out _);
    }

    private ImmutableArray<MethodSymbol> GetAccessibleConstructorsForOverloadResolution(
        NamedTypeSymbol type,
        bool allowProtectedConstructorsOfBaseType,
        out ImmutableArray<MethodSymbol> allInstanceConstructors) {
        if (type.IsErrorType())
            type = type.GetNonErrorGuess() as NamedTypeSymbol ?? type;

        if (type.IsNullableType())
            type = type.StrippedType() as NamedTypeSymbol ?? type;

        allInstanceConstructors = type.instanceConstructors;
        return FilterInaccessibleConstructors(allInstanceConstructors, allowProtectedConstructorsOfBaseType);
    }

    internal ImmutableArray<MethodSymbol> FilterInaccessibleConstructors(
        ImmutableArray<MethodSymbol> constructors,
        bool allowProtectedConstructorsOfBaseType) {
        ArrayBuilder<MethodSymbol> builder = null;

        for (var i = 0; i < constructors.Length; i++) {
            var constructor = constructors[i];

            if (!IsConstructorAccessible(constructor, allowProtectedConstructorsOfBaseType)) {
                if (builder is null) {
                    builder = ArrayBuilder<MethodSymbol>.GetInstance();
                    builder.AddRange(constructors, i);
                }
            } else {
                builder?.Add(constructor);
            }
        }

        return builder is null ? constructors : builder.ToImmutableAndFree();
    }

    private BoundObjectCreationExpression BindClassCreationExpressionContinued(
        SyntaxNode node,
        SyntaxNode typeNode,
        NamedTypeSymbol type,
        AnalyzedArguments analyzedArguments,
        MemberResolutionResult<MethodSymbol> memberResolutionResult,
        ImmutableArray<MethodSymbol> candidateConstructors,
        BelteDiagnosticQueue diagnostics,
        bool wasTargetTyped = false) {
        ImmutableArray<int> argToParams;

        if (memberResolutionResult.isNotNull) {
            CheckAndCoerceArguments(
                node,
                memberResolutionResult,
                analyzedArguments,
                diagnostics,
                receiver: null,
                out argToParams
            );
        } else {
            argToParams = memberResolutionResult.result.argsToParams;
        }

        var method = memberResolutionResult.member;
        var hasError = false;

        BindDefaultArguments(
            node,
            method.parameters,
            analyzedArguments.arguments,
            analyzedArguments.refKinds,
            analyzedArguments.names,
            ref argToParams,
            out var defaultArguments,
            enableCallerInfo: true,
            diagnostics
        );

        var arguments = analyzedArguments.arguments.Select(a => a.expression).ToImmutableArray();
        var refKinds = analyzedArguments.refKinds.ToImmutableOrNull();
        var creation = new BoundObjectCreationExpression(
            node,
            method,
            // candidateConstructors,
            arguments,
            // analyzedArguments.GetNames(),
            refKinds,
            argToParams,
            defaultArguments,
            // constantValueOpt,
            // boundInitializerOpt,
            wasTargetTyped,
            type,
            hasError
        );

        return creation;
    }

    private BoundExpression CreateErrorClassCreationExpression(
        SyntaxNode node,
        SyntaxNode typeNode,
        NamedTypeSymbol type,
        AnalyzedArguments analyzedArguments,
        MemberResolutionResult<MethodSymbol> memberResolutionResult,
        ImmutableArray<MethodSymbol> candidateConstructors,
        BelteDiagnosticQueue diagnostics) {
        if (memberResolutionResult.isNotNull) {
            CheckAndCoerceArguments(
                node,
                memberResolutionResult,
                analyzedArguments,
                diagnostics,
                receiver: null,
                argsToParams: out _
            );
        }

        LookupResultKind resultKind;

        if (type.isAbstract || type.IsPrimitiveType())
            resultKind = LookupResultKind.NotCreatable;
        else if (memberResolutionResult.isValid && !IsConstructorAccessible(memberResolutionResult.member))
            resultKind = LookupResultKind.Inaccessible;
        else
            resultKind = LookupResultKind.OverloadResolutionFailure;

        return new BoundErrorExpression(
            node,
            resultKind,
            [.. candidateConstructors],
            BuildArgumentsForErrorRecovery(analyzedArguments),
            type,
            true
        );
    }

    private bool IsConstructorAccessible(MethodSymbol constructor, bool allowProtectedConstructorsOfBaseType = false) {
        var containingType = this.containingType;

        if (containingType is not null) {
            return allowProtectedConstructorsOfBaseType
                ? IsAccessible(constructor, null)
                : IsSymbolAccessibleConditional(constructor, containingType, constructor.containingType);
        } else {
            return IsSymbolAccessibleConditional(constructor, compilation.globalNamespaceInternal);
        }
    }

    internal BoundExpression BindBooleanExpression(ExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var expression = BindValue(node, diagnostics, BindValueKind.RValue);
        var boolean = CorLibrary.GetNullableType(SpecialType.Bool);

        if (expression.hasAnyErrors) {
            return new BoundCastExpression(
                node,
                BindToTypeForErrorRecovery(expression),
                Conversion.None,
                null,
                boolean,
                true
            );
        }

        var conversion = conversions.ClassifyConversionFromExpression(expression, boolean);

        if (conversion.isImplicit) {
            var collapsed = Conversion.CollapseConversion(conversion);

            if (collapsed.kind == ConversionKind.Identity) {
                if (expression.kind == BoundKind.AssignmentOperator) {
                    var assignment = (BoundAssignmentOperator)expression;

                    if (assignment.right.constantValue?.specialType == SpecialType.Bool)
                        diagnostics.Push(Warning.IncorrectBooleanAssignment(assignment.syntax.location));
                }
            }

            return CreateConversion(
                node: expression.syntax,
                source: expression,
                conversion: conversion,
                isCast: false,
                destination: boolean,
                diagnostics: diagnostics
            );
        }

        expression = BindToNaturalType(expression, diagnostics);
        var best = UnaryOperatorOverloadResolution(
            UnaryOperatorKind.True,
            expression,
            node,
            diagnostics,
            out var resultKind,
            out var originalUserDefinedOperators
        );

        if (!best.hasValue) {
            GenerateImplicitConversionError(diagnostics, node, conversion, expression, boolean);
            return new BoundCastExpression(node, expression, Conversion.None, null, boolean, true);
        }

        var signature = best.signature;
        var resultOperand = CreateConversion(
            node,
            expression,
            best.conversion,
            isCast: false,
            destination: best.signature.operandType,
            diagnostics: diagnostics
        );

        return new BoundUnaryOperator(
            node,
            resultOperand,
            signature.kind,
            signature.method,
            null,
            signature.returnType,
            false
        );
    }

    private BoundExpression BindIdentifier(
        SimpleNameSyntax node,
        bool called,
        bool indexed,
        BelteDiagnosticQueue diagnostics) {
        BoundExpression expression;
        var hasTemplateArguments = node.arity > 0;
        var templateArgumentList = node is TemplateNameSyntax t ? t.templateArgumentList.arguments : default;
        var templateArguments = hasTemplateArguments ? BindTemplateArguments(templateArgumentList, diagnostics) : null;

        var lookupResult = LookupResult.GetInstance();
        var name = node.identifier.valueText;
        LookupIdentifier(lookupResult, node, called);

        if (lookupResult.kind != LookupResultKind.Empty) {
            var members = ArrayBuilder<Symbol>.GetInstance();
            var symbol = GetSymbolOrMethodGroup(
                lookupResult,
                node,
                name,
                node.arity,
                members,
                diagnostics,
                out var isError,
                null
            );

            if (symbol is null) {
                var receiver = SynthesizeMethodGroupReceiver(node, members);
                expression = ConstructBoundMemberGroupAndReportOmittedTemplateArguments(
                    node,
                    templateArgumentList,
                    templateArguments,
                    receiver,
                    name,
                    members,
                    lookupResult,
                    receiver is not null ? BoundMethodGroupFlags.HasImplicitReceiver : BoundMethodGroupFlags.None,
                    isError,
                    diagnostics
                );

                ReportSimpleProgramLocalReferencedOutsideOfTopLevelStatement(node, members[0], diagnostics);
            } else {
                var isNamedType = symbol.kind is SymbolKind.NamedType or SymbolKind.ErrorType;

                if (hasTemplateArguments && isNamedType) {
                    symbol = ConstructNamedTypeUnlessTemplateArgumentOmitted(
                        node,
                        (NamedTypeSymbol)symbol,
                        templateArgumentList,
                        templateArguments,
                        diagnostics
                    );
                }

                expression = BindNonMethod(node, symbol, diagnostics, lookupResult.kind, indexed, isError);

                if (!isNamedType && (hasTemplateArguments || node.kind == SyntaxKind.TemplateName)) {
                    expression = new BoundErrorExpression(
                        node,
                        LookupResultKind.WrongTemplate,
                        [symbol],
                        [BindToTypeForErrorRecovery(expression)],
                        expression.Type(),
                        isError
                    );
                }

                if (symbol is DataContainerSymbol d && d.isGlobal && containingMember is not SynthesizedEntryPoint)
                    ReportSimpleProgramLocalReferencedOutsideOfTopLevelStatement(node, symbol, diagnostics);
            }

            members.Free();
        } else {
            expression = null;

            if (node is IdentifierNameSyntax identifier) {
                if (FallBackOnDiscard(identifier))
                    expression = new BoundDiscardExpression(node, isInferred: true, type: null);
            }

            if (expression is null) {
                expression = ErrorExpression(node);

                if (lookupResult.error is not null)
                    diagnostics.Push(BelteDiagnostic.AddLocation(lookupResult.error, node.location));
                else
                    diagnostics.Push(Error.UndefinedSymbol(node.location, name));
            }
        }

        lookupResult.Free();
        return expression;
    }

    private static bool FallBackOnDiscard(IdentifierNameSyntax node) {
        if (node.identifier.valueText != "_")
            return false;

        var containingDeconstruction = node.GetContainingDeconstruction();
        var isDiscard = containingDeconstruction is not null || IsOutVarDiscardIdentifier(node);
        return isDiscard;
    }

    private static bool IsOutVarDiscardIdentifier(SimpleNameSyntax node) {
        var parent = node.parent;
        return parent?.kind == SyntaxKind.Argument &&
            ((ArgumentSyntax)parent).refKindKeyword?.kind == SyntaxKind.OutKeyword;
    }

    private bool ReportSimpleProgramLocalReferencedOutsideOfTopLevelStatement(
        SimpleNameSyntax node,
        Symbol symbol,
        BelteDiagnosticQueue diagnostics) {
        if (!compilation.options.isScript &&
            symbol.containingSymbol is SynthesizedEntryPoint &&
            !containingType.Equals(symbol.containingSymbol.containingType)) {
            diagnostics.Push(Error.ProgramLocalReferencedOutsideOfTopLevelStatement(node.location, node));
            return true;
        }

        return false;
    }

    private BoundMethodGroup ConstructBoundMemberGroupAndReportOmittedTemplateArguments(
        SyntaxNode syntax,
        SeparatedSyntaxList<BaseArgumentSyntax> templateArgumentsSyntax,
        AnalyzedArguments templateArguments,
        BoundExpression receiver,
        string plainName,
        ArrayBuilder<Symbol> members,
        LookupResult lookupResult,
        BoundMethodGroupFlags methodGroupFlags,
        bool hasErrors,
        BelteDiagnosticQueue diagnostics) {
        if (!hasErrors &&
            lookupResult.isMultiViable &&
            templateArgumentsSyntax?.Any(SyntaxKind.OmittedArgument) == true) {
            diagnostics.Push(Error.BadArity(
                syntax.location,
                plainName,
                MessageID.IDS_MethodGroup.Localize(),
                templateArgumentsSyntax.Count
            ));

            hasErrors = true;
        }

        switch (members[0].kind) {
            case SymbolKind.Method:
                return new BoundMethodGroup(
                    syntax,
                    plainName,
                    members.SelectAsArray(s => (MethodSymbol)s),
                    templateArguments is null
                        ? []
                        : templateArguments.arguments.Select(a => a.typeOrConstant).ToImmutableArray(),
                    lookupResult.singleSymbolOrDefault,
                    BelteDiagnostic.AddLocation(lookupResult.error, syntax.location),
                    methodGroupFlags,
                    receiver,
                    lookupResult.kind,
                    null,
                    hasErrors
                );
            default:
                throw ExceptionUtilities.UnexpectedValue(members[0].kind);
        }
    }

    private BoundExpression SynthesizeMethodGroupReceiver(BelteSyntaxNode syntax, ArrayBuilder<Symbol> members) {
        var currentType = containingType;

        if (currentType is null)
            return null;

        var declaringType = members[0].containingType;

        if (currentType.IsEqualToOrDerivedFrom(declaringType, TypeCompareKind.ConsiderEverything) ||
            (currentType.isInterface &&
            (declaringType.specialType == SpecialType.Object || currentType.allInterfaces.Contains(declaringType)))) {
            return new BoundThisExpression(syntax, currentType);
        }

        return null;
    }

    private BoundExpression BindNonMethod(
        SimpleNameSyntax node,
        Symbol symbol,
        BelteDiagnosticQueue diagnostics,
        LookupResultKind resultKind,
        bool indexed,
        bool isError) {
        switch (symbol.kind) {
            case SymbolKind.Local: {
                    var localSymbol = (DataContainerSymbol)symbol;
                    var type = BindResultTypeForLocalVariableReference(
                        node,
                        localSymbol,
                        diagnostics,
                        out var isTypeError
                    );

                    isError |= isTypeError;

                    var constantValue = localSymbol.isConstExpr && !isInsideNameof && !type.IsErrorType()
                        ? localSymbol.GetConstantValue(node, localInProgress, diagnostics)
                        : null;

                    return new BoundDataContainerExpression(
                        node,
                        localSymbol,
                        constantValue,
                        type,
                        isError
                    );
                }
            case SymbolKind.Parameter: {
                    var parameter = (ParameterSymbol)symbol;

                    if (IsBadLocalOrParameterCapture(parameter, parameter.type, parameter.refKind)) {
                        isError = true;
                        // TODO is this a reachable error?
                        throw ExceptionUtilities.Unreachable();
                    }

                    return new BoundParameterExpression(
                        node,
                        parameter,
                        null,
                        parameter.type,
                        isError
                    );
                }
            case SymbolKind.Namespace:
                return new BoundNamespaceExpression(node, (NamespaceSymbol)symbol, null, isError);
            case SymbolKind.Alias: {
                    var alias = (AliasSymbol)symbol;
                    return alias.target switch {
                        TypeSymbol typeSymbol => new BoundTypeExpression(node, null, alias, typeSymbol, isError),
                        NamespaceSymbol namespaceSymbol => new BoundNamespaceExpression(node, namespaceSymbol, alias, isError),
                        _ => throw ExceptionUtilities.UnexpectedValue(alias.target.kind),
                    };
                }
            case SymbolKind.NamedType:
            case SymbolKind.ErrorType:
            case SymbolKind.TemplateParameter:
                return new BoundTypeExpression(node, null, null, (TypeSymbol)symbol, isError);
            case SymbolKind.Field: {
                    var receiver = SynthesizeReceiver(node, symbol, diagnostics);
                    return BindFieldAccess(
                        node,
                        receiver,
                        (FieldSymbol)symbol,
                        diagnostics,
                        resultKind,
                        indexed,
                        isError
                    );
                }
            default:
                throw ExceptionUtilities.UnexpectedValue(symbol.kind);
        }
    }

    private bool IsBadLocalOrParameterCapture(Symbol symbol, TypeSymbol type, RefKind refKind) {
        if (refKind != RefKind.None) {
            if (containingMember is MethodSymbol containingMethod &&
                (object)symbol.containingSymbol != containingMethod) {
                return (containingMethod.methodKind == MethodKind.LocalFunction) && !isInsideNameof;
            }
        }

        return false;
    }

    private BoundExpression SynthesizeReceiver(SyntaxNode node, Symbol member, BelteDiagnosticQueue diagnostics) {
        if (!member.RequiresInstanceReceiver())
            return null;

        var currentType = containingType;
        var declaringType = member.containingType;

        if (currentType.IsEqualToOrDerivedFrom(declaringType, TypeCompareKind.ConsiderEverything) ||
            (currentType.isInterface &&
            (declaringType.specialType == SpecialType.Object || currentType.allInterfaces.Contains(declaringType)))) {
            var hasErrors = false;

            if (!isInsideNameof) {
                BelteDiagnostic diagnosticInfoOpt = null;

                if (inFieldInitializer) {
                    diagnostics.Push(Error.CannotUseThis(node.location));
                } else if (_inConstructorInitializer) {
                    diagnostics.Push(Error.InstanceRequired(node.location, member));
                } else {
                    var containingMember = this.containingMember;

                    var locationIsInstanceMember = !containingMember.isStatic &&
                        (containingMember.kind != SymbolKind.NamedType);

                    if (!locationIsInstanceMember)
                        diagnostics.Push(Error.InstanceRequired(node.location, member));
                }

                diagnosticInfoOpt ??= GetDiagnosticIfRefOrOutThisParameterCaptured(node.location);
                hasErrors = diagnosticInfoOpt is not null;

                if (hasErrors && !isInsideNameof)
                    diagnostics.Push(diagnosticInfoOpt);
            }

            return new BoundThisExpression(node, currentType ?? CreateErrorType(), hasErrors);
        } else {
            return null;
        }
    }

    private void LookupIdentifier(LookupResult lookupResult, SimpleNameSyntax node, bool called) {
        LookupIdentifier(lookupResult, node.identifier.valueText, node.arity, called, node.location);
    }

    private void LookupIdentifier(
        LookupResult lookupResult,
        string name,
        int arity,
        bool called,
        TextLocation errorLocation) {
        var options = LookupOptions.AllMethodsOnArityZero;

        if (called)
            options |= LookupOptions.MustBeInvocableIfMember;

        if (!isInMethodBody && !isInsideNameof)
            options |= LookupOptions.MustNotBeMethodTemplateParameter;

        LookupSymbolsWithFallback(lookupResult, name, arity, errorLocation, options: options);
    }

    private BoundExpression BindMemberAccess(
        MemberAccessExpressionSyntax node,
        bool called,
        bool indexed,
        BelteDiagnosticQueue diagnostics) {
        BoundExpression boundLeft;

        if (node.operatorToken.kind == SyntaxKind.MinusGreaterThanToken) {
            boundLeft = BindRValueWithoutTargetType(node.expression, diagnostics);

            BindPointerIndirectionExpressionInternal(
                node,
                diagnostics,
                boundLeft,
                out var pointedAtType,
                out var hasErrors
            );

            if (pointedAtType is null) {
                boundLeft = ToErrorExpression(boundLeft);
            } else {
                boundLeft = new BoundPointerIndirectionOperator(
                    node.expression,
                    boundLeft,
                    false,
                    pointedAtType,
                    hasErrors
                );
            }
        } else {
            boundLeft = BindExpression(node.expression, diagnostics);
        }

        return BindMemberAccessWithBoundLeft(
            node,
            boundLeft,
            node.name,
            node.operatorToken,
            called,
            indexed,
            diagnostics
        );
    }

    private BoundExpression BindReferenceType(ReferenceTypeSyntax node, BelteDiagnosticQueue diagnostics) {
        diagnostics.Push(Error.UnexpectedToken(node.refKeyword.location, node.refKeyword.kind));
        return new BoundTypeExpression(node, null, null, CreateErrorType("ref"));
    }

    private BoundExpression BindReferenceExpression(ReferenceExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var firstToken = node.GetFirstToken();
        diagnostics.Push(Error.UnexpectedToken(firstToken.location, firstToken.kind));
        return new BoundErrorExpression(
            node,
            LookupResultKind.Empty,
            [],
            [BindToTypeForErrorRecovery(
                BindValue(node.expression, BelteDiagnosticQueue.Discarded, BindValueKind.RefersToLocation)
            )],
            CreateErrorType("ref"),
            true
        );
    }

    private BoundExpression BindQualifiedName(QualifiedNameSyntax node, BelteDiagnosticQueue diagnostics) {
        var left = BindLeftOfPotentialColorColorMemberAccess(node.left, diagnostics);
        return BindMemberAccessWithBoundLeft(node, left, node.right, node.period, false, false, diagnostics);
    }

    private BoundExpression BindLeftOfPotentialColorColorMemberAccess(
        ExpressionSyntax left,
        BelteDiagnosticQueue diagnostics) {
        if (left is IdentifierNameSyntax identifier)
            return BindLeftIdentifierOfPotentialColorColorMemberAccess(identifier, diagnostics);

        return BindExpression(left, diagnostics);
    }

    private BoundExpression BindLeftIdentifierOfPotentialColorColorMemberAccess(
        IdentifierNameSyntax left,
        BelteDiagnosticQueue diagnostics) {
        if (left.isFabricated)
            return BindAsValue(left, diagnostics);

        var lookupResult = LookupResult.GetInstance();
        LookupIdentifier(lookupResult, left, called: false);

        var leftSymbol = lookupResult.isSingleViable ? lookupResult.symbols[0] : null;
        lookupResult.Free();

        if (leftSymbol is null)
            return BindAsValue(left, diagnostics);

        TypeSymbol leftType = null;

        switch (leftSymbol.kind) {
            case SymbolKind.Field:
                var fieldSymbol = (FieldSymbol)leftSymbol;
                leftType = fieldSymbol.GetFieldType(fieldsBeingBound).type;
                leftType = GetAdjustedTypeForEnumMemberReference(fieldSymbol, leftType) ?? leftType;
                break;
            case SymbolKind.Local:
                leftType = BindResultTypeForLocalVariableReference(left, (DataContainerSymbol)leftSymbol, BelteDiagnosticQueue.Discarded, isError: out _);
                break;
            case SymbolKind.Parameter:
                leftType = ((ParameterSymbol)leftSymbol).type;
                break;
        }

        if (leftType is null)
            return BindAsValue(left, diagnostics);

        var leftName = left.identifier.valueText;

        if (leftType.name == leftName || IsUsingAliasInScope(leftName)) {
            var boundType = BindNamespaceOrType(left, BelteDiagnosticQueue.Discarded);

            if (TypeSymbol.Equals(boundType.type, leftType, TypeCompareKind.AllIgnoreOptions)) {
                throw ExceptionUtilities.Unreachable();
                // return new BoundTypeOrValueExpression(left, this, leftSymbol, leftType);
            }
        }

        var boundValue = BindAsValue(left, diagnostics);
        return boundValue;

        BoundExpression BindAsValue(IdentifierNameSyntax left, BelteDiagnosticQueue diagnostics) {
            return BindIdentifier(left, called: false, indexed: false, diagnostics: diagnostics);
        }
    }

    private TypeSymbol GetAdjustedTypeForEnumMemberReference(FieldSymbol fieldSymbol, TypeSymbol fieldType) {
        NamedTypeSymbol underlyingType = null;

        if (InEnumMemberInitializer()) {
            NamedTypeSymbol enumType = null;
            var type = fieldSymbol.containingType;
            var isEnumField = fieldSymbol.isStatic && type.IsEnumType();

            if (isEnumField)
                enumType = type;
            else if (fieldSymbol.isConst && fieldType.IsEnumType())
                enumType = (NamedTypeSymbol)fieldType;

            if (enumType is not null)
                underlyingType = enumType.enumUnderlyingType;
        }

        return underlyingType;
    }

    private bool IsUsingAliasInScope(string name) {
        for (var chain = importChain; chain is not null; chain = chain.parentOpt) {
            if (IsUsingAlias(chain.imports.usingAliases, name))
                return true;
        }

        return false;
    }

    private TypeSymbol BindResultTypeForLocalVariableReference(
        SimpleNameSyntax node,
        DataContainerSymbol localSymbol,
        BelteDiagnosticQueue diagnostics,
        out bool isError) {
        isError = false;
        TypeSymbol type;

        // TODO Pretty sure this is reported already
        // if (ReportSimpleProgramLocalReferencedOutsideOfTopLevelStatement(node, localSymbol, diagnostics)) {
        //     type = new ExtendedErrorTypeSymbol(
        //         compilation,
        //         name: "var",
        //         arity: 0,
        //         error: null,
        //         variableUsedBeforeDeclaration: true
        //     );
        // } else
        if (IsUsedBeforeDeclaration(node, localSymbol)) {
            var lookupResult = LookupResult.GetInstance();

            LookupMembersInType(
                lookupResult,
                containingType,
                localSymbol.name,
                arity: 0,
                basesBeingResolved: null,
                options: LookupOptions.Default,
                originalBinder: this,
                errorLocation: node.location,
                diagnose: false
            );

            var possibleField = lookupResult.singleSymbolOrDefault as FieldSymbol;
            lookupResult.Free();

            if (possibleField is not null) {
                diagnostics.Push(
                    Error.LocalUsedBeforeDeclarationAndHidesField(node.location, localSymbol, possibleField)
                );
            } else {
                diagnostics.Push(Error.LocalUsedBeforeDeclaration(node.location, localSymbol));
            }

            type = new ExtendedErrorTypeSymbol(
                compilation,
                name: "var",
                arity: 0,
                error: null,
                variableUsedBeforeDeclaration: true
            );
        } else {
            type = localSymbol.GetTypeWithAnnotations(node, diagnostics).type;

            if (IsBadLocalOrParameterCapture(localSymbol, type, localSymbol.refKind)) {
                // TODO is this a reachable error?
                throw ExceptionUtilities.Unreachable();
                // isError = true;
            }
        }

        return type;

        static bool IsUsedBeforeDeclaration(SimpleNameSyntax node, DataContainerSymbol localSymbol) {
            if (!localSymbol.hasSourceLocation)
                return false;

            var declaration = localSymbol.GetDeclarationSyntax();

            if (node.span.start >= declaration.span.start)
                return false;

            return node.syntaxTree == declaration.syntaxTree;
        }
    }

    private BoundExpression BindMemberAccessWithBoundLeft(
        ExpressionSyntax node,
        BoundExpression boundLeft,
        SimpleNameSyntax right,
        SyntaxToken operatorToken,
        bool called,
        bool indexed,
        BelteDiagnosticQueue diagnostics) {
        boundLeft = MakeMemberAccessValue(boundLeft, diagnostics);
        var leftType = boundLeft.Type();

        if (leftType is not null && leftType.IsVoidType()) {
            diagnostics.Push(Error.InvalidUnaryOperatorUse(
                operatorToken.location,
                SyntaxFacts.GetText(operatorToken.kind),
                leftType
            ));

            return ErrorExpression(node, boundLeft);
        }

        boundLeft = BindToNaturalType(boundLeft, diagnostics);
        leftType = boundLeft.Type()?.StrippedType();
        var isConditional = operatorToken.kind is SyntaxKind.QuestionPeriodToken or SyntaxKind.QuestionPeriodPeriodToken;
        var lookupResult = LookupResult.GetInstance();

        try {
            var options = LookupOptions.AllMethodsOnArityZero;

            if (called)
                options |= LookupOptions.MustBeInvocableIfMember;

            var templateArgumentsSyntax = right.kind == SyntaxKind.TemplateName
                ? ((TemplateNameSyntax)right).templateArgumentList.arguments
                : null;

            var templateArguments = templateArgumentsSyntax?.Count > 0
                ? BindTemplateArguments(templateArgumentsSyntax, diagnostics)
                : null;

            var rightName = right.identifier.valueText;
            var rightArity = right.arity;
            BoundExpression result = null;

            switch (boundLeft.kind) {
                case BoundKind.NamespaceExpression: {
                        var ns = ((BoundNamespaceExpression)boundLeft).namespaceSymbol;
                        LookupMembersWithFallback(
                            lookupResult,
                            ns,
                            rightName,
                            rightArity,
                            right.location,
                            options: options
                        );

                        var symbols = lookupResult.symbols;

                        if (lookupResult.isMultiViable) {
                            var sym = ResultSymbol(
                                lookupResult,
                                rightName,
                                rightArity,
                                node,
                                diagnostics,
                                out var wasError,
                                ns,
                                options
                            );

                            if (wasError) {
                                return new BoundErrorExpression(
                                    node,
                                    LookupResultKind.Ambiguous,
                                    lookupResult.symbols.AsImmutable(),
                                    ImmutableArray.Create(boundLeft),
                                    CreateErrorType(rightName),
                                    hasErrors: true
                                );
                            } else if (sym.kind == SymbolKind.Namespace) {
                                return new BoundNamespaceExpression(node, (NamespaceSymbol)sym, null);
                            } else {
                                var type = (NamedTypeSymbol)sym;

                                if (templateArguments is not null) {
                                    type = ConstructNamedTypeUnlessTypeArgumentOmitted(
                                        right,
                                        type,
                                        templateArgumentsSyntax,
                                        templateArguments,
                                        diagnostics
                                    );
                                }

                                return new BoundTypeExpression(node, null, null, type);
                            }
                        } else if (lookupResult.kind == LookupResultKind.WrongTemplate) {
                            diagnostics.Push(lookupResult.error);

                            return new BoundTypeExpression(node, null, null, new ExtendedErrorTypeSymbol(
                                GetContainingNamespaceOrType(symbols[0]),
                                symbols.ToImmutable(),
                                lookupResult.kind,
                                lookupResult.error,
                                rightArity
                            ));
                        } else if (lookupResult.kind == LookupResultKind.Empty) {
                            NotFound(
                                node,
                                rightName,
                                rightArity,
                                rightName,
                                diagnostics,
                                alias: null,
                                qualifier: ns,
                                options: options
                            );

                            return new BoundErrorExpression(
                                node,
                                lookupResult.kind,
                                symbols.AsImmutable(),
                                ImmutableArray.Create(boundLeft),
                                CreateErrorType(rightName),
                                hasErrors: true
                            );
                        }

                        return null;
                    }
                case BoundKind.TypeExpression: {
                        if (leftType.typeKind == TypeKind.TemplateParameter) {
                            LookupMembersWithFallback(
                                lookupResult,
                                leftType,
                                rightName,
                                rightArity,
                                right.location,
                                null,
                                options | LookupOptions.MustNotBeInstance | LookupOptions.MustBeAbstractOrVirtual
                            );

                            if (lookupResult.isMultiViable) {
                                result = BindMemberOfType(
                                    node,
                                    right,
                                    rightName,
                                    rightArity,
                                    indexed,
                                    boundLeft,
                                    templateArgumentsSyntax,
                                    templateArguments,
                                    lookupResult,
                                    BoundMethodGroupFlags.None,
                                    diagnostics
                                );
                            } else if (lookupResult.isClear) {
                                diagnostics.Push(Error.LookupInTemplateVariable(boundLeft.syntax.location, leftType));
                                return ErrorExpression(node, LookupResultKind.NotAValue, boundLeft);
                            }
                        } else if (_enclosingNameofArgument == node) {
                            result = BindInstanceMemberAccess(
                                node,
                                right,
                                boundLeft,
                                rightName,
                                rightArity,
                                templateArgumentsSyntax,
                                templateArguments,
                                called,
                                indexed,
                                diagnostics
                            );
                        } else {
                            LookupMembersWithFallback(
                                lookupResult,
                                leftType,
                                rightName,
                                rightArity,
                                right.location,
                                null,
                                options
                            );

                            if (lookupResult.isMultiViable) {
                                result = BindMemberOfType(
                                    node,
                                    right,
                                    rightName,
                                    rightArity,
                                    indexed,
                                    boundLeft,
                                    templateArgumentsSyntax,
                                    templateArguments,
                                    lookupResult,
                                    BoundMethodGroupFlags.None,
                                    diagnostics
                                );
                            }
                        }
                    }

                    break;
                default:
                    if (boundLeft.IsLiteralNull()) {
                        if (!boundLeft.hasAnyErrors) {
                            diagnostics.Push(Error.InvalidUnaryOperatorUse(
                                node.location,
                                operatorToken.text,
                                CreateErrorType("<null>")
                            ));
                        }

                        return ErrorExpression(node, boundLeft);
                    } else if (leftType is not null) {
                        boundLeft = CheckValue(boundLeft, BindValueKind.RValue, diagnostics);
                        boundLeft = BindToNaturalType(boundLeft, diagnostics);

                        result = BindInstanceMemberAccess(
                            node,
                            right,
                            boundLeft,
                            rightName,
                            rightArity,
                            templateArgumentsSyntax,
                            templateArguments,
                            called,
                            indexed,
                            diagnostics
                        );
                    }

                    break;
            }

            if (result is not null)
                return CreateConditionalAccess(node, isConditional, boundLeft, result, diagnostics);

            BindMemberAccessReportError(node, right, rightName, boundLeft, lookupResult.error, diagnostics);

            return BindMemberAccessBadResult(
                node,
                rightName,
                boundLeft,
                lookupResult.error,
                lookupResult.symbols.ToImmutable(),
                lookupResult.kind
            );
        } finally {
            lookupResult.Free();
        }
    }

    private NamedTypeSymbol ConstructNamedTypeUnlessTypeArgumentOmitted(
        SyntaxNode typeSyntax,
        NamedTypeSymbol type,
        SeparatedSyntaxList<BaseArgumentSyntax> templateArgumentsSyntax,
        AnalyzedArguments templateArguments,
        BelteDiagnosticQueue diagnostics) {
        if (templateArgumentsSyntax.Any(SyntaxKind.OmittedArgument)) {
            diagnostics.Push(Error.BadArity(
                typeSyntax.location,
                type,
                MessageID.IDS_SK_TYPE.Localize(),
                templateArgumentsSyntax.Count)
            );

            return type;
        } else {
            return ConstructNamedType(
                type,
                typeSyntax,
                templateArgumentsSyntax,
                templateArguments,
                basesBeingResolved: null,
                diagnostics: diagnostics
            );
        }
    }

    private BelteDiagnostic NotFound(
        SyntaxNode where,
        string simpleName,
        int arity,
        string whereText,
        BelteDiagnosticQueue diagnostics,
        string alias,
        NamespaceOrTypeSymbol qualifier,
        LookupOptions options) {
        var location = where.location;
        // AssemblySymbol forwardedToAssembly;

        // TODO Attributes
        // if (options.IsAttributeTypeLookup() && !options.IsVerbatimNameAttributeTypeLookup()) {
        //     string attributeName = arity > 0 ? $"{simpleName}Attribute<>" : $"{simpleName}Attribute";

        //     NotFound(where, simpleName, arity, attributeName, diagnostics, aliasOpt, qualifierOpt, options | LookupOptions.VerbatimNameAttributeTypeOnly);
        // }

        if (qualifier is not null) {
            if (qualifier.isType) {
                if (qualifier is ErrorTypeSymbol errorQualifier && errorQualifier.error is not null)
                    return errorQualifier.error;

                var error = Error.DottedTypeNamesNotFound(location, whereText, qualifier.StrippedTypeOrSelf());
                diagnostics.Push(error);
                return error;
            } else {
                // TODO Assembly refs
                // forwardedToAssembly = GetForwardedToAssembly(simpleName, arity, ref qualifierOpt, diagnostics, location);

                if (ReferenceEquals(qualifier, compilation.globalNamespace)) {
                    var error = Error.GlobalSingleTypeNameNotFound(location, whereText);
                    diagnostics.Push(error);
                    return error;
                } else {
                    object container = qualifier;

                    if (alias is not null && qualifier.isNamespace && ((NamespaceSymbol)qualifier).isGlobalNamespace)
                        container = alias;

                    var error = Error.DottedTypeNamesNotFoundInNamespace(location, whereText, container);
                    diagnostics.Push(error);
                    return error;
                }
            }
        }

        if (options == LookupOptions.NamespaceAliasesOnly) {
            var error = Error.AliasNotFound(location, whereText);
            diagnostics.Push(error);
            return error;
        }

        // if (where is IdentifierNameSyntax { identifier.text: "var" } && !options.IsAttributeTypeLookup()) {
        //     var code = (where.Parent is QueryClauseSyntax) ? ErrorCode.ERR_TypeVarNotFoundRangeVariable : ErrorCode.ERR_TypeVarNotFound;
        //     return diagnostics.Add(code, location);
        // }

        // forwardedToAssembly = GetForwardedToAssembly(simpleName, arity, ref qualifierOpt, diagnostics, location);

        // if ((object)forwardedToAssembly != null) {
        //     return qualifierOpt == null
        //         ? diagnostics.Add(ErrorCode.ERR_SingleTypeNameNotFoundFwd, location, whereText, forwardedToAssembly)
        //         : diagnostics.Add(ErrorCode.ERR_DottedTypeNameNotFoundInNSFwd, location, whereText, qualifierOpt, forwardedToAssembly);
        // }

        var finalError = Error.SingleTypeNameNotFound(location, whereText);
        diagnostics.Push(finalError);
        return finalError;
    }

    private BoundExpression CreateConditionalAccess(
        SyntaxNode syntax,
        bool isConditional,
        BoundExpression receiver,
        BoundExpression access,
        BelteDiagnosticQueue diagnostics) {
        var receiverType = receiver.Type();

        if (!isConditional) {
            if (receiverType is not null &&
                (receiverType.IsNullableType() ||
                (receiverType is TemplateParameterSymbol p && !p.hasNotNullConstraint))) {
                ReportNullableReceiver(syntax, receiver, access, diagnostics);
            }

            return access;
        }

        if (receiver.hasErrors || access.hasErrors)
            return access;

        if (receiverType is not null &&
            !receiverType.IsNullableType() &&
            (receiverType is not TemplateParameterSymbol tp || tp.hasNotNullConstraint)) {
            ReportNonNullableReceiver(syntax, receiver, access, diagnostics);
            return access;
        }

        return new BoundConditionalAccessExpression(
            syntax,
            receiver,
            access,
            access.Type() is null ? access.Type() : CorLibrary.GetOrCreateNullableType(access.Type())
        );
    }

    private void ReportNullableReceiver(
        SyntaxNode syntax,
        BoundExpression receiver,
        BoundExpression access,
        BelteDiagnosticQueue diagnostics) {
        switch (access.kind) {
            case BoundKind.FieldAccessExpression: {
                    var field = ((BoundFieldAccessExpression)access).field;
                    diagnostics.Push(Error.NullableReceiver(syntax.location, receiver, field));
                    break;
                }
            case BoundKind.ArrayAccessExpression: {
                    var index = ((BoundArrayAccessExpression)access).index;
                    diagnostics.Push(Error.NullableReceiverArray(syntax.location, receiver, index));
                    break;
                }
            case BoundKind.MethodGroup: {
                    var right = ((BoundMethodGroup)access).name;
                    diagnostics.Push(Error.NullableReceiverCall(syntax.location, receiver, right));
                    break;
                }
            case BoundKind.IndexerAccessExpression: {
                    var index = ((BoundIndexerAccessExpression)access).index;

                    if (CorLibrary.GetWellKnownType(WellKnownType.Array)
                        .Equals(receiver.type.StrippedType().originalDefinition)) {
                        diagnostics.Push(Error.NullableReceiverArray(syntax.location, receiver, index));
                    } else {
                        diagnostics.Push(Error.NullableReceiverIndex(syntax.location, receiver, index));
                    }

                    break;
                }
            case BoundKind.UnconvertedArrayLength:
            case BoundKind.ArrayLength:
                diagnostics.Push(Error.NullableReceiverProperty(
                    syntax.location,
                    receiver,
                    WellKnownMemberNames.BufferLength
                ));

                break;
            case BoundKind.ErrorExpression:
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(access.kind);
        }
    }

    private void ReportNonNullableReceiver(
        SyntaxNode syntax,
        BoundExpression receiver,
        BoundExpression access,
        BelteDiagnosticQueue diagnostics) {
        switch (access.kind) {
            case BoundKind.FieldAccessExpression: {
                    var field = ((BoundFieldAccessExpression)access).field;
                    diagnostics.Push(Error.NonNullableReceiver(syntax.location, receiver, field));
                    break;
                }
            case BoundKind.ArrayAccessExpression: {
                    var index = ((BoundArrayAccessExpression)access).index;
                    diagnostics.Push(Error.NonNullableReceiverArray(syntax.location, receiver, index));
                    break;
                }
            case BoundKind.MethodGroup: {
                    var right = ((BoundMethodGroup)access).name;
                    diagnostics.Push(Error.NonNullableReceiverCall(syntax.location, receiver, right));
                    break;
                }
            case BoundKind.IndexerAccessExpression: {
                    var index = ((BoundIndexerAccessExpression)access).index;

                    if (CorLibrary.GetWellKnownType(WellKnownType.Array)
                        .Equals(receiver.type.StrippedType().originalDefinition)) {
                        diagnostics.Push(Error.NonNullableReceiverArray(syntax.location, receiver, index));
                    } else {
                        diagnostics.Push(Error.NonNullableReceiverIndex(syntax.location, receiver, index));
                    }

                    break;
                }
            case BoundKind.UnconvertedArrayLength:
            case BoundKind.ArrayLength:
                diagnostics.Push(Error.NonNullableReceiverProperty(
                    syntax.location,
                    receiver,
                    WellKnownMemberNames.BufferLength
                ));

                break;
            case BoundKind.ErrorExpression:
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(access.kind);
        }
    }

    private BoundExpression BindMemberAccessBadResult(
        SyntaxNode node,
        string nameString,
        BoundExpression boundLeft,
        BelteDiagnostic lookupError,
        ImmutableArray<Symbol> symbols,
        LookupResultKind lookupKind) {
        if (symbols.Length > 0 && symbols[0].kind == SymbolKind.Method) {
            var builder = ArrayBuilder<MethodSymbol>.GetInstance();

            foreach (var s in symbols)
                if (s is MethodSymbol m) builder.Add(m);

            var methods = builder.ToImmutableAndFree();

            return new BoundMethodGroup(
                node,
                nameString,
                methods,
                [],
                methods.Length == 1 ? methods[0] : null,
                BelteDiagnostic.AddLocation(lookupError, node.location),
                BoundMethodGroupFlags.None,
                boundLeft,
                lookupKind,
                null,
                true
            );
        }

        var symbol = symbols.Length == 1 ? symbols[0] : null;
        return new BoundErrorExpression(
            node,
            lookupKind,
            symbol is null ? [] : [symbol],
            boundLeft is null ? [] : [BindToTypeForErrorRecovery(boundLeft)],
            GetNonMethodMemberType(symbol),
            true
        );
    }

    private TypeSymbol GetNonMethodMemberType(Symbol symbol) {
        TypeSymbol resultType = null;

        if (symbol is not null) {
            switch (symbol.kind) {
                case SymbolKind.Field:
                    resultType = ((FieldSymbol)symbol).GetFieldType(fieldsBeingBound).type;
                    break;
            }
        }

        return resultType ?? CreateErrorType();
    }

    private void BindMemberAccessReportError(
        SyntaxNode node,
        SyntaxNode name,
        string plainName,
        BoundExpression boundLeft,
        BelteDiagnostic lookupError,
        BelteDiagnosticQueue diagnostics) {
        if (boundLeft.hasAnyErrors && boundLeft.kind != BoundKind.TypeExpression)
            return;

        if (lookupError is not null) {
            diagnostics.Push(BelteDiagnostic.AddLocation(lookupError, node.location));
        } else {
            if (boundLeft.type is null)
                diagnostics.Push(Error.NoSuchMember(name.location, boundLeft, plainName));
            else
                diagnostics.Push(Error.NoSuchMember(name.location, boundLeft.StrippedType(), plainName));
        }
    }

    private BoundExpression BindInstanceMemberAccess(
        SyntaxNode node,
        SyntaxNode right,
        BoundExpression boundLeft,
        string rightName,
        int rightArity,
        SeparatedSyntaxList<BaseArgumentSyntax> templateArgumentsSyntax,
        AnalyzedArguments templateArguments,
        bool called,
        bool indexed,
        BelteDiagnosticQueue diagnostics) {
        var leftType = boundLeft.StrippedType();

        if (leftType.IsArray() &&
            rightName == WellKnownMemberNames.BufferLength &&
            rightArity == 0 &&
            !called &&
            !indexed) {
            // TODO Consider raising an error if the name is correct but is called or has template arguments or something
            return new BoundUnconvertedArrayLength(node, boundLeft, null);
        }

        var lookupResult = LookupResult.GetInstance();

        try {
            var leftIsBaseReference = boundLeft.kind == BoundKind.BaseExpression;
            LookupInstanceMember(
                lookupResult,
                leftType,
                leftIsBaseReference,
                rightName,
                rightArity,
                called,
                right.location
            );

            BoundMethodGroupFlags flags = 0;

            if (lookupResult.isMultiViable) {
                return BindMemberOfType(
                    node,
                    right,
                    rightName,
                    rightArity,
                    indexed,
                    boundLeft,
                    templateArgumentsSyntax,
                    templateArguments,
                    lookupResult,
                    flags,
                    diagnostics
                );
            }

            BindMemberAccessReportError(node, right, rightName, boundLeft, lookupResult.error, diagnostics);
            return BindMemberAccessBadResult(
                node,
                rightName,
                boundLeft,
                lookupResult.error,
                lookupResult.symbols.ToImmutable(),
                lookupResult.kind
            );
        } finally {
            lookupResult.Free();
        }
    }

    private void LookupInstanceMember(
        LookupResult lookupResult,
        TypeSymbol leftType,
        bool leftIsBaseReference,
        string rightName,
        int rightArity,
        bool called,
        TextLocation errorLocation) {
        var options = LookupOptions.AllMethodsOnArityZero;

        if (called)
            options |= LookupOptions.MustBeInvocableIfMember;

        if (leftIsBaseReference)
            options |= LookupOptions.UseBaseReferenceAccessibility;

        LookupMembersWithFallback(
            lookupResult,
            leftType,
            rightName,
            rightArity,
            errorLocation,
            basesBeingResolved: null,
            options: options
        );
    }

    private BoundExpression BindMemberOfType(
        SyntaxNode node,
        SyntaxNode right,
        string plainName,
        int arity,
        bool indexed,
        BoundExpression left,
        SeparatedSyntaxList<BaseArgumentSyntax> templateArgumentsSyntax,
        AnalyzedArguments templateArguments,
        LookupResult lookupResult,
        BoundMethodGroupFlags methodGroupFlags,
        BelteDiagnosticQueue diagnostics) {
        var members = ArrayBuilder<Symbol>.GetInstance();
        BoundExpression result;
        var symbol = GetSymbolOrMethodGroup(
            lookupResult,
            right,
            plainName,
            arity,
            members,
            diagnostics,
            out var wasError,
            qualifier: left is BoundTypeExpression typeExpr ? typeExpr.Type() : null
        );

        if (symbol is null) {
            result = ConstructBoundMemberGroupAndReportOmittedTemplateArguments(
                node,
                templateArgumentsSyntax,
                templateArguments,
                left,
                plainName,
                members,
                lookupResult,
                methodGroupFlags,
                wasError,
                diagnostics
            );
        } else {
            if (left is not null)
                left = BindToNaturalType(left, diagnostics);

            switch (symbol.kind) {
                case SymbolKind.NamedType:
                case SymbolKind.ErrorType:
                    if (IsInstanceReceiver(left) && !wasError) {
                        diagnostics.Push(Error.NoInstanceRequired(right.location, plainName, symbol));
                        wasError = true;
                    }

                    var type = (NamedTypeSymbol)symbol;

                    if (templateArguments is not null && templateArgumentsSyntax != default) {
                        type = ConstructNamedTypeUnlessTemplateArgumentOmitted(
                            right,
                            type,
                            templateArgumentsSyntax,
                            templateArguments,
                            diagnostics
                        );
                    }

                    result = new BoundTypeExpression(node, new TypeWithAnnotations(type), null, type);
                    break;
                case SymbolKind.Field:
                    result = BindFieldAccess(
                        node,
                        left,
                        (FieldSymbol)symbol,
                        diagnostics,
                        lookupResult.kind,
                        indexed,
                        wasError
                    );

                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(symbol.kind);
            }
        }

        members.Free();
        return result;
    }

    private Symbol GetSymbolOrMethodGroup(
        LookupResult result,
        SyntaxNode node,
        string plainName,
        int arity,
        ArrayBuilder<Symbol> methodGroup,
        BelteDiagnosticQueue diagnostics,
        out bool wasError,
        NamespaceOrTypeSymbol qualifier) {
        node = GetNameSyntax(node) ?? node;
        wasError = false;
        Symbol other = null;

        foreach (var symbol in result.symbols) {
            var kind = symbol.kind;

            if (methodGroup.Count > 0) {
                var existingKind = methodGroup[0].kind;

                if (existingKind != kind) {
                    if (existingKind == SymbolKind.Method) {
                        other = symbol;
                        continue;
                    }

                    other = methodGroup[0];
                    methodGroup.Clear();
                }
            }

            if (kind == SymbolKind.Method)
                methodGroup.Add(symbol);
            else
                other = symbol;
        }

        if ((methodGroup.Count > 0) && methodGroup[0].kind == SymbolKind.Method) {
            if ((methodGroup[0].kind == SymbolKind.Method) || (other is null)) {
                if (result.error is not null) {
                    diagnostics.Push(result.error);
                    wasError = result.error.info.severity == DiagnosticSeverity.Error;
                }

                return null;
            }
        }

        methodGroup.Clear();
        return ResultSymbol(result, plainName, arity, node, diagnostics, out wasError, qualifier);
    }

    private static NameSyntax GetNameSyntax(SyntaxNode syntax) {
        return GetNameSyntax(syntax, out _);
    }

    internal static NameSyntax GetNameSyntax(SyntaxNode syntax, out string nameString) {
        nameString = "";

        while (true) {
            switch (syntax.kind) {
                case SyntaxKind.ParenthesizedExpression:
                    syntax = ((ParenthesisExpressionSyntax)syntax).expression;
                    continue;
                case SyntaxKind.CastExpression:
                    syntax = ((CastExpressionSyntax)syntax).expression;
                    continue;
                case SyntaxKind.MemberAccessExpression:
                    return ((MemberAccessExpressionSyntax)syntax).name;
                default:
                    return syntax as NameSyntax;
            }
        }
    }

    private protected BoundExpression BindFieldAccess(
        SyntaxNode node,
        BoundExpression receiver,
        FieldSymbol fieldSymbol,
        BelteDiagnosticQueue diagnostics,
        LookupResultKind resultKind,
        bool indexed,
        bool hasErrors) {
        var hasError = false;
        var type = fieldSymbol.containingType;
        var isEnumField = fieldSymbol.isStatic && type.IsEnumType();

        if (isEnumField && !type.IsValidEnumType()) {
            throw ExceptionUtilities.Unreachable();
            // Error(diagnostics, ErrorCode.ERR_BindToBogus, node, fieldSymbol);
            // hasError = true;
        }

        if (!hasError && !isEnumField)
            hasError = CheckInstanceOrStatic(node, receiver, fieldSymbol, ref resultKind, diagnostics);

        if (!hasError && fieldSymbol.isFixedSizeBuffer && !isInsideNameof) {
            var receiverType = receiver.type;

            hasError = receiverType is null || !receiverType.isValueType;

            // TODO Do we need these errors?
            // if (!hasError) {
            //     var isFixedStatementExpression = SyntaxFacts.IsFixedStatementExpression(node);

            //     if (IsMoveableVariable(receiver, accessedLocalOrParameterOpt: out _) != isFixedStatementExpression) {
            //         if (!indexed) {
            //             // SPEC C# 7.3: If the fixed size buffer access is the receiver of an element_access_expression,
            //             // E may be either fixed or moveable
            //             CheckFeatureAvailability(node, MessageID.IDS_FeatureIndexingMovableFixedBuffers, diagnostics);
            //         } else {
            //             Error(diagnostics, isFixedStatementExpression ? ErrorCode.ERR_FixedNotNeeded : ErrorCode.ERR_FixedBufferNotFixed, node);
            //             hasErrors = hasError = true;
            //         }
            //     }
            // }

            if (!hasError) {
                hasError = !CheckValueKind(
                    node,
                    receiver,
                    BindValueKind.FixedReceiver,
                    checkingReceiver: false,
                    diagnostics: diagnostics
                );
            }
        }

        ConstantValue constantValueOpt = null;

        if ((fieldSymbol.isConstExpr || (isEnumField && !IsInstanceReceiver(receiver))) && !isInsideNameof) {
            constantValueOpt = fieldSymbol.GetConstantValue(constantFieldsInProgress);

            if ((object)constantValueOpt == (object)ConstantValue.Unset)
                constantValueOpt = null;
        }

        if (!fieldSymbol.isStatic) {
            // WarnOnAccessOfOffDefault(node, receiver, diagnostics);
            // TODO warning?
        }

        if (!IsBadBaseAccess(node, receiver, fieldSymbol, diagnostics))
            CheckReceiverAndRuntimeSupportForSymbolAccess(node, receiver, fieldSymbol, diagnostics);

        var fieldType = (isEnumField && IsInstanceReceiver(receiver))
            ? CorLibrary.GetSpecialType(SpecialType.Bool)
            : fieldSymbol.GetFieldType(fieldsBeingBound).type;

        BoundExpression expr = new BoundFieldAccessExpression(
            node,
            receiver,
            fieldSymbol,
            constantValueOpt,
            fieldType,
            hasError
        );

        if (InEnumMemberInitializer()) {
            NamedTypeSymbol enumType = null;
            if (isEnumField)
                enumType = type;
            else if (constantValueOpt is not null && fieldType.IsEnumType())
                enumType = (NamedTypeSymbol)fieldType;

            if (enumType is not null) {
                var underlyingType = enumType.enumUnderlyingType;
                expr = new BoundCastExpression(
                    node,
                    expr,
                    Conversion.ImplicitNumeric,
                    constantValue: expr.constantValue,
                    type: underlyingType
                );
            }
        }

        return expr;
    }

    private void CheckReceiverAndRuntimeSupportForSymbolAccess(
        SyntaxNode node,
        BoundExpression receiver,
        Symbol symbol,
        BelteDiagnosticQueue diagnostics) {
        // TODO interfaces
        // if (symbol.containingType?.isInterface == true) {
        //     if (symbol.isStatic && (symbol.isAbstract || symbol.isVirtual)) {
        //         if (receiver is not BoundTypeExpression { type: { typeKind: TypeKind.TemplateParameter } }) {
        //             Error(diagnostics, ErrorCode.ERR_BadAbstractStaticMemberAccess, node);
        //             return;
        //         }
        //     }

        //     if (receiver is { type: TemplateParameterSymbol { allowsRefLikeType: true } } &&
        //         IsNotImplementableInstanceMember(symbol)) {
        //         Error(diagnostics, ErrorCode.ERR_BadNonVirtualInterfaceMemberAccessOnAllowsRefLike, node);
        //     } else if (!Compilation.Assembly.RuntimeSupportsDefaultInterfaceImplementation && Compilation.SourceModule != symbol.ContainingModule) {
        //         if (IsNotImplementableInstanceMember(symbol)) {
        //             Error(diagnostics, ErrorCode.ERR_RuntimeDoesNotSupportDefaultInterfaceImplementation, node);
        //         } else {
        //             switch (symbol.DeclaredAccessibility) {
        //                 case Accessibility.Protected:
        //                 case Accessibility.ProtectedOrInternal:
        //                 case Accessibility.ProtectedAndInternal:

        //                     Error(diagnostics, ErrorCode.ERR_RuntimeDoesNotSupportProtectedAccessForInterfaceMember, node);
        //                     break;
        //             }
        //         }
        //     }
        // }

        // static bool IsNotImplementableInstanceMember(Symbol symbol) {
        //     return !symbol.isStatic && !(symbol is TypeSymbol) &&
        //            !symbol.IsImplementableInterfaceMember();
        // }
    }

    private bool InEnumMemberInitializer() {
        var containingType = this.containingType;
        return inFieldInitializer && containingType is not null && containingType.IsEnumType();
    }

    private bool IsBadBaseAccess(
        SyntaxNode node,
        BoundExpression receiver,
        Symbol member,
        BelteDiagnosticQueue diagnostics) {
        if (receiver?.kind == BoundKind.BaseExpression && member.isAbstract) {
            diagnostics.Push(Error.AbstractBaseCall(node.location, member));
            return true;
        }

        return false;
    }

    private bool CheckInstanceOrStatic(
        SyntaxNode node,
        BoundExpression receiver,
        Symbol symbol,
        ref LookupResultKind resultKind,
        BelteDiagnosticQueue diagnostics) {
        var instanceReceiver = IsInstanceReceiver(receiver);

        if (!symbol.RequiresInstanceReceiver()) {
            if (instanceReceiver) {
                if (!isInsideNameof) {
                    if (flags.Includes(BinderFlags.ObjectInitializerMember))
                        diagnostics.Push(Error.StaticMemberInObjectInitializer(node.location, symbol));
                    else
                        diagnostics.Push(Error.NoInstanceRequired(node.location, symbol.name, symbol.containingSymbol));
                } else {
                    return false;
                }

                resultKind = LookupResultKind.StaticInstanceMismatch;
                return true;
            }
        } else {
            if (!instanceReceiver && !isInsideNameof) {
                diagnostics.Push(Error.InstanceRequired(node.location, symbol));
                resultKind = LookupResultKind.StaticInstanceMismatch;
                return true;
            }
        }

        return false;
    }

    private static bool IsInstanceReceiver(BoundExpression receiver) {
        return receiver is not null && receiver.kind != BoundKind.TypeExpression;
    }

    private BoundExpression MakeMemberAccessValue(BoundExpression expression, BelteDiagnosticQueue diagnostics) {
        switch (expression.kind) {
            case BoundKind.MethodGroup: {
                    /*
                        var methodGroup = (BoundMethodGroup)expression;
                        var resolution = ResolveMethodGroup(methodGroup, null);
                        diagnostics.PushRange(resolution.Diagnostics);

                        if (resolution.methodGroup is not null && !resolution.hasAnyErrors) {
                            var method = resolution.methodGroup.methods[0];
                            // Error(diagnostics, ErrorCode.ERR_BadSKunknown, methodGroup.NameSyntax, method, MessageID.IDS_SK_METHOD.Localize());
                            // TODO error
                        }

                        // expression = this.BindMemberAccessBadResult(methodGroup);
                        expression = new BoundErrorExpression(expression.type);
                        resolution.Free();
                        return expression;
                        */
                    // TODO do we even need a special case here?
                    return expression;
                }
            default:
                return BindToNaturalType(expression, diagnostics);
        }
    }

    private BoundExpression BindMethodGroup(
        ExpressionSyntax node,
        bool called,
        bool indexed,
        BelteDiagnosticQueue diagnostics) {
        switch (node.kind) {
            case SyntaxKind.IdentifierName:
            case SyntaxKind.TemplateName:
                return BindIdentifier((SimpleNameSyntax)node, called, indexed, diagnostics);
            case SyntaxKind.MemberAccessExpression:
                return BindMemberAccess((MemberAccessExpressionSyntax)node, called, indexed, diagnostics);
            case SyntaxKind.ParenthesizedExpression:
                return BindMethodGroup(((ParenthesisExpressionSyntax)node).expression, false, false, diagnostics);
            default:
                return BindExpressionInternal(node, diagnostics, called, indexed);
        }
    }

    private BoundExpression BindCallExpression(CallExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        BoundExpression result;
        var analyzedArguments = AnalyzedArguments.GetInstance();

        if (ReceiverIsInvocation(node, out var nested)) {
            var invocations = ArrayBuilder<CallExpressionSyntax>.GetInstance();

            invocations.Push(node);
            node = nested;

            while (ReceiverIsInvocation(node, out nested)) {
                invocations.Push(node);
                node = nested;
            }

            var boundExpression = BindMethodGroup(node.expression, true, false, diagnostics);

            while (true) {
                result = BindArgumentsAndInvocation(node, boundExpression, analyzedArguments, diagnostics);

                if (!invocations.TryPop(out node))
                    break;

                var memberAccess = (MemberAccessExpressionSyntax)node.expression;
                analyzedArguments.Clear();
                boundExpression = BindMemberAccessWithBoundLeft(
                    memberAccess,
                    result,
                    memberAccess.name,
                    memberAccess.operatorToken,
                    true,
                    false,
                    diagnostics
                );
            }

            invocations.Free();
        } else {
            var boundExpression = BindMethodGroup(node.expression, true, false, diagnostics);
            result = BindArgumentsAndInvocation(node, boundExpression, analyzedArguments, diagnostics);
        }

        analyzedArguments.Free();
        return result;

        static bool ReceiverIsInvocation(CallExpressionSyntax node, out CallExpressionSyntax nested) {
            if (node.expression is MemberAccessExpressionSyntax {
                expression: CallExpressionSyntax receiver,
                kind: SyntaxKind.MemberAccessExpression
            }) {
                nested = receiver;
                return true;
            }

            nested = null;
            return false;
        }
    }

    private BoundExpression BindArgumentsAndInvocation(
        CallExpressionSyntax node,
        BoundExpression boundExpression,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        boundExpression = CheckValue(boundExpression, BindValueKind.RValueOrMethodGroup, diagnostics);
        var underlyingExpression = boundExpression is BoundConditionalAccessExpression c
            ? c.accessExpression
            : boundExpression;
        var name = GetName(node.expression);
        BindArgumentsAndNames(node.argumentList, diagnostics, analyzedArguments);

        var call = BindCallExpression(
            node,
            node.expression,
            name,
            underlyingExpression,
            analyzedArguments,
            diagnostics
        );

        if (boundExpression is BoundConditionalAccessExpression cond) {
            return new BoundConditionalAccessExpression(
                cond.syntax,
                cond.receiver,
                call,
                CorLibrary.GetOrCreateNullableType(call.type)
            );
        }

        return call;
    }

    private BoundExpression BindCallExpression(
        SyntaxNode node,
        SyntaxNode expression,
        string methodName,
        BoundExpression boundExpression,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        BoundExpression result;
        if (boundExpression.kind == BoundKind.MethodGroup) {
            result = BindMethodGroupInvocation(
                node,
                expression,
                methodName,
                (BoundMethodGroup)boundExpression,
                analyzedArguments,
                diagnostics
            );
        } else if (boundExpression.Type()?.kind == SymbolKind.FunctionPointerType) {
            result = BindFunctionPointerInvocation(node, boundExpression, analyzedArguments, diagnostics);
        } else if (boundExpression.StrippedType()?.kind == SymbolKind.FunctionType) {
            result = BindFunctionInvocation(
                node,
                expression,
                methodName,
                boundExpression,
                analyzedArguments,
                diagnostics,
                (FunctionTypeSymbol)boundExpression.StrippedType()
            );
        } else {
            if (!boundExpression.hasAnyErrors)
                diagnostics.Push(Error.CannotCallNonMethod(expression.location));

            result = CreateErrorCall(node, boundExpression, LookupResultKind.NotInvocable, analyzedArguments);
        }

        return result;
    }

    private BoundFunctionPointerCallExpression BindFunctionPointerInvocation(
        SyntaxNode node,
        BoundExpression boundExpression,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        boundExpression = BindToNaturalType(boundExpression, diagnostics);

        var funcPtr = (FunctionPointerTypeSymbol)boundExpression.Type();

        var overloadResolutionResult = OverloadResolutionResult<FunctionPointerMethodSymbol>.GetInstance();
        var methodsBuilder = ArrayBuilder<FunctionPointerMethodSymbol>.GetInstance(1);

        methodsBuilder.Add(funcPtr.signature);

        overloadResolution.FunctionPointerOverloadResolution(
            methodsBuilder,
            analyzedArguments,
            overloadResolutionResult
        );

        if (!overloadResolutionResult.succeeded) {
            var methods = methodsBuilder.ToImmutableAndFree();

            overloadResolutionResult.ReportDiagnostics(
                binder: this,
                node.location,
                node: null,
                diagnostics,
                name: null,
                boundExpression,
                boundExpression.syntax,
                analyzedArguments,
                methods,
                typeContainingConstructor: null,
                returnRefKind: funcPtr.signature.refKind
            );

            return new BoundFunctionPointerCallExpression(
                node,
                boundExpression,
                BuildArgumentsForErrorRecovery(analyzedArguments, StaticCast<MethodSymbol>.From(methods)),
                analyzedArguments.refKinds.ToImmutableOrNull(),
                LookupResultKind.OverloadResolutionFailure,
                funcPtr.signature.returnType,
                hasErrors: true
            );
        }

        methodsBuilder.Free();

        var methodResult = overloadResolutionResult.bestResult;
        CheckAndCoerceArguments(node, methodResult, analyzedArguments, diagnostics, receiver: null, argsToParams: out _);

        var args = analyzedArguments.arguments.Select(a => a.expression).ToImmutableArray();
        var refKinds = analyzedArguments.refKinds.ToImmutableOrNull();

        return new BoundFunctionPointerCallExpression(
            node,
            boundExpression,
            args,
            refKinds,
            LookupResultKind.Viable,
            funcPtr.signature.returnType,
            false
        );
    }

    private BoundExpression BindFunctionInvocation(
        SyntaxNode node,
        SyntaxNode expression,
        string methodName,
        BoundExpression boundExpression,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics,
        FunctionTypeSymbol functionType) {
        BoundExpression result;
        var methodGroup = MethodGroup.GetInstance();
        methodGroup.PopulateWithSingleMethod(boundExpression, functionType.signature);
        var overloadResolutionResult = OverloadResolutionResult<MethodSymbol>.GetInstance();

        overloadResolution.MethodOverloadResolution(
            members: methodGroup.methods,
            templateArguments: methodGroup.templateArguments,
            receiver: methodGroup.receiver,
            arguments: analyzedArguments,
            result: overloadResolutionResult
        );

        result = BindCallExpressionContinued(
            node,
            expression,
            methodName,
            overloadResolutionResult,
            analyzedArguments,
            methodGroup,
            functionType,
            diagnostics
        );

        overloadResolutionResult.Free();
        methodGroup.Free();
        return result;
    }

    internal MethodGroupResolution ResolveMethodGroup(
        BoundMethodGroup node,
        AnalyzedArguments analyzedArguments,
        RefKind returnRefKind = default,
        TypeSymbol returnType = null,
        bool isMethodGroupConversion = false) {
        var methodResolution = ResolveDefaultMethodGroup(
            node,
            analyzedArguments,
            returnRefKind,
            returnType,
            isMethodGroupConversion
        );

        if (methodResolution.isEmpty && !methodResolution.hasAnyErrors) {
            var diagnostics = BelteDiagnosticQueue.GetInstance();
            diagnostics.PushRange(methodResolution.diagnostics);

            BindMemberAccessReportError(
                node.memberAccessExpressionSyntax ?? node.nameSyntax,
                node.nameSyntax,
                node.name,
                node.receiver,
                node.lookupError,
                diagnostics
            );

            return new MethodGroupResolution(
                methodResolution.methodGroup,
                methodResolution.otherSymbol,
                methodResolution.overloadResolutionResult,
                methodResolution.analyzedArguments,
                methodResolution.resultKind,
                diagnostics
            );
        }

        return methodResolution;
    }

    private MethodGroupResolution ResolveDefaultMethodGroup(
        BoundMethodGroup node,
        AnalyzedArguments analyzedArguments,
        RefKind returnRefKind = default,
        TypeSymbol returnType = null,
        bool isMethodGroupConversion = false) {
        var methods = node.methods;

        if (methods.Length == 0) {
            if (node.lookupSymbol is MethodSymbol method)
                methods = [method];
        }

        var sealedDiagnostics = BelteDiagnosticQueue.Discarded;

        if (node.lookupError is not null) {
            sealedDiagnostics = BelteDiagnosticQueue.GetInstance();
            sealedDiagnostics.Push(node.lookupError);
        }

        if (methods.Length == 0)
            return new MethodGroupResolution(node.lookupSymbol, node.resultKind, sealedDiagnostics);

        var methodGroup = MethodGroup.GetInstance();
        methodGroup.PopulateWithNonExtensionMethods(
            node.receiver,
            methods,
            node.templateArguments,
            node.resultKind,
            node.lookupError
        );

        if (node.lookupError is not null)
            return new MethodGroupResolution(methodGroup, sealedDiagnostics);

        if (analyzedArguments is null) {
            return new MethodGroupResolution(methodGroup, sealedDiagnostics);
        } else {
            var result = OverloadResolutionResult<MethodSymbol>.GetInstance();

            overloadResolution.MethodOverloadResolution(
                methodGroup.methods,
                methodGroup.templateArguments,
                methodGroup.receiver,
                analyzedArguments,
                result,
                isMethodGroupConversion,
                returnRefKind,
                returnType
            );

            return new MethodGroupResolution(
                methodGroup,
                null,
                result,
                AnalyzedArguments.GetInstance(analyzedArguments),
                methodGroup.resultKind,
                sealedDiagnostics
            );
        }
    }

    private BoundExpression BindMethodGroupInvocation(
        SyntaxNode syntax,
        SyntaxNode expression,
        string methodName,
        BoundMethodGroup methodGroup,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics) {
        var resolution = ResolveMethodGroup(methodGroup, analyzedArguments);

        if (!methodGroup.hasAnyErrors)
            diagnostics.PushRange(resolution.diagnostics);

        BoundExpression result;
        if (resolution.hasAnyErrors) {
            LookupResultKind resultKind;

            if (resolution.overloadResolutionResult is not null) {
                // TODO Also find originalMethods and typeArguments to add to the bad call?
                resultKind = resolution.methodGroup.resultKind;
            } else {
                resultKind = methodGroup.resultKind;
            }

            result = CreateErrorCall(syntax, methodGroup.receiver, resultKind, analyzedArguments);
        } else if (!resolution.isEmpty) {
            if (resolution.resultKind != LookupResultKind.Viable) {
                if (resolution.methodGroup is not null) {
                    BindCallExpressionContinued(
                        syntax,
                        expression,
                        methodName,
                        resolution.overloadResolutionResult,
                        resolution.analyzedArguments,
                        resolution.methodGroup,
                        null,
                        BelteDiagnosticQueue.Discarded
                    );
                }

                result = CreateErrorCall(syntax, methodGroup, methodGroup.resultKind, analyzedArguments);
            } else {
                result = BindCallExpressionContinued(
                    syntax,
                    expression,
                    methodName,
                    resolution.overloadResolutionResult,
                    resolution.analyzedArguments,
                    resolution.methodGroup,
                    null,
                    diagnostics
                );
            }
        } else {
            result = CreateErrorCall(syntax, methodGroup, methodGroup.resultKind, analyzedArguments);
        }

        resolution.Free();
        return result;
    }

    private BoundCallExpression BindCallExpressionContinued(
        SyntaxNode node,
        SyntaxNode expression,
        string methodName,
        OverloadResolutionResult<MethodSymbol> result,
        AnalyzedArguments analyzedArguments,
        MethodGroup methodGroup,
        FunctionTypeSymbol functionType,
        BelteDiagnosticQueue diagnostics) {
        if (!result.succeeded) {
            if (analyzedArguments.anyErrors) {
                foreach (var argument in analyzedArguments.arguments) {
                    if (argument.isExpression) {
                        switch (argument.expression) {
                            case UnboundLambda unboundLambda:
                                // var boundWithErrors = unboundLambda.BindForErrorRecovery();
                                // diagnostics.AddRange(boundWithErrors.Diagnostics);
                                break;
                            case BoundUnconvertedObjectCreationExpression _:
                                _ = BindToNaturalType(argument.expression, diagnostics);
                                break;
                        }
                    }
                }
            } else {
                result.ReportDiagnostics(
                    this,
                    GetLocationForOverloadResolutionDiagnostic(node, expression),
                    node,
                    diagnostics,
                    methodName,
                    methodGroup.receiver,
                    expression,
                    analyzedArguments,
                    methodGroup.methods.ToImmutable(),
                    null,
                    false,
                    functionTypeSymbol: functionType
                );
            }

            return CreateErrorCall(node, methodGroup.receiver, methodGroup.resultKind, analyzedArguments);
        }

        var methodResult = result.bestResult;
        var returnType = methodResult.member.returnType;
        var method = methodResult.member;
        var receiver = methodGroup.receiver;

        CheckAndCoerceArguments(node, methodResult, analyzedArguments, diagnostics, receiver, out var argsToParams);
        BindDefaultArguments(
            node,
            method.parameters,
            analyzedArguments.arguments,
            analyzedArguments.refKinds,
            analyzedArguments.names,
            ref argsToParams,
            out var defaultArguments,
            true,
            diagnostics
        );

        var gotError = MemberGroupFinalValidation(receiver, method, expression, diagnostics);

        // TODO what is this error
        // CheckImplicitThisCopyInReadOnlyMember(receiver, method, diagnostics);

        // This will be the receiver of the BoundCall node that we create.
        // For extension methods, there is no receiver because the receiver in source was actually the first argument.
        // For instance methods, we may have synthesized an implicit this node.  We'll keep it for the emitter.
        // For static methods, we may have synthesized a type expression.  It serves no purpose, so we'll drop it.
        // TODO how to check for compiler generation?
        if (!method.requiresInstanceReceiver && receiver is not null /*&& receiver.WasCompilerGenerated*/)
            receiver = null;

        // TODO how to check for compiler generation?
        if (!gotError && method.requiresInstanceReceiver && receiver is not null && receiver.kind == BoundKind.ThisExpression /*&& receiver.WasCompilerGenerated*/) {
            gotError = IsRefOrOutThisParameterCaptured(node, diagnostics);
        }

        (var args, var argRefKinds) = RearrangeArguments(
            analyzedArguments.arguments,
            analyzedArguments.refKinds,
            argsToParams
        );

        return new BoundCallExpression(
            node,
            receiver,
            method,
            args.Select(a => a.expression).ToImmutableArray(),
            argRefKinds,
            defaultArguments,
            LookupResultKind.Viable,
            returnType,
            gotError
        );
    }

    private static (ImmutableArray<T>, ImmutableArray<RefKind>) RearrangeArguments<T>(
        ArrayBuilder<T> arguments,
        ArrayBuilder<RefKind> refKinds,
        ImmutableArray<int> argsToParams) {
        ImmutableArray<T> args;
        ImmutableArray<RefKind> argRefKinds;

        if (argsToParams.IsDefault) {
            args = arguments.ToImmutable();
            argRefKinds = refKinds.ToImmutableOrNull();
        } else {
            // Could rearrange the arguments during lowering,
            // but this prevents any issues with walking the lowerer multiple times
            var argCount = arguments.Count;
            var argRefKindCount = refKinds.Count;

            var argsBuilder = new T[argCount];
            var argRefKindsBuilder = new RefKind[argCount];

            for (var i = 0; i < argsToParams.Length; i++) {
                var target = argsToParams[i];
                argsBuilder[target] = arguments[i];

                if (i < argRefKindCount)
                    argRefKindsBuilder[target] = refKinds[i];
            }

            args = argsBuilder.ToImmutableArray();
            argRefKinds = argRefKindCount == 0 ? default : argRefKindsBuilder.ToImmutableArray();
        }

        return (args, argRefKinds);
    }

    private bool MemberGroupFinalValidation(
        BoundExpression receiver,
        MethodSymbol methodSymbol,
        SyntaxNode node,
        BelteDiagnosticQueue diagnostics) {
        IsBadBaseAccess(node, receiver, methodSymbol, diagnostics);

        if (MemberGroupFinalValidationAccessibilityChecks(receiver, methodSymbol, node, diagnostics))
            return true;

        if (!methodSymbol.isEffectivelyConst) {
            if (flags.Includes(BinderFlags.ConstContext) && IsThisInstanceAccess(receiver)) {
                diagnostics.Push(Error.NonConstantCallInConstant(node.location, methodSymbol));
                return true;
            }

            var receiverSymbol = receiver?.expressionSymbol;

            if ((receiverSymbol is DataContainerSymbol local && (local.isConst || local.isConstExpr)) ||
                (receiverSymbol is FieldSymbol field && (field.isConst || field.isConstExpr)) ||
                (receiverSymbol is ParameterSymbol parameter && parameter.isConst)) {
                diagnostics.Push(Error.NonConstantCallOnConstant(node.location, methodSymbol));
                return true;
            }
        }

        return !methodSymbol.CheckMethodConstraints(node.location, diagnostics);
    }

    private static bool IsMemberAccessedThroughVariableOrValue(BoundExpression receiver) {
        if (receiver is null)
            return false;

        return !IsMemberAccessedThroughType(receiver);
    }

    private bool MemberGroupFinalValidationAccessibilityChecks(
        BoundExpression receiver,
        Symbol memberSymbol,
        SyntaxNode node,
        BelteDiagnosticQueue diagnostics) {
        if (receiver is not null || memberSymbol is not MethodSymbol { methodKind: MethodKind.Constructor }) {
            if (!memberSymbol.RequiresInstanceReceiver()) {
                if (!WasImplicitReceiver(receiver) && IsMemberAccessedThroughVariableOrValue(receiver)) {
                    diagnostics.Push(Error.NoInstanceRequired(
                        node.location,
                        memberSymbol.name,
                        memberSymbol.containingSymbol
                    ));

                    return true;
                }
            } else if (IsMemberAccessedThroughType(receiver)) {
                diagnostics.Push(Error.InstanceRequired(node.location, memberSymbol));
                return true;
            } else if (WasImplicitReceiver(receiver)) {
                if (inFieldInitializer || _inConstructorInitializer) {
                    var errorNode = node;

                    if (node.parent is not null && node.parent.kind == SyntaxKind.CallExpression)
                        errorNode = node.parent;

                    if (inFieldInitializer)
                        diagnostics.Push(Error.InstanceRequiredInFieldInitializer(errorNode.location, memberSymbol));
                    else
                        diagnostics.Push(Error.InstanceRequired(errorNode.location, memberSymbol));

                    return true;
                }

                if (receiver is null || containingMember.isStatic) {
                    diagnostics.Push(Error.InstanceRequired(node.location, memberSymbol));
                    return true;
                }
            }
        }

        var containingType = this.containingType;

        if (containingType is not null) {
            var isAccessible = IsSymbolAccessibleConditional(memberSymbol.GetTypeOrReturnType().type, containingType);

            if (!isAccessible) {
                diagnostics.Push(Error.MemberIsInaccessible(node.location, memberSymbol));
                return true;
            }
        }

        return false;
    }

    private void CheckAndCoerceArguments<TMember>(
        SyntaxNode node,
        MemberResolutionResult<TMember> methodResult,
        AnalyzedArguments analyzedArguments,
        BelteDiagnosticQueue diagnostics,
        BoundExpression receiver,
        out ImmutableArray<int> argsToParams)
        where TMember : Symbol {
        var result = methodResult.result;
        var arguments = analyzedArguments.arguments;
        var parameters = methodResult.leastOverriddenMember.GetParameters();

        for (var arg = 0; arg < arguments.Count; arg++) {
            var argument = arguments[arg];

            if (!analyzedArguments.hasErrors[arg]) {
                var argRefKind = analyzedArguments.RefKind(arg);
                var argNumber = arg + 1;

                // Warn for `ref`/`in` or None/`ref readonly` mismatch.
                if (argRefKind == RefKind.Ref) {
                } else if (argRefKind == RefKind.None &&
                    GetCorrespondingParameter(in result, parameters, arg)
                        .refKind is RefKind.RefConst or RefKind.RefFinal &&
                    argument.isExpression) {
                    var syntax = analyzedArguments.syntaxes[arg];

                    if (!CheckValueKind(
                        syntax,
                        argument.expression,
                        BindValueKind.RefersToLocation,
                        checkingReceiver: false,
                        BelteDiagnosticQueue.Discarded)) {
                        diagnostics.Push(Warning.RefConstNotVariable(syntax.location, argNumber));
                    } else if (arg != 0) {
                        if (CheckValueKind(
                            syntax,
                            argument.expression,
                            BindValueKind.Assignable,
                            checkingReceiver: false,
                            BelteDiagnosticQueue.Discarded)) {
                            diagnostics.Push(Warning.ArgExpectedRef(syntax.location, argNumber));
                        } else {
                            // TODO Reachable?
                            throw ExceptionUtilities.Unreachable();
                            // Argument {0} should be passed with the 'in' keyword
                            // diagnostics.Add(
                            //     ErrorCode.WRN_ArgExpectedIn,
                            //     argument.Syntax,
                            //     argNumber);
                        }
                    }
                }
            }

            var paramNum = result.ParameterFromArgument(arg);

            if (argument.isExpression) {
                arguments[arg] = CoerceArgument(
                    in methodResult,
                    receiver,
                    parameters,
                    argument.expression,
                    arg,
                    parameters[paramNum].typeWithAnnotations,
                    diagnostics
                );
            } else {
                arguments[arg] = argument;
            }
        }

        argsToParams = result.argsToParams;
        return;

        BoundExpressionOrTypeOrConstant CoerceArgument(
            in MemberResolutionResult<TMember> methodResult,
            BoundExpression receiver,
            ImmutableArray<ParameterSymbol> parameters,
            BoundExpression argument,
            int arg,
            TypeWithAnnotations parameterTypeWithAnnotations,
            BelteDiagnosticQueue diagnostics) {
            var result = methodResult.result;
            var kind = result.ConversionForArg(arg);
            argument = ReduceNumericIfApplicable(parameterTypeWithAnnotations.type, argument);
            var coercedArgument = argument;

            if (!kind.isIdentity || argument.kind == BoundKind.UnconvertedImplicitEnumFieldExpression) {
                coercedArgument = CreateConversion(
                    argument.syntax,
                    argument,
                    kind,
                    isCast: false,
                    parameterTypeWithAnnotations.type,
                    diagnostics
                );
            } else if (argument.kind == BoundKind.OutVariablePendingInference) {
                coercedArgument = ((OutVariablePendingInference)argument)
                    .SetInferredTypeWithAnnotations(parameterTypeWithAnnotations, diagnostics);
            } else if (argument.kind == BoundKind.DiscardExpression && !argument.HasExpressionType()) {
                coercedArgument = ((BoundDiscardExpression)argument)
                    .SetInferredTypeWithAnnotations(parameterTypeWithAnnotations);
            } else if (argument.NeedsToBeConverted()) {
                coercedArgument = BindToNaturalType(argument, diagnostics);
            }

            return new BoundExpressionOrTypeOrConstant(coercedArgument);
        }

        static ParameterSymbol GetCorrespondingParameter(
            in MemberAnalysisResult result,
            ImmutableArray<ParameterSymbol> parameters,
            int arg) {
            var paramNum = result.ParameterFromArgument(arg);
            return parameters[paramNum];
        }
    }

    internal static ParameterSymbol? GetCorrespondingParameter(
        int argumentOrdinal,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<int> argsToParamsOpt) {
        var n = parameters.Length;
        ParameterSymbol parameter;

        if (argsToParamsOpt.IsDefault) {
            if (argumentOrdinal < n)
                parameter = parameters[argumentOrdinal];
            else
                parameter = null;
        } else {
            var parameterOrdinal = argsToParamsOpt[argumentOrdinal];

            if (parameterOrdinal < n)
                parameter = parameters[parameterOrdinal];
            else
                parameter = null;
        }

        return parameter;
    }

    internal void BindDefaultArguments(
        SyntaxNode node,
        ImmutableArray<ParameterSymbol> parameters,
        ArrayBuilder<BoundExpressionOrTypeOrConstant> argumentsBuilder,
        ArrayBuilder<RefKind>? argumentRefKindsBuilder,
        ArrayBuilder<(string Name, TextLocation Location)?>? namesBuilder,
        ref ImmutableArray<int> argsToParams,
        out BitVector defaultArguments,
        bool enableCallerInfo,
        BelteDiagnosticQueue diagnostics) {
        var paramsIndex = parameters.Length - 1;
        var visitedParameters = BitVector.Create(parameters.Length);

        for (var i = 0; i < argumentsBuilder.Count; i++) {
            var parameter = GetCorrespondingParameter(i, parameters, argsToParams);

            if (parameter is not null)
                visitedParameters[parameter.ordinal] = true;
        }

        var haveDefaultArguments = !parameters.All(
            static (param, visitedParameters) => visitedParameters[param.ordinal], visitedParameters
        );

        if (!haveDefaultArguments) {
            defaultArguments = default;
            return;
        }

        ArrayBuilder<int>? argsToParamsBuilder = null;
        if (!argsToParams.IsDefault) {
            argsToParamsBuilder = ArrayBuilder<int>.GetInstance(argsToParams.Length);
            argsToParamsBuilder.AddRange(argsToParams);
        }

        if (haveDefaultArguments) {
            var containingMember = this.containingMember;
            defaultArguments = BitVector.Create(parameters.Length);
            var lastIndex = ^0;
            var argumentsCount = argumentsBuilder.Count;

            foreach (var parameter in parameters.AsSpan()[..lastIndex]) {
                if (!visitedParameters[parameter.ordinal]) {
                    defaultArguments[argumentsBuilder.Count] = true;
                    argumentsBuilder.Add(new BoundExpressionOrTypeOrConstant(BindDefaultArgument(
                        node,
                        parameter,
                        containingMember,
                        enableCallerInfo,
                        diagnostics,
                        argumentsBuilder,
                        argumentsCount,
                        argsToParams
                    )));

                    if (argumentRefKindsBuilder is { Count: > 0 })
                        argumentRefKindsBuilder.Add(RefKind.None);

                    argsToParamsBuilder?.Add(parameter.ordinal);

                    if (namesBuilder?.Count > 0)
                        namesBuilder.Add(null);
                }
            }
        } else {
            defaultArguments = default;
        }

        if (argsToParamsBuilder is not null) {
            argsToParams = argsToParamsBuilder.ToImmutableOrNull();
            argsToParamsBuilder.Free();
        }

        BoundExpression BindDefaultArgument(
            SyntaxNode syntax,
            ParameterSymbol parameter,
            Symbol containingMember,
            bool enableCallerInfo,
            BelteDiagnosticQueue diagnostics,
            ArrayBuilder<BoundExpressionOrTypeOrConstant> argumentsBuilder,
            int argumentsCount,
            ImmutableArray<int> argsToParamsOpt) {
            var parameterType = parameter.type;

            if (flags.Includes(BinderFlags.ParameterDefaultValue))
                return new BoundDefaultExpression(syntax, false, null, null, parameterType);

            var parameterDefaultValue = parameter.explicitDefaultConstantValue;
            var defaultConstantValue = parameterDefaultValue?.value;
            // var callerSourceLocation = enableCallerInfo ? GetCallerLocation(syntax) : null;
            BoundExpression defaultValue;

            // TODO The [CallerLineNumber] attribute is neat
            // if (callerSourceLocation is object && parameter.IsCallerLineNumber) {
            //     int line = callerSourceLocation.SourceTree.GetDisplayLineNumber(callerSourceLocation.SourceSpan);
            //     defaultValue = new BoundLiteral(syntax, ConstantValue.Create(line), Compilation.GetSpecialType(SpecialType.System_Int32)) { WasCompilerGenerated = true };
            // } else if (callerSourceLocation is object && parameter.IsCallerFilePath) {
            //     string path = callerSourceLocation.SourceTree.GetDisplayPath(callerSourceLocation.SourceSpan, Compilation.Options.SourceReferenceResolver);
            //     defaultValue = new BoundLiteral(syntax, ConstantValue.Create(path), Compilation.GetSpecialType(SpecialType.System_String)) { WasCompilerGenerated = true };
            // } else if (callerSourceLocation is object && parameter.IsCallerMemberName && containingMember is not null) {
            //     var memberName = containingMember.GetMemberCallerName();
            //     defaultValue = new BoundLiteral(syntax, ConstantValue.Create(memberName), Compilation.GetSpecialType(SpecialType.System_String)) { WasCompilerGenerated = true };
            // } else if (callerSourceLocation is object
            //       && !parameter.IsCallerMemberName
            //       && Conversions.ClassifyBuiltInConversion(Compilation.GetSpecialType(SpecialType.System_String), parameterType, isChecked: false, ref discardedUseSiteInfo).Exists
            //       && getArgumentIndex(parameter.CallerArgumentExpressionParameterIndex, argsToParamsOpt) is int argumentIndex
            //       && argumentIndex > -1 && argumentIndex < argumentsCount) {
            //     var argument = argumentsBuilder[argumentIndex];
            //     defaultValue = new BoundLiteral(syntax, ConstantValue.Create(argument.Syntax.ToString()), Compilation.GetSpecialType(SpecialType.System_String)) { WasCompilerGenerated = true };

            if (defaultConstantValue is null) {
                defaultValue = new BoundDefaultExpression(syntax, false, null, null, parameterType);
            } else {
                TypeSymbol constantType = CorLibrary.GetSpecialType(parameterDefaultValue.specialType);
                defaultValue = new BoundLiteralExpression(syntax, parameterDefaultValue, constantType);
            }

            var conversion = conversions.ClassifyConversionFromExpression(defaultValue, parameterType);

            if (!conversion.exists)
                GenerateImplicitConversionError(diagnostics, syntax, conversion, defaultValue, parameterType);

            var isCast = conversion.isExplicit;
            defaultValue = CreateConversion(
                defaultValue.syntax,
                defaultValue,
                conversion,
                isCast,
                parameterType,
                diagnostics
            );

            return defaultValue;

            // static int GetArgumentIndex(int parameterIndex, ImmutableArray<int> argsToParamsOpt)
            //     => argsToParamsOpt.IsDefault
            //         ? parameterIndex
            //         : argsToParamsOpt.IndexOf(parameterIndex);
        }
    }

    private static TextLocation GetLocationForOverloadResolutionDiagnostic(SyntaxNode node, SyntaxNode expression) {
        if (node != expression) {
            switch (expression.kind) {
                case SyntaxKind.QualifiedName:
                    return ((QualifiedNameSyntax)expression).right.location;
                case SyntaxKind.MemberAccessExpression:
                    return ((MemberAccessExpressionSyntax)expression).name.location;
            }
        }

        return expression.location;
    }

    private static ImmutableArray<MethodSymbol> GetOriginalMethods(
        OverloadResolutionResult<MethodSymbol> overloadResolutionResult) {
        if (overloadResolutionResult is null)
            return [];

        var builder = ArrayBuilder<MethodSymbol>.GetInstance();

        foreach (var result in overloadResolutionResult.results)
            builder.Add(result.member);

        return builder.ToImmutableAndFree();
    }

    private static bool IsUnboundTemplate(MethodSymbol method) {
        return method.isTemplateMethod && method.constructedFrom == method;
    }

    private BoundCallExpression CreateErrorCall(
        SyntaxNode node,
        BoundExpression expression,
        LookupResultKind resultKind,
        AnalyzedArguments analyzedArguments) {
        TypeSymbol returnType = new ExtendedErrorTypeSymbol(compilation, "", arity: 0, error: null);
        var methodContainer = expression?.Type() ?? containingType;
        MethodSymbol method = new ErrorMethodSymbol(methodContainer, returnType, "");

        var args = BuildArgumentsForErrorRecovery(analyzedArguments);
        var argRefKinds = analyzedArguments.refKinds.ToImmutableOrNull();

        return new BoundCallExpression(
            node,
            expression,
            method,
            args,
            argRefKinds,
            default,
            resultKind,
            method.returnType,
            true
        );
    }

    private BoundCallExpression CreateErrorCall(
        SyntaxNode node,
        string name,
        BoundExpression receiver,
        ImmutableArray<MethodSymbol> methods,
        LookupResultKind resultKind,
        ImmutableArray<TypeOrConstant> templateArguments,
        AnalyzedArguments analyzedArguments) {
        MethodSymbol method;
        ImmutableArray<BoundExpression> args;

        if (!templateArguments.IsDefaultOrEmpty) {
            var constructedMethods = ArrayBuilder<MethodSymbol>.GetInstance();

            foreach (var m in methods) {
                constructedMethods.Add(m.constructedFrom == m && m.arity == templateArguments.Length
                    ? m.Construct(templateArguments)
                    : m
                );
            }

            methods = constructedMethods.ToImmutableAndFree();
        }

        if (methods.Length == 1 && !IsUnboundTemplate(methods[0])) {
            method = methods[0];
        } else {
            var returnType = GetCommonTypeOrReturnType(methods)
                ?? new ExtendedErrorTypeSymbol(compilation, "", arity: 0, error: null);
            var methodContainer = receiver is not null && receiver.type is not null
                ? receiver.Type()
                : containingType;
            method = new ErrorMethodSymbol(methodContainer, returnType, name);
        }

        args = BuildArgumentsForErrorRecovery(analyzedArguments, methods);
        var argRefKinds = analyzedArguments.refKinds.ToImmutableOrNull();
        receiver = BindToTypeForErrorRecovery(receiver);

        return new BoundCallExpression(
            node,
            receiver,
            method,
            args,
            argRefKinds,
            default,
            resultKind,
            method.returnType,
            true
        );
    }

    private static TypeSymbol GetCommonTypeOrReturnType<TMember>(ImmutableArray<TMember> members)
        where TMember : Symbol {
        TypeSymbol type = null;

        for (int i = 0, n = members.Length; i < n; i++) {
            var returnType = members[i].GetTypeOrReturnType().type;

            if (type is null)
                type = returnType;
            else if (!TypeSymbol.Equals(type, returnType, TypeCompareKind.ConsiderEverything))
                return null;
        }

        return type;
    }

    private ImmutableArray<BoundExpression> BuildArgumentsForErrorRecovery(
        AnalyzedArguments analyzedArguments,
        ImmutableArray<MethodSymbol> methods) {
        var parameterListList = ArrayBuilder<ImmutableArray<ParameterSymbol>>.GetInstance();

        foreach (var m in methods) {
            if (!IsUnboundTemplate(m) && m.parameterCount > 0) {
                parameterListList.Add(m.parameters);

                if (parameterListList.Count == MaxParameterListsForErrorRecovery)
                    break;
            }
        }

        var result = BuildArgumentsForErrorRecovery(analyzedArguments, parameterListList);
        parameterListList.Free();
        return result;
    }

    private ImmutableArray<BoundExpression> BuildArgumentsForErrorRecovery(
        AnalyzedArguments analyzedArguments) {
        return BuildArgumentsForErrorRecovery(analyzedArguments, Enumerable.Empty<ImmutableArray<ParameterSymbol>>());
    }

    private ImmutableArray<BoundExpression> BuildArgumentsForErrorRecovery(
        AnalyzedArguments analyzedArguments,
        IEnumerable<ImmutableArray<ParameterSymbol>> parameterListList) {
        var argumentCount = analyzedArguments.arguments.Count;
        var newArguments = ArrayBuilder<BoundExpression>.GetInstance(argumentCount);
        newArguments.AddRange(analyzedArguments.arguments.Select(a => a.expression));

        for (var i = 0; i < argumentCount; i++) {
            var argument = newArguments[i];

            switch (argument.kind) {
                case BoundKind.ParameterExpression:
                case BoundKind.DataContainerExpression:
                    newArguments[i] = BindToTypeForErrorRecovery(argument);
                    break;
                case BoundKind.OutVariablePendingInference:
                case BoundKind.DiscardExpression:
                    if (argument.HasExpressionType())
                        break;

                    var candidateType = GetCorrespondingParameterTypeLocal(i);

                    if (argument.kind == BoundKind.OutVariablePendingInference) {
                        if (candidateType is null) {
                            newArguments[i] = ((OutVariablePendingInference)argument).FailInference(this, null);
                        } else {
                            newArguments[i] = ((OutVariablePendingInference)argument)
                                .SetInferredTypeWithAnnotations(new TypeWithAnnotations(candidateType), null);
                        }
                    } else {
                        if (candidateType is null) {
                            newArguments[i] = ((BoundDiscardExpression)argument).FailInference(this, null);
                        } else {
                            newArguments[i] = ((BoundDiscardExpression)argument)
                                .SetInferredTypeWithAnnotations(new TypeWithAnnotations(candidateType));
                        }
                    }

                    break;
                default:
                    newArguments[i] = BindToTypeForErrorRecovery(argument, GetCorrespondingParameterTypeLocal(i));
                    break;
            }
        }

        return newArguments.ToImmutableAndFree();

        TypeSymbol GetCorrespondingParameterTypeLocal(int i) {
            TypeSymbol candidateType = null;

            foreach (var parameterList in parameterListList) {
                var parameterType = GetCorrespondingParameterType(analyzedArguments, i, parameterList);

                if (parameterType is not null) {
                    if (candidateType is null) {
                        candidateType = parameterType;
                    } else if (!candidateType.Equals(
                        parameterType,
                        TypeCompareKind.IgnoreArraySizesAndLowerBounds)) {
                        candidateType = null;
                        break;
                    }
                }
            }

            return candidateType;
        }
    }

    private void SetInferredTypes(
        ArrayBuilder2<DeconstructionVariable> variables,
        ImmutableArray<TypeSymbol> foundTypes,
        BelteDiagnosticQueue diagnostics) {
        var matchCount = Math.Min(variables.Count, foundTypes.Length);

        for (var i = 0; i < matchCount; i++) {
            var variable = variables[i];

            if (variable.single is { } pending) {
                if (pending.type is not null)
                    continue;

                variables[i] = new DeconstructionVariable(
                    SetInferredType(pending, foundTypes[i], diagnostics),
                    variable.syntax
                );
            }
        }
    }

    private BoundExpression SetInferredType(
        BoundExpression expression,
        TypeSymbol type,
        BelteDiagnosticQueue diagnostics) {
        switch (expression.kind) {
            case BoundKind.DeconstructionVariablePendingInference: {
                    var pending = (DeconstructionVariablePendingInference)expression;
                    return pending.SetInferredTypeWithAnnotations(new TypeWithAnnotations(type), this, diagnostics);
                }
            case BoundKind.DiscardExpression: {
                    var pending = (BoundDiscardExpression)expression;
                    return pending.SetInferredTypeWithAnnotations(new TypeWithAnnotations(type));
                }
            default:
                throw ExceptionUtilities.UnexpectedValue(expression.kind);
        }
    }

    private static TypeSymbol GetCorrespondingParameterType(
        AnalyzedArguments analyzedArguments,
        int i,
        ImmutableArray<ParameterSymbol> parameterList) {
        var name = analyzedArguments.Name(i);

        if (name is not null) {
            foreach (var parameter in parameterList) {
                if (parameter.name == name)
                    return parameter.type;
            }

            return null;
        }

        return (i < parameterList.Length) ? parameterList[i].type : null;
    }

    private void BindArgumentsAndNames(
        BaseArgumentListSyntax argumentList,
        BelteDiagnosticQueue diagnostics,
        AnalyzedArguments result) {
        if (argumentList is null)
            return;

        var hadError = false;

        foreach (var argumentSyntax in argumentList.arguments)
            BindArgumentAndName(result, diagnostics, ref hadError, argumentSyntax);
    }

    private void BindArgumentAndName(
        AnalyzedArguments result,
        BelteDiagnosticQueue diagnostics,
        ref bool hadError,
        BaseArgumentSyntax argumentSyntax) {
        RefKind refKind;
        BoundExpression boundArgument;
        SyntaxToken identifier;

        if (argumentSyntax is OmittedArgumentSyntax omitted) {
            refKind = RefKind.None;
            identifier = null;
            boundArgument = new BoundLiteralExpression(omitted, ConstantValue.Null, null);
        } else if (argumentSyntax is ArgumentSyntax normal) {
            refKind = normal.refKindKeyword is null ? RefKind.None :
                normal.refKindKeyword.kind == SyntaxKind.RefKeyword
                    ? RefKind.Ref
                    : RefKind.Out;

            identifier = normal.identifier;
            boundArgument = BindArgumentValue(normal, diagnostics, refKind);
            refKind = InferBoundArgumentRefKind(boundArgument, refKind);
        } else {
            throw ExceptionUtilities.Unreachable();
        }

        // TODO Why did we make this restriction?
        // if (compilation.options.isScript && refKind != RefKind.None &&
        //     boundArgument is BoundDataContainerExpression d && d.dataContainer.isGlobal) {
        //     diagnostics.Push(Error.CannotPassGlobalByRef(argumentSyntax.location));
        //     hadError = true;
        // }

        BindArgumentAndName(
            result,
            diagnostics,
            argumentSyntax,
            boundArgument,
            identifier,
            refKind
        );

        static RefKind InferBoundArgumentRefKind(BoundExpression boundArgument, RefKind refKind) {
            if (refKind is RefKind.None or RefKind.Out)
                return refKind;

            var argRefKind = boundArgument.GetRefKind();
            var argIsConst = boundArgument.IsConst();

            if (argRefKind != RefKind.None)
                return argRefKind;

            if (argIsConst)
                return RefKind.RefConst;

            return refKind;
        }
    }

    private BoundExpression BindArgumentValue(
        ArgumentSyntax argumentSyntax,
        BelteDiagnosticQueue diagnostics,
        RefKind refKind) {
        if (argumentSyntax.expression.kind == SyntaxKind.DeclarationExpression) {
            var declarationExpression = (DeclarationExpressionSyntax)argumentSyntax.expression;
            return BindOutDeclarationArgument(declarationExpression, diagnostics);
        }

        return BindValue(
            argumentSyntax.expression,
            diagnostics,
            // We do RefConst here because its always safe to pass a Ref as RefConst
            // This is so overload resolution infers refkind from ref expressions correctly
            refKind == RefKind.None ? BindValueKind.RValue : BindValueKind.RefConst
        );
    }

    private BoundExpression BindOutDeclarationArgument(
        DeclarationExpressionSyntax declarationExpression,
        BelteDiagnosticQueue diagnostics) {
        var identifier = declarationExpression.identifier;
        var typeSyntax = declarationExpression.type;
        bool isVar;
        bool isNonNullable;
        bool isNullable;

        var localSymbol = LookupLocal(identifier);

        if (localSymbol is not null) {
            var isConst = false;
            var isConstExpr = false;
            var declType = BindVariableTypeWithAnnotations(
                declarationExpression,
                diagnostics,
                typeSyntax,
                ref isConst,
                ref isConstExpr,
                out isVar,
                out isNonNullable,
                out isNullable,
                out _
            );

            if (isNonNullable || isNullable)
                diagnostics.Push(Error.OutVarAnnotated(typeSyntax.location));

            localSymbol.scopeBinder.ValidateDeclarationNameConflictsInScope(localSymbol, diagnostics);

            if (isVar) {
                return new OutVariablePendingInference(
                    declarationExpression,
                    localSymbol,
                    null
                );
            }

            return new BoundDataContainerExpression(
                declarationExpression,
                localSymbol,
                constantValue: null,
                type: declType.type
            );
        } else {
            var expressionVariableField = LookupDeclaredField(declarationExpression)
                ?? throw ExceptionUtilities.Unreachable();

            var receiver = SynthesizeReceiver(declarationExpression, expressionVariableField, diagnostics);

            if (typeSyntax.isImplicitlyTyped) {
                // TODO BindTypeOrAliasOrImplicitType
                BindTypeOrImplicitType(
                    typeSyntax,
                    BelteDiagnosticQueue.Discarded,
                    out isVar,
                    out isNonNullable,
                    out isNullable
                );

                if (isNonNullable || isNullable)
                    diagnostics.Push(Error.OutVarAnnotated(typeSyntax.location));

                if (isVar) {
                    return new OutVariablePendingInference(
                        declarationExpression,
                        expressionVariableField,
                        receiver
                    );
                }
            }

            var fieldType = expressionVariableField.GetFieldType(fieldsBeingBound).type;

            return new BoundFieldAccessExpression(
                declarationExpression,
                receiver,
                expressionVariableField,
                null,
                type: fieldType
            );
        }
    }

    internal GlobalExpressionVariable LookupDeclaredField(DeclarationExpressionSyntax declaration) {
        return LookupDeclaredField(declaration, declaration.identifier.valueText);
    }

    internal GlobalExpressionVariable LookupDeclaredField(SyntaxNode node, string identifier) {
        foreach (var member in containingType?.GetMembers(identifier) ?? []) {
            GlobalExpressionVariable field;

            if (member.kind == SymbolKind.Field &&
                (field = member as GlobalExpressionVariable)?.syntaxTree == node.syntaxTree &&
                field.syntaxNode == node) {
                return field;
            }
        }

        return null;
    }

    // TODO
    // private NamespaceOrTypeOrAliasSymbolWithAnnotations BindTypeOrAliasOrImplicitType(
    //     TypeSyntax syntax,
    //     BelteDiagnosticQueue diagnostics,
    //     out bool isVar,
    //     out bool isNonNullable,
    //     out bool isNullable) {
    //     if (syntax.isImplicitlyTyped) {
    //         return BindTypeOrAliasOrImplicitType((IdentifierNameSyntax)syntax, diagnostics, out isVar);
    //     } else {
    //         isVar = false;
    //         isNonNullable = false;
    //         isNullable = false;
    //         return BindTypeOrAlias(syntax, diagnostics, basesBeingResolved: null);
    //     }
    // }

    private void BindArgumentAndName(
        AnalyzedArguments result,
        BelteDiagnosticQueue diagnostics,
        BelteSyntaxNode argumentSyntax,
        BoundExpression boundArgumentExpression,
        SyntaxToken identifier,
        RefKind refKind) {
        var hasRefKinds = result.refKinds.Any();

        if (refKind != RefKind.None) {
            if (!hasRefKinds) {
                hasRefKinds = true;
                var argCount = result.arguments.Count;

                for (var i = 0; i < argCount; i++)
                    result.refKinds.Add(RefKind.None);
            }
        }

        if (hasRefKinds)
            result.refKinds.Add(refKind);

        var hasNames = result.names.Any();

        if (identifier is not null) {
            if (!hasNames) {
                var argCount = result.arguments.Count;

                for (var i = 0; i < argCount; i++)
                    result.names.Add(null);
            }

            result.AddName(identifier);
        } else if (hasNames) {
            result.names.Add(null);
        }

        result.hasErrors.Add(boundArgumentExpression is BoundErrorExpression);
        result.syntaxes.Add(argumentSyntax);
        result.types.Add(boundArgumentExpression.Type());
        result.arguments.Add(new BoundExpressionOrTypeOrConstant(boundArgumentExpression));
    }

    private static string GetName(ExpressionSyntax syntax) {
        var nameSyntax = GetNameSyntax(syntax, out var nameString);

        if (nameSyntax is not null)
            return nameSyntax.GetUnqualifiedName().identifier.valueText;

        return nameString;
    }

    private BoundExpression BindParenthesisExpression(
        ParenthesisExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var value = BindExpression(node.expression, diagnostics);
        CheckNotNamespaceOrType(value, node.expression.location, diagnostics);
        return value;
    }

    private static bool CheckNotNamespaceOrType(
        BoundExpression expression,
        TextLocation location,
        BelteDiagnosticQueue diagnostics) {
        switch (expression.kind) {
            case BoundKind.NamespaceExpression:
                diagnostics.Push(Error.BadSKKnown(
                    expression.syntax.location,
                    ((BoundNamespaceExpression)expression).namespaceSymbol,
                    MessageID.IDS_SK_NAMESPACE.Localize(),
                    MessageID.IDS_SK_VARIABLE.Localize()
                ));

                return false;
            case BoundKind.TypeExpression:
                if (expression.type is TemplateParameterSymbol t && t.underlyingType.specialType != SpecialType.Type)
                    return true;

                diagnostics.Push(Error.BadSKKnown(
                    expression.syntax.location,
                    expression.type,
                    MessageID.IDS_SK_TYPE.Localize(),
                    MessageID.IDS_SK_VARIABLE.Localize()
                ));

                return false;
            default:
                return true;
        }
    }

    private static bool CheckNotNamespaceOrType(BoundExpression expression, BelteDiagnosticQueue diagnostics) {
        return CheckNotNamespaceOrType(expression, expression.syntax.location, diagnostics);
    }

    private BoundStatement BindEmptyStatement(EmptyStatementSyntax node, BelteDiagnosticQueue diagnostics) {
        return new BoundNopStatement(node);
    }

    private BoundExpression BindInterpolatedString(
        InterpolatedStringExpressionSyntax expression,
        BelteDiagnosticQueue diagnostics) {
        var builder = ArrayBuilder<BoundExpression>.GetInstance();
        var stringType = CorLibrary.GetSpecialType(SpecialType.String);
        ConstantValue resultConstant = null;
        var isResultConstant = true;
        var isCString = expression.stringStart.text.StartsWith('c');
        var isCWString = expression.stringStart.text.StartsWith('w');

        TypeSymbol type = isCString
            ? new PointerTypeSymbol(
                new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.UInt8)))
            : isCWString
                ? new PointerTypeSymbol(
                    new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Char)))
                : stringType;

        if (expression.contents.Count == 0) {
            resultConstant = new ConstantValue(string.Empty, SpecialType.String);
            return new BoundInterpolatedStringExpression(
                expression,
                builder.ToImmutableAndFree(),
                isCString,
                isCWString,
                resultConstant,
                type
            );
        }

        foreach (var content in expression.contents) {
            switch (content.kind) {
                case SyntaxKind.Interpolation: {
                        var interpolation = (InterpolationSyntax)content;

                        if (interpolation.expression is null)
                            continue;

                        var value = BindValue(interpolation.expression, diagnostics, BindValueKind.RValue);

                        builder.Add(value);

                        if (!isResultConstant ||
                            value.constantValue is null ||
                            interpolation is null ||
                            !(value.constantValue is { specialType: SpecialType.String })) {
                            isResultConstant = false;
                            continue;
                        }

                        resultConstant = (resultConstant is null)
                            ? value.constantValue
                            : FoldStringConcatenation(resultConstant, value.constantValue);

                        continue;
                    }
                case SyntaxKind.InterpolatedStringText: {
                        var text = ((InterpolatedStringTextSyntax)content).token.value;

                        var constantValue = new ConstantValue(text, SpecialType.String);
                        builder.Add(new BoundLiteralExpression(content, constantValue, stringType));

                        if (isResultConstant) {
                            resultConstant = resultConstant is null
                                ? constantValue
                                : FoldStringConcatenation(resultConstant, constantValue);
                        }

                        continue;
                    }
                default:
                    throw ExceptionUtilities.UnexpectedValue(content.kind);
            }
        }

        if (!isResultConstant)
            resultConstant = null;

        return new BoundInterpolatedStringExpression(
            expression,
            builder.ToImmutableAndFree(),
            isCString,
            isCWString,
            resultConstant,
            type
        );

        static ConstantValue FoldStringConcatenation(ConstantValue left, ConstantValue right) {
            if (left is null || right is null)
                return null;

            if (left.specialType != SpecialType.String || right.specialType != SpecialType.String)
                return null;

            return new ConstantValue(
                (string)left.value + (string)right.value,
                SpecialType.String
            );
        }
    }

    private BoundExpression BindLiteralExpression(LiteralExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        return BindLiteral(node, node.token);
    }

    private BoundExpression BindDefaultLiteralExpression(
        DefaultLiteralExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var isLowLevel = node.lowlevelKeyword is not null;

        if (isLowLevel && !flags.Includes(BinderFlags.LowLevelContext))
            diagnostics.Push(Error.LowLevelDefaultOutsideLowLevelContext(node.location));

        return new BoundDefaultLiteral(node, isLowLevel);
    }

    private BoundExpression BindDefaultExpression(DefaultExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var typeWithAnnotations = BindType(node.type, diagnostics, out var alias);
        var type = typeWithAnnotations.type;
        var typeExpression = new BoundTypeExpression(node.type, typeWithAnnotations, alias, type);
        var isLowLevel = node.lowlevelKeyword is not null;

        ReportDefaultExpressionErrors(node, type, isLowLevel, false, diagnostics);

        return new BoundDefaultExpression(
            node,
            isLowLevel,
            typeExpression,
            LiteralUtilities.TryGetDefaultValue(type),
            type
        );
    }

    private BoundExpression BindExtendedLiteralExpression(
        ExtendedLiteralExpressionSyntax node,
        BelteDiagnosticQueue diagnostics) {
        var literal = node.token is not null
            ? BindLiteral(node, node.token)
            : BindExpression(node.expression, diagnostics);

        return new BoundUnconvertedExtendedLiteralExpression(node, node.suffix.text, literal, literal.type);
    }

    private BoundExpression BindLiteral(SyntaxNode node, SyntaxToken literalToken) {
        var value = literalToken.value;
        var kind = literalToken.kind;

        if (value is null) {
            switch (kind) {
                case SyntaxKind.NullKeyword:
                    return new BoundLiteralExpression(node, new ConstantValue(null, SpecialType.None), null);
                case SyntaxKind.NullptrKeyword:
                    return new BoundUnconvertedNullptrExpression(node);
                default:
                    throw ExceptionUtilities.UnexpectedValue(kind);
            }
        }

        var specialType = SpecialTypeExtensions.SpecialTypeFromLiteralValue(value);
        var constantValue = new ConstantValue(value, specialType);

        if (kind == SyntaxKind.CStringLiteralToken) {
            var pointerType = new PointerTypeSymbol(
                new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.UInt8))
            );

            return new BoundCStringLiteral(node, isWide: false, constantValue, pointerType);
        } else if (kind == SyntaxKind.CWStringLiteralToken) {
            var pointerType = new PointerTypeSymbol(
                new TypeWithAnnotations(CorLibrary.GetSpecialType(SpecialType.Char))
            );

            return new BoundCStringLiteral(node, isWide: true, constantValue, pointerType);
        }

        var type = CorLibrary.GetSpecialType(specialType);
        return new BoundLiteralExpression(node, constantValue, type);
    }

    private BoundLiteralExpression ExpandLiteralToLargerNumeric(BoundLiteralExpression node) {
        var specialType = CodeGenerator.NormalizeNumericType(node.Type().specialType);

        switch (specialType) {
            case SpecialType.UInt8:
            case SpecialType.UInt16:
            case SpecialType.UInt32:
            case SpecialType.Int8:
            case SpecialType.Int16:
            case SpecialType.Int32:
            case SpecialType.Int64:
                return BoundFactory.Literal(
                    node.syntax,
                    Convert.ToInt64(node.constantValue.value),
                    CorLibrary.GetSpecialType(SpecialType.Int)
                );
            case SpecialType.Float32:
            case SpecialType.Float64:
                return BoundFactory.Literal(
                    node.syntax,
                    Convert.ToDouble(node.constantValue.value),
                    CorLibrary.GetSpecialType(SpecialType.Decimal)
                );
            default:
                return node;
        }
    }

    private BoundThisExpression BindThisExpression(ThisExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var hasErrors = true;

        if (!HasThis(true, out var inStaticContext)) {
            if (inStaticContext)
                diagnostics.Push(Error.CannotUseThisInStaticMethod(node.location));
            else
                diagnostics.Push(Error.CannotUseThis(node.location));
        } else {
            hasErrors = IsRefOrOutThisParameterCaptured(node.keyword, diagnostics);
        }

        return new BoundThisExpression(node, containingType, hasErrors);
    }

    private BoundBaseExpression BindBaseExpression(BaseExpressionSyntax node, BelteDiagnosticQueue diagnostics) {
        var hasErrors = false;
        TypeSymbol baseType = containingType?.baseType;

        if (!HasThis(true, out var inStaticContext)) {
            if (inStaticContext)
                diagnostics.Push(Error.CannotUseBaseInStaticMethod(node.location));
            else
                diagnostics.Push(Error.CannotUseBase(node.location));

            hasErrors = true;
        } else if (baseType is null) {
            diagnostics.Push(Error.NoBaseClass(node.location, containingType));
            hasErrors = true;
        } else if (containingType is null || node.parent is null ||
            (node.parent.kind != SyntaxKind.MemberAccessExpression && node.parent.kind != SyntaxKind.IndexExpression)) {
            diagnostics.Push(Error.CannotUseBase(node.location));
            hasErrors = true;
        } else if (IsRefOrOutThisParameterCaptured(node.keyword, diagnostics)) {
            hasErrors = true;
        }

        return new BoundBaseExpression(node, baseType, hasErrors);
    }

    internal bool HasThis(bool isExplicit, out bool inStaticContext) {
        var member = containingMember;

        if (member?.isStatic == true) {
            inStaticContext = member.kind == SymbolKind.Field || member.kind == SymbolKind.Method;
            return false;
        }

        inStaticContext = false;

        if (_inConstructorInitializer)
            return false;

        if (inFieldInitializer)
            return false;

        return true;
    }

    private bool IsRefOrOutThisParameterCaptured(SyntaxNodeOrToken thisOrBaseToken, BelteDiagnosticQueue diagnostics) {
        if (GetDiagnosticIfRefOrOutThisParameterCaptured(thisOrBaseToken.location) is { } diagnostic) {
            diagnostics.Push(diagnostic);
            return true;
        }

        return false;
    }

    private BelteDiagnostic GetDiagnosticIfRefOrOutThisParameterCaptured(TextLocation location) {
        var thisSymbol = containingMember.EnclosingThisSymbol();

        if (thisSymbol is not null &&
            thisSymbol.containingSymbol != containingMember &&
            thisSymbol.refKind != RefKind.None) {
            // TODO error, confirm this is the right one
            throw ExceptionUtilities.Unreachable();
            // return Error.CannotUseThis(location);
        }

        return null;
    }
}
