using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class RefSafetyAnalysis : BoundTreeWalkerWithStackGuardWithoutRecursionOnLeftOfBinaryOperator {
    private const uint CallingMethodScope = 0;
    private const uint ReturnOnlyScope = 1;
    private const uint CurrentMethodScope = 2;

    private readonly MethodSymbol _symbol;
    private readonly BelteDiagnosticQueue _diagnostics;
    private uint _localScopeDepth;
    private Dictionary<DataContainerSymbol, (uint refEscapeScope, uint valEscapeScope)> _localEscapeScopes;
    private Dictionary<BoundValuePlaceholder, uint>? _placeholderScopes;

    internal static void Analyze(MethodSymbol method, BoundNode node, BelteDiagnosticQueue diagnostics) {
        var visitor = new RefSafetyAnalysis(method, diagnostics);
        // TODO We should probably handle CancelledByStackGuardException here
        visitor.Visit(node);
    }

    private RefSafetyAnalysis(
        MethodSymbol symbol,
        BelteDiagnosticQueue diagnostics,
        Dictionary<DataContainerSymbol, (uint RefEscapeScope, uint ValEscapeScope)> localEscapeScopes = null) {
        _symbol = symbol;
        _diagnostics = diagnostics;
        _localScopeDepth = CurrentMethodScope - 1;
        _localEscapeScopes = localEscapeScopes;
    }

    private (uint refEscapeScope, uint valEscapeScope) GetLocalScopes(DataContainerSymbol local) {
        return _localEscapeScopes?.TryGetValue(local, out var scopes) == true
            ? scopes
            : (CallingMethodScope, CallingMethodScope);
    }

    private void SetLocalScopes(DataContainerSymbol local, uint refEscapeScope, uint valEscapeScope) {
        AddOrSetLocalScopes(local, refEscapeScope, valEscapeScope);
    }

    private void AddPlaceholderScope(BoundValuePlaceholder placeholder, uint valEscapeScope) {
        _placeholderScopes ??= [];
        _placeholderScopes[placeholder] = valEscapeScope;
    }

    private void RemovePlaceholderScope(BoundValuePlaceholder placeholder) { }

    private uint GetPlaceholderScope(BoundValuePlaceholder placeholder) {
        return _placeholderScopes?.TryGetValue(placeholder, out var scope) == true
            ? scope
            : CallingMethodScope;
    }

    private void AddLocalScopes(DataContainerSymbol local, uint refEscapeScope, uint valEscapeScope) {
        var scopedModifier = local.scope;

        if (scopedModifier != ScopedKind.None) {
            refEscapeScope = scopedModifier == ScopedKind.ScopedRef
                ? _localScopeDepth
                : CurrentMethodScope;
            // valEscapeScope = scopedModifier == ScopedKind.ScopedValue
            //     ? _localScopeDepth
            //     : CallingMethodScope;
            valEscapeScope = CallingMethodScope;
        }

        AddOrSetLocalScopes(local, refEscapeScope, valEscapeScope);
    }

    private void AddOrSetLocalScopes(DataContainerSymbol local, uint refEscapeScope, uint valEscapeScope) {
        _localEscapeScopes ??= [];
        _localEscapeScopes[local] = (refEscapeScope, valEscapeScope);
    }

    private void RemoveLocalScopes(DataContainerSymbol local) { }

    internal override BoundNode VisitBlockStatement(BoundBlockStatement node) {
        using var _ = new LocalScope(this, node.locals);
        return base.VisitBlockStatement(node);
    }

    internal override BoundNode VisitFieldEqualsValue(BoundFieldEqualsValue node) {
        throw ExceptionUtilities.Unreachable();
    }

    internal override BoundNode VisitLocalFunctionStatement(BoundLocalFunctionStatement node) {
        var localFunction = node.symbol;
        var analysis = new RefSafetyAnalysis(localFunction, _diagnostics, _localEscapeScopes);
        analysis.Visit(node.body);
        return null;
    }

    internal override BoundNode VisitConstructorMethodBody(BoundConstructorMethodBody node) {
        using var _ = new LocalScope(this, node.locals);
        return base.VisitConstructorMethodBody(node);
    }

    internal override BoundNode VisitForStatement(BoundForStatement node) {
        using var outerLocals = new LocalScope(this, node.locals);
        using var innerLocals = new LocalScope(this, node.innerLocals);
        return base.VisitForStatement(node);
    }

    internal override BoundNode VisitDoWhileStatement(BoundDoWhileStatement node) {
        using var _ = new LocalScope(this, node.locals);
        return base.VisitDoWhileStatement(node);
    }

    internal override BoundNode VisitWhileStatement(BoundWhileStatement node) {
        using var _ = new LocalScope(this, node.locals);
        return base.VisitWhileStatement(node);
    }

    internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
        base.VisitLocalDeclarationStatement(node);

        if (node.declaration.initializer is { } initializer) {
            var localSymbol = (SourceDataContainerSymbol)node.declaration.dataContainer;
            var (refEscapeScope, valEscapeScope) = GetLocalScopes(localSymbol);

            if (localSymbol.scope != ScopedKind.None) {
                // if (node.DeclaredTypeOpt?.Type.IsRefLikeOrAllowsRefLikeType() == true) {
                //     ValidateEscape(initializer, valEscapeScope, isByRef: false, _diagnostics);
                // }
            } else {
                SetLocalScopes(localSymbol, _localScopeDepth, _localScopeDepth);

                valEscapeScope = GetValEscape(initializer, _localScopeDepth);

                if (localSymbol.refKind != RefKind.None)
                    refEscapeScope = GetRefEscape(initializer, _localScopeDepth);

                SetLocalScopes(localSymbol, refEscapeScope, valEscapeScope);
            }
        }

        return null;
    }

    internal override BoundNode VisitReturnStatement(BoundReturnStatement node) {
        base.VisitReturnStatement(node);

        if (node.expression is { type: { } } expr)
            ValidateEscape(expr, ReturnOnlyScope, node.refKind != RefKind.None, _diagnostics);

        return null;
    }

    internal override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node) {
        base.VisitAssignmentOperator(node);
        ValidateAssignment(node.syntax, node.left, node.right, node.isRef, _diagnostics);
        return null;
    }

    internal override BoundNode VisitCompoundAssignmentOperator(BoundCompoundAssignmentOperator node) {
        base.VisitCompoundAssignmentOperator(node);
        ValidateAssignment(node.syntax, node.left, node, isRef: false, _diagnostics);
        return null;
    }

    internal override BoundNode VisitConditionalOperator(BoundConditionalOperator node) {
        base.VisitConditionalOperator(node);

        if (node.isRef)
            ValidateRefConditionalOperator(node.syntax, node.trueExpression, node.falseExpression, _diagnostics);

        return null;
    }

    private void VisitArgumentsAndGetArgumentPlaceholders(
        BoundExpression receiver,
        ImmutableArray<BoundExpression> arguments) {
        for (var i = 0; i < arguments.Length; i++) {
            var arg = arguments[i];
            // Some interpolation handling would go here
            Visit(arg);
        }
    }

    private protected override void VisitArguments(BoundCallExpression node) {
        VisitArgumentsAndGetArgumentPlaceholders(node.receiver, node.arguments);

        if (!node.hasErrors) {
            var method = node.method;

            CheckInvocationArgMixing(
                node.syntax,
                MethodInfo.Create(method),
                node.receiver,
                // node.InitialBindingReceiverIsSubjectToCloning,
                ThreeState.Unknown,
                method.parameters,
                node.arguments,
                node.argumentRefKinds,
                // node.argsToParamsOpt,
                default,
                _localScopeDepth,
                _diagnostics
            );
        }
    }

    internal override BoundNode VisitObjectCreationExpression(BoundObjectCreationExpression node) {
        VisitObjectCreationExpressionBase(node);
        return null;
    }

    internal override BoundNode VisitNewT(BoundNewT node) {
        VisitObjectCreationExpressionBase(node);
        return null;
    }

    private void VisitObjectCreationExpressionBase(BoundObjectCreationExpressionBase node) {
        VisitArgumentsAndGetArgumentPlaceholders(receiver: null, node.arguments);

        if (!node.hasErrors) {
            var constructor = node.constructor;

            if (constructor is { }) {
                var methodInfo = MethodInfo.Create(constructor);

                CheckInvocationArgMixing(
                    node.syntax,
                    in methodInfo,
                    receiverOpt: null,
                    receiverIsSubjectToCloning: ThreeState.Unknown,
                    constructor.parameters,
                    node.arguments,
                    node.argumentRefKinds,
                    node.argsToParams,
                    _localScopeDepth,
                    _diagnostics
                );
            }
        }
    }

    internal void ValidateEscape(
        BoundExpression expression,
        uint escapeTo,
        bool isByRef,
        BelteDiagnosticQueue diagnostics) {
        if (isByRef)
            CheckRefEscape(expression.syntax, expression, _localScopeDepth, escapeTo, false, diagnostics);
        else
            CheckValEscape(expression.syntax, expression, _localScopeDepth, escapeTo, false, diagnostics);
    }

    internal uint GetRefEscape(BoundExpression expression, uint scopeOfTheContainingExpression) {
        if (expression.hasErrors)
            return CallingMethodScope;

        if (expression.type?.GetSpecialTypeSafe() == SpecialType.Void)
            return CallingMethodScope;

        if (expression.constantValue is not null)
            return scopeOfTheContainingExpression;

        switch (expression.kind) {
            case BoundKind.ArrayAccessExpression:
            case BoundKind.PointerIndirectionOperator:
            case BoundKind.PointerIndexAccessExpression:
                return CallingMethodScope;
            case BoundKind.ParameterExpression:
                return GetParameterRefEscape(((BoundParameterExpression)expression).parameter);
            case BoundKind.DataContainerExpression:
                return GetLocalScopes(((BoundDataContainerExpression)expression).dataContainer).refEscapeScope;
            case BoundKind.ThisExpression:
                var thisParam = ((MethodSymbol)_symbol).thisParameter;
                return GetParameterRefEscape(thisParam);
            case BoundKind.ConditionalOperator:
                var conditional = (BoundConditionalOperator)expression;

                if (conditional.isRef) {
                    return Math.Max(GetRefEscape(conditional.trueExpression, scopeOfTheContainingExpression),
                                    GetRefEscape(conditional.falseExpression, scopeOfTheContainingExpression));
                }

                break;
            case BoundKind.FieldAccessExpression:
                return GetFieldRefEscape((BoundFieldAccessExpression)expression, scopeOfTheContainingExpression);
            case BoundKind.CallExpression: {
                    var call = (BoundCallExpression)expression;

                    var methodSymbol = call.method;

                    if (methodSymbol.refKind == RefKind.None)
                        break;

                    return GetInvocationEscape(
                        MethodInfo.Create(call.method),
                        call.receiver,
                        // call.InitialBindingReceiverIsSubjectToCloning,
                        ThreeState.Unknown,
                        methodSymbol.parameters,
                        call.arguments,
                        call.argumentRefKinds,
                        // call.argsToParams,
                        default,
                        scopeOfTheContainingExpression,
                        isRefEscape: true
                    );
                }
            case BoundKind.FunctionPointerCallExpression: {
                    var ptrInvocation = (BoundFunctionPointerCallExpression)expression;

                    var methodSymbol = ptrInvocation.functionPointer.signature;

                    if (methodSymbol.refKind == RefKind.None)
                        break;

                    return GetInvocationEscape(
                        MethodInfo.Create(methodSymbol),
                        receiver: null,
                        receiverIsSubjectToCloning: ThreeState.Unknown,
                        methodSymbol.parameters,
                        ptrInvocation.arguments,
                        ptrInvocation.argumentRefKindsOpt,
                        argsToParamsOpt: default,
                        scopeOfTheContainingExpression,
                        isRefEscape: true
                    );
                }

            case BoundKind.AssignmentOperator:
                var assignment = (BoundAssignmentOperator)expression;

                if (!assignment.isRef)
                    break;

                return GetRefEscape(assignment.left, scopeOfTheContainingExpression);
        }

        return scopeOfTheContainingExpression;
    }

    internal bool CheckRefEscape(
        SyntaxNode node,
        BoundExpression expression,
        uint escapeFrom,
        uint escapeTo,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        if (escapeTo >= escapeFrom)
            return true;

        if (expression.hasErrors)
            return true;

        if (expression.type?.GetSpecialTypeSafe() == SpecialType.Void)
            return true;

        if (expression.constantValue is not null) {
            diagnostics.Push(GetStandardRValueRefEscapeError(node.location, escapeTo));
            return false;
        }

        switch (expression.kind) {
            case BoundKind.ArrayAccessExpression:
            case BoundKind.PointerIndirectionOperator:
            case BoundKind.PointerIndexAccessExpression:
                return true;
            case BoundKind.ParameterExpression:
                var parameter = (BoundParameterExpression)expression;

                return CheckParameterRefEscape(
                    node,
                    parameter,
                    parameter.parameter,
                    escapeTo,
                    checkingReceiver,
                    diagnostics
                );
            case BoundKind.DataContainerExpression:
                var local = (BoundDataContainerExpression)expression;
                return CheckLocalRefEscape(node, local, escapeTo, checkingReceiver, diagnostics);
            case BoundKind.ThisExpression:
                var thisParam = ((MethodSymbol)_symbol).thisParameter;
                return CheckParameterRefEscape(node, expression, thisParam, escapeTo, checkingReceiver, diagnostics);
            case BoundKind.ConditionalOperator:
                var conditional = (BoundConditionalOperator)expression;

                if (conditional.isRef) {
                    return CheckRefEscape(
                            conditional.trueExpression.syntax,
                            conditional.trueExpression,
                            escapeFrom,
                            escapeTo,
                            checkingReceiver: false,
                            diagnostics: diagnostics
                        ) &&
                        CheckRefEscape(
                            conditional.falseExpression.syntax,
                            conditional.falseExpression,
                            escapeFrom,
                            escapeTo,
                            checkingReceiver: false,
                            diagnostics: diagnostics
                        );
                }

                break;
            case BoundKind.FunctionPointerCallExpression:
                var functionPointerInvocation = (BoundFunctionPointerCallExpression)expression;
                var signature = functionPointerInvocation.functionPointer.signature;

                if (signature.refKind == RefKind.None)
                    break;

                return CheckInvocationEscape(
                    functionPointerInvocation.syntax,
                    MethodInfo.Create(signature),
                    functionPointerInvocation.invokedExpression,
                    receiverIsSubjectToCloning: ThreeState.False,
                    signature.parameters,
                    functionPointerInvocation.arguments,
                    functionPointerInvocation.argumentRefKindsOpt,
                    argsToParamsOpt: default,
                    checkingReceiver,
                    escapeFrom,
                    escapeTo,
                    diagnostics,
                    isRefEscape: true
                );
            case BoundKind.FieldAccessExpression:
                var fieldAccess = (BoundFieldAccessExpression)expression;
                return CheckFieldRefEscape(node, fieldAccess, escapeFrom, escapeTo, diagnostics);
            case BoundKind.CallExpression: {
                    var call = (BoundCallExpression)expression;

                    var methodSymbol = call.method;

                    if (methodSymbol.refKind == RefKind.None)
                        break;

                    return CheckInvocationEscape(
                        call.syntax,
                        MethodInfo.Create(methodSymbol),
                        call.receiver,
                        // call.InitialBindingReceiverIsSubjectToCloning,
                        ThreeState.Unknown,
                        methodSymbol.parameters,
                        call.arguments,
                        call.argumentRefKinds,
                        // call.argsToParams,
                        default,
                        checkingReceiver,
                        escapeFrom,
                        escapeTo,
                        diagnostics,
                        isRefEscape: true
                    );
                }
            case BoundKind.AssignmentOperator:
                var assignment = (BoundAssignmentOperator)expression;

                if (!assignment.isRef)
                    break;

                return CheckRefEscape(
                    node,
                    assignment.left,
                    escapeFrom,
                    escapeTo,
                    checkingReceiver: false,
                    diagnostics
                );
            case BoundKind.CastExpression:
                var conversion = (BoundCastExpression)expression;
                // if (conversion.conversion == Conversion.ImplicitThrow) {
                //     return CheckRefEscape(node, conversion.Operand, escapeFrom, escapeTo, checkingReceiver, diagnostics);
                // }
                break;
            case BoundKind.ThrowExpression:
                return true;
        }

        diagnostics.Push(GetStandardRValueRefEscapeError(node.location, escapeTo));
        return false;
    }

    internal uint GetValEscape(BoundExpression expression, uint scopeOfTheContainingExpression) {
        if (expression.hasErrors)
            return CallingMethodScope;

        if (expression.constantValue is not null)
            return CallingMethodScope;

        if (expression.Type()?.IsRefLikeOrAllowsRefLikeType() != true)
            return CallingMethodScope;

        switch (expression.kind) {
            case BoundKind.PointerIndexAccessExpression:
            case BoundKind.PointerIndirectionOperator:
                return CallingMethodScope;
            case BoundKind.ThisExpression:
                var thisParam = ((MethodSymbol)_symbol).thisParameter;
                return GetParameterValEscape(thisParam);
            case BoundKind.ParameterExpression:
                return GetParameterValEscape(((BoundParameterExpression)expression).parameter);
            case BoundKind.DataContainerExpression:
                return GetLocalScopes(((BoundDataContainerExpression)expression).dataContainer).valEscapeScope;
            case BoundKind.ConditionalOperator:
                var conditional = (BoundConditionalOperator)expression;
                var consEscape = GetValEscape(conditional.trueExpression, scopeOfTheContainingExpression);

                if (conditional.isRef)
                    return consEscape;

                return Math.Max(
                    consEscape,
                    GetValEscape(conditional.falseExpression, scopeOfTheContainingExpression)
                );
            case BoundKind.NullCoalescingOperator:
                var coalescingOp = (BoundNullCoalescingOperator)expression;

                return Math.Max(GetValEscape(coalescingOp.left, scopeOfTheContainingExpression),
                                GetValEscape(coalescingOp.right, scopeOfTheContainingExpression));
            case BoundKind.FieldAccessExpression:
                var fieldAccess = (BoundFieldAccessExpression)expression;
                var fieldSymbol = fieldAccess.field;

                if (fieldSymbol.isStatic || !fieldSymbol.containingType.isRefLikeType)
                    return CallingMethodScope;

                return GetValEscape(fieldAccess.receiver, scopeOfTheContainingExpression);

            case BoundKind.FunctionPointerCallExpression:
                var ptrInvocation = (BoundFunctionPointerCallExpression)expression;
                var ptrSymbol = ptrInvocation.functionPointer.signature;

                return GetInvocationEscape(
                    MethodInfo.Create(ptrSymbol),
                    receiver: null,
                    receiverIsSubjectToCloning: ThreeState.Unknown,
                    ptrSymbol.parameters,
                    ptrInvocation.arguments,
                    ptrInvocation.argumentRefKindsOpt,
                    argsToParamsOpt: default,
                    scopeOfTheContainingExpression,
                    isRefEscape: false
                );
            case BoundKind.CallExpression: {
                    var call = (BoundCallExpression)expression;

                    return GetInvocationEscape(
                        MethodInfo.Create(call.method),
                        call.receiver,
                        // call.InitialBindingReceiverIsSubjectToCloning,
                        ThreeState.Unknown,
                        call.method.parameters,
                        call.arguments,
                        call.argumentRefKinds,
                        // call.argsToParams,
                        default,
                        scopeOfTheContainingExpression,
                        isRefEscape: false
                    );
                }
            case BoundKind.ObjectCreationExpression: {
                    var objectCreation = (BoundObjectCreationExpression)expression;
                    var constructorSymbol = objectCreation.constructor;

                    var escape = GetInvocationEscape(
                        MethodInfo.Create(constructorSymbol),
                        receiver: null,
                        receiverIsSubjectToCloning: ThreeState.Unknown,
                        constructorSymbol.parameters,
                        objectCreation.arguments,
                        objectCreation.argumentRefKinds,
                        objectCreation.argsToParams,
                        scopeOfTheContainingExpression,
                        isRefEscape: false
                    );

                    return escape;
                }
            case BoundKind.NewT: {
                    var newT = (BoundNewT)expression;
                    var escape = CallingMethodScope;
                    return escape;
                }
            case BoundKind.UnaryOperator:
                var unaryOperator = (BoundUnaryOperator)expression;
                if (unaryOperator.method is { } unaryMethod) {
                    return GetInvocationEscape(
                        MethodInfo.Create(unaryMethod),
                        receiver: null,
                        receiverIsSubjectToCloning: ThreeState.Unknown,
                        unaryMethod.parameters,
                        argsOpt: [unaryOperator.operand],
                        argRefKindsOpt: default,
                        argsToParamsOpt: default,
                        scopeOfTheContainingExpression: scopeOfTheContainingExpression,
                        isRefEscape: false
                    );
                }

                return GetValEscape(unaryOperator.operand, scopeOfTheContainingExpression);
            case BoundKind.CastExpression:
                var conversion = (BoundCastExpression)expression;
                return GetValEscape(conversion.operand, scopeOfTheContainingExpression);
            case BoundKind.AssignmentOperator:
                return GetValEscape(((BoundAssignmentOperator)expression).right, scopeOfTheContainingExpression);
            case BoundKind.NullCoalescingAssignmentOperator:
                return GetValEscape(
                    ((BoundNullCoalescingAssignmentOperator)expression).right,
                    scopeOfTheContainingExpression
                );
            case BoundKind.IncrementOperator:
                return GetValEscape(((BoundIncrementOperator)expression).operand, scopeOfTheContainingExpression);
            case BoundKind.CompoundAssignmentOperator:
                var compound = (BoundCompoundAssignmentOperator)expression;

                if (compound.op.method is { } compoundMethod) {
                    return GetInvocationEscape(
                        MethodInfo.Create(compoundMethod),
                        receiver: null,
                        receiverIsSubjectToCloning: ThreeState.Unknown,
                        compoundMethod.parameters,
                        argsOpt: [compound.left, compound.right],
                        argRefKindsOpt: default,
                        argsToParamsOpt: default,
                        scopeOfTheContainingExpression: scopeOfTheContainingExpression,
                        isRefEscape: false
                    );
                }

                return Math.Max(GetValEscape(compound.left, scopeOfTheContainingExpression),
                                GetValEscape(compound.right, scopeOfTheContainingExpression));
            case BoundKind.BinaryOperator:
                var binary = (BoundBinaryOperator)expression;

                if (binary.method is { } binaryMethod) {
                    return GetInvocationEscape(
                        MethodInfo.Create(binaryMethod),
                        receiver: null,
                        receiverIsSubjectToCloning: ThreeState.Unknown,
                        binaryMethod.parameters,
                        argsOpt: [binary.left, binary.right],
                        argRefKindsOpt: default,
                        argsToParamsOpt: default,
                        scopeOfTheContainingExpression: scopeOfTheContainingExpression,
                        isRefEscape: false
                    );
                }

                return Math.Max(GetValEscape(binary.left, scopeOfTheContainingExpression),
                                GetValEscape(binary.right, scopeOfTheContainingExpression));
            case BoundKind.InitializerList:
                var colExpr = (BoundInitializerList)expression;
                return GetValEscape(colExpr.items, scopeOfTheContainingExpression);
            case BoundKind.AsOperator:
            case BoundKind.ConditionalAccessExpression:
            case BoundKind.ArrayAccessExpression:
                return scopeOfTheContainingExpression;
            default:
                return scopeOfTheContainingExpression;
        }
    }

    private uint GetValEscape(ImmutableArray<BoundExpression> expressions, uint scopeOfTheContainingExpression) {
        var result = CallingMethodScope;

        foreach (var expression in expressions)
            result = Math.Max(result, GetValEscape(expression, scopeOfTheContainingExpression));

        return result;
    }

    private bool CheckValEscape(
        ImmutableArray<BoundExpression> expressions,
        uint escapeFrom,
        uint escapeTo,
        BelteDiagnosticQueue diagnostics) {
        foreach (var expression in expressions) {
            if (!CheckValEscape(expression.syntax, expression, escapeFrom, escapeTo, false, diagnostics))
                return false;
        }

        return true;
    }

    internal bool CheckValEscape(
        SyntaxNode node,
        BoundExpression expression,
        uint escapeFrom,
        uint escapeTo,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        if (escapeTo >= escapeFrom)
            return true;

        if (expression.hasErrors)
            return true;

        if (expression.constantValue is not null)
            return true;

        if (expression.Type()?.IsRefLikeOrAllowsRefLikeType() != true)
            return true;

        switch (expression.kind) {
            case BoundKind.PointerIndexAccessExpression:
                var accessedExpression = ((BoundPointerIndexAccessExpression)expression).receiver;
                return CheckValEscape(
                    accessedExpression.syntax,
                    accessedExpression,
                    escapeFrom,
                    escapeTo,
                    checkingReceiver,
                    diagnostics
                );
            case BoundKind.PointerIndirectionOperator:
                var operandExpression = ((BoundPointerIndirectionOperator)expression).operand;
                return CheckValEscape(
                    operandExpression.syntax,
                    operandExpression,
                    escapeFrom,
                    escapeTo,
                    checkingReceiver,
                    diagnostics
                );
            case BoundKind.ThisExpression:
                var thisParam = ((MethodSymbol)_symbol).thisParameter;
                return CheckParameterValEscape(node, thisParam, escapeTo, diagnostics);
            case BoundKind.ParameterExpression:
                return CheckParameterValEscape(
                    node,
                    ((BoundParameterExpression)expression).parameter,
                    escapeTo,
                    diagnostics
                );
            case BoundKind.DataContainerExpression:
                var localSymbol = ((BoundDataContainerExpression)expression).dataContainer;

                if (GetLocalScopes(localSymbol).valEscapeScope > escapeTo) {
                    diagnostics.Push(Error.EscapeLocal(node.location, localSymbol));
                    return false;
                }

                return true;
            case BoundKind.InitializerList:
                var colExpr = (BoundInitializerList)expression;
                return CheckValEscape(colExpr.items, escapeFrom, escapeTo, diagnostics);
            case BoundKind.ConditionalOperator: {
                    var conditional = (BoundConditionalOperator)expression;
                    var consValid = CheckValEscape(
                        conditional.trueExpression.syntax,
                        conditional.trueExpression,
                        escapeFrom,
                        escapeTo,
                        checkingReceiver: false,
                        diagnostics: diagnostics
                    );

                    if (!consValid || conditional.isRef)
                        return consValid;

                    return CheckValEscape(
                        conditional.falseExpression.syntax,
                        conditional.falseExpression,
                        escapeFrom,
                        escapeTo,
                        checkingReceiver: false,
                        diagnostics: diagnostics
                    );
                }
            case BoundKind.FunctionPointerCallExpression:
                var ptrInvocation = (BoundFunctionPointerCallExpression)expression;
                var ptrSymbol = ptrInvocation.functionPointer.signature;

                return CheckInvocationEscape(
                    ptrInvocation.syntax,
                    MethodInfo.Create(ptrSymbol),
                    receiver: null,
                    receiverIsSubjectToCloning: ThreeState.Unknown,
                    ptrSymbol.parameters,
                    ptrInvocation.arguments,
                    ptrInvocation.argumentRefKindsOpt,
                    argsToParamsOpt: default,
                    checkingReceiver,
                    escapeFrom,
                    escapeTo,
                    diagnostics,
                    isRefEscape: false
                );
            case BoundKind.NullCoalescingOperator:
                var coalescingOp = (BoundNullCoalescingOperator)expression;
                return CheckValEscape(
                        coalescingOp.left.syntax,
                        coalescingOp.left,
                        escapeFrom,
                        escapeTo,
                        checkingReceiver,
                        diagnostics
                    ) &&
                    CheckValEscape(
                        coalescingOp.right.syntax,
                        coalescingOp.right,
                        escapeFrom,
                        escapeTo,
                        checkingReceiver,
                        diagnostics
                    );

            case BoundKind.FieldAccessExpression:
                var fieldAccess = (BoundFieldAccessExpression)expression;
                var fieldSymbol = fieldAccess.field;

                if (fieldSymbol.isStatic || !fieldSymbol.containingType.isRefLikeType)
                    return true;

                return CheckValEscape(node, fieldAccess.receiver, escapeFrom, escapeTo, true, diagnostics);
            case BoundKind.CallExpression: {
                    var call = (BoundCallExpression)expression;
                    var methodSymbol = call.method;

                    return CheckInvocationEscape(
                        call.syntax,
                        MethodInfo.Create(methodSymbol),
                        call.receiver,
                        // TODO
                        ThreeState.Unknown,
                        // call.initialBindingReceiverIsSubjectToCloning,
                        methodSymbol.parameters,
                        call.arguments,
                        call.argumentRefKinds,
                        // ? Binder covers this, correct?
                        // call.argsToParams,
                        default,
                        checkingReceiver,
                        escapeFrom,
                        escapeTo,
                        diagnostics,
                        isRefEscape: false
                    );
                }
            case BoundKind.ObjectCreationExpression: {
                    var objectCreation = (BoundObjectCreationExpression)expression;
                    var constructorSymbol = objectCreation.constructor;

                    var escape = CheckInvocationEscape(
                        objectCreation.syntax,
                        MethodInfo.Create(constructorSymbol),
                        receiver: null,
                        receiverIsSubjectToCloning: ThreeState.Unknown,
                        constructorSymbol.parameters,
                        objectCreation.arguments,
                        objectCreation.argumentRefKinds,
                        objectCreation.argsToParams,
                        checkingReceiver,
                        escapeFrom,
                        escapeTo,
                        diagnostics,
                        isRefEscape: false
                    );

                    return escape;
                }

            case BoundKind.NewT: {
                    var newT = (BoundNewT)expression;
                    var escape = true;
                    return escape;
                }
            case BoundKind.UnaryOperator:
                var unary = (BoundUnaryOperator)expression;
                if (unary.method is { } unaryMethod) {
                    return CheckInvocationEscape(
                        unary.syntax,
                        MethodInfo.Create(unaryMethod),
                        receiver: null,
                        receiverIsSubjectToCloning: ThreeState.Unknown,
                        unaryMethod.parameters,
                        argsOpt: [unary.operand],
                        argRefKindsOpt: default,
                        argsToParamsOpt: default,
                        checkingReceiver: checkingReceiver,
                        escapeFrom: escapeFrom,
                        escapeTo: escapeTo,
                        diagnostics,
                        isRefEscape: false
                    );
                }

                return CheckValEscape(node, unary.operand, escapeFrom, escapeTo, false, diagnostics);
            case BoundKind.CastExpression:
                var conversion = (BoundCastExpression)expression;
                return CheckValEscape(node, conversion.operand, escapeFrom, escapeTo, false, diagnostics);
            case BoundKind.AssignmentOperator:
                var assignment = (BoundAssignmentOperator)expression;
                return CheckValEscape(node, assignment.left, escapeFrom, escapeTo, false, diagnostics);
            case BoundKind.NullCoalescingAssignmentOperator:
                var nullCoalescingAssignment = (BoundNullCoalescingAssignmentOperator)expression;

                return CheckValEscape(
                    node,
                    nullCoalescingAssignment.left,
                    escapeFrom,
                    escapeTo,
                    checkingReceiver: false,
                    diagnostics: diagnostics
                );
            case BoundKind.IncrementOperator:
                var increment = (BoundIncrementOperator)expression;
                return CheckValEscape(node, increment.operand, escapeFrom, escapeTo, false, diagnostics);
            case BoundKind.CompoundAssignmentOperator:
                var compound = (BoundCompoundAssignmentOperator)expression;

                if (compound.op.method is { } compoundMethod) {
                    return CheckInvocationEscape(
                        compound.syntax,
                        MethodInfo.Create(compoundMethod),
                        receiver: null,
                        receiverIsSubjectToCloning: ThreeState.Unknown,
                        compoundMethod.parameters,
                        argsOpt: [compound.left, compound.right],
                        argRefKindsOpt: default,
                        argsToParamsOpt: default,
                        checkingReceiver: checkingReceiver,
                        escapeFrom: escapeFrom,
                        escapeTo: escapeTo,
                        diagnostics,
                        isRefEscape: false
                    );
                }

                return CheckValEscape(compound.left.syntax, compound.left, escapeFrom, escapeTo, false, diagnostics) &&
                       CheckValEscape(compound.right.syntax, compound.right, escapeFrom, escapeTo, false, diagnostics);
            case BoundKind.BinaryOperator:
                var binary = (BoundBinaryOperator)expression;

                if (binary.method is { } binaryMethod) {
                    return CheckInvocationEscape(
                        binary.syntax,
                        MethodInfo.Create(binaryMethod),
                        receiver: null,
                        receiverIsSubjectToCloning: ThreeState.Unknown,
                        binaryMethod.parameters,
                        argsOpt: [binary.left, binary.right],
                        argRefKindsOpt: default,
                        argsToParamsOpt: default,
                        checkingReceiver: checkingReceiver,
                        escapeFrom: escapeFrom,
                        escapeTo: escapeTo,
                        diagnostics,
                        isRefEscape: false
                    );
                }

                return CheckValEscape(binary.left.syntax, binary.left, escapeFrom, escapeTo, false, diagnostics) &&
                       CheckValEscape(binary.right.syntax, binary.right, escapeFrom, escapeTo, false, diagnostics);
            case BoundKind.AsOperator:
            case BoundKind.ConditionalAccessExpression:
            case BoundKind.ArrayAccessExpression:
                return false;
            default:
                diagnostics.Push(Error.InternalError(node.location));
                return false;
        }
    }

    private bool CheckInvocationEscape(
        SyntaxNode syntax,
        in MethodInfo methodInfo,
        BoundExpression receiver,
        ThreeState receiverIsSubjectToCloning,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<BoundExpression> argsOpt,
        ImmutableArray<RefKind> argRefKindsOpt,
        ImmutableArray<int> argsToParamsOpt,
        bool checkingReceiver,
        uint escapeFrom,
        uint escapeTo,
        BelteDiagnosticQueue diagnostics,
        bool isRefEscape) {
        var result = true;

        var argsAndParamsAll = ArrayBuilder<EscapeValue>.GetInstance();

        GetFilteredInvocationArgumentsForEscape(
            methodInfo,
            receiver,
            receiverIsSubjectToCloning,
            parameters,
            argsOpt,
            argRefKindsOpt,
            argsToParamsOpt,
            isRefEscape,
            argsAndParamsAll
        );

        var symbol = methodInfo.symbol;
        var returnsRefToRefStruct = methodInfo.returnsRefToRefStruct;

        foreach (var (param, argument, _, isArgumentRefEscape) in argsAndParamsAll) {
            if (!returnsRefToRefStruct
                || ((param is null ||
                     (param is { refKind: not RefKind.None, type: { } type } && type.IsRefLikeOrAllowsRefLikeType())) &&
                    isArgumentRefEscape == isRefEscape)) {
                var valid = isArgumentRefEscape
                    ? CheckRefEscape(argument.syntax, argument, escapeFrom, escapeTo, false, diagnostics)
                    : CheckValEscape(argument.syntax, argument, escapeFrom, escapeTo, false, diagnostics);

                if (!valid) {
                    if ((object)argument != receiver)
                        ReportInvocationEscapeError(syntax, symbol, param, checkingReceiver, diagnostics);

                    result = false;
                    break;
                }
            }
        }

        argsAndParamsAll.Free();
        return result;
    }

    private static BelteDiagnostic GetStandardRValueRefEscapeError(TextLocation location, uint escapeTo) {
        if (escapeTo is CallingMethodScope or ReturnOnlyScope) {
            return Error.RefReturnLValueExpected(location);
        }

        return Error.EscapeOther(location);
    }

    private static string GetInvocationParameterName(ParameterSymbol parameter) {
        var parameterName = parameter.name;

        if (string.IsNullOrEmpty(parameterName))
            parameterName = parameter.ordinal.ToString();

        return parameterName;
    }

    private static void ReportInvocationEscapeError(
        SyntaxNode syntax,
        Symbol symbol,
        ParameterSymbol parameter,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        diagnostics.Push(GetCallEscapeError(syntax.location, symbol, checkingReceiver, parameter));
    }

    private static BelteDiagnostic GetCallEscapeError(
        TextLocation location,
        Symbol symbol,
        bool checkingReceiver,
        ParameterSymbol parameter) {
        var parameterName = GetInvocationParameterName(parameter);

        return checkingReceiver
            ? Error.EscapeCall2(location, symbol, parameterName)
            : Error.EscapeCall(location, symbol, parameterName);
    }

    private void GetFilteredInvocationArgumentsForEscape(
        in MethodInfo methodInfo,
        BoundExpression? receiver,
        ThreeState receiverIsSubjectToCloning,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<BoundExpression> argsOpt,
        ImmutableArray<RefKind> argRefKindsOpt,
        ImmutableArray<int> argsToParamsOpt,
        bool isInvokedWithRef,
        ArrayBuilder<EscapeValue> escapeValues) {
        if (!isInvokedWithRef && !HasRefLikeReturn(methodInfo.symbol))
            return;

        GetEscapeValues(
            methodInfo,
            receiver,
            receiverIsSubjectToCloning,
            parameters,
            argsOpt,
            argRefKindsOpt,
            argsToParamsOpt,
            mixableArguments: null,
            escapeValues
        );

        static bool HasRefLikeReturn(Symbol symbol) {
            switch (symbol) {
                case MethodSymbol method:
                    if (method.methodKind == MethodKind.Constructor)
                        return method.containingType.isRefLikeType;

                    return method.returnType.IsRefLikeOrAllowsRefLikeType();
                default:
                    return false;
            }
        }
    }

    private void GetEscapeValues(
        in MethodInfo methodInfo,
        BoundExpression? receiver,
        ThreeState receiverIsSubjectToCloning,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<BoundExpression> argsOpt,
        ImmutableArray<RefKind> argRefKindsOpt,
        ImmutableArray<int> argsToParamsOpt,
        ArrayBuilder<MixableDestination>? mixableArguments,
        ArrayBuilder<EscapeValue> escapeValues) {
        if (!methodInfo.symbol.RequiresInstanceReceiver())
            receiver = null;

        var escapeArguments = ArrayBuilder<EscapeArgument>.GetInstance();

        GetInvocationArgumentsForEscape(
            methodInfo,
            receiver,
            receiverIsSubjectToCloning,
            parameters,
            argsOpt,
            argRefKindsOpt,
            argsToParamsOpt,
            mixableArguments,
            escapeArguments
        );

        foreach (var (parameter, argument, refKind) in escapeArguments) {
            if (parameter is null) {
                if (refKind != RefKind.None)
                    escapeValues.Add(new EscapeValue(null, argument, EscapeLevel.ReturnOnly, true));

                if (argument.Type()?.IsRefLikeOrAllowsRefLikeType() == true)
                    escapeValues.Add(new EscapeValue(null, argument, EscapeLevel.CallingMethod, false));

                continue;
            }

            if (parameter.type.IsRefLikeOrAllowsRefLikeType() &&
                GetParameterValEscapeLevel(parameter) is { } valEscapeLevel) {
                escapeValues.Add(new EscapeValue(parameter, argument, valEscapeLevel, isRefEscape: false));
            }

            if (parameter.refKind != RefKind.None && GetParameterRefEscapeLevel(parameter) is { } refEscapeLevel)
                escapeValues.Add(new EscapeValue(parameter, argument, refEscapeLevel, isRefEscape: true));
        }

        escapeArguments.Free();
    }

    private void GetInvocationArgumentsForEscape(
        in MethodInfo methodInfo,
        BoundExpression receiver,
        ThreeState receiverIsSubjectToCloning,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<BoundExpression> argsOpt,
        ImmutableArray<RefKind> argRefKindsOpt,
        ImmutableArray<int> argsToParamsOpt,
        ArrayBuilder<MixableDestination>? mixableArguments,
        ArrayBuilder<EscapeArgument> escapeArguments) {
        if (receiver is { }) {
            var method = methodInfo.method;

            // if (receiverIsSubjectToCloning == ThreeState.True) {
            //     receiver = new BoundCapturedReceiverPlaceholder(receiver.Syntax, receiver, _localScopeDepth, receiver.Type).MakeCompilerGenerated();
            // }

            var tuple = GetReceiver(methodInfo, receiver);
            escapeArguments.Add(tuple);

            if (mixableArguments is not null && IsMixableParameter(tuple.parameter))
                mixableArguments.Add(new MixableDestination(tuple.parameter, receiver));
        }

        if (!argsOpt.IsDefault) {
            for (var argIndex = 0; argIndex < argsOpt.Length; argIndex++) {
                var argument = argsOpt[argIndex];

                var parameter = argIndex < parameters.Length
                    ? parameters[argsToParamsOpt.IsDefault ? argIndex : argsToParamsOpt[argIndex]]
                    : null;

                if (mixableArguments is not null &&
                    IsMixableParameter(parameter) &&
                    IsMixableArgument(argument)) {
                    mixableArguments.Add(new MixableDestination(parameter, argument));
                }

                var refKind = parameter?.refKind ?? RefKind.None;

                if (!argRefKindsOpt.IsDefault)
                    refKind = argRefKindsOpt[argIndex];

                if (refKind == RefKind.None &&
                    parameter?.refKind is RefKind.RefConstParameter) {
                    refKind = parameter.refKind;
                }

                escapeArguments.Add(new EscapeArgument(parameter, argument, refKind));
            }
        }

        static bool IsMixableParameter(ParameterSymbol parameter)
            => parameter is not null &&
                parameter.type.IsRefLikeOrAllowsRefLikeType() &&
                parameter.refKind.IsWritableReference();

        static bool IsMixableArgument(BoundExpression _) {
            return true;
        }

        static EscapeArgument GetReceiver(in MethodInfo methodInfo, BoundExpression receiver) {
            if (methodInfo.method is not null && methodInfo.setMethod is not null) {
                var getArgument = GetReceiverCore(methodInfo.method, receiver);

                if (getArgument.refKind == RefKind.Ref)
                    return getArgument;

                var setArgument = GetReceiverCore(methodInfo.setMethod, receiver);

                if (setArgument.refKind == RefKind.Ref)
                    return setArgument;

                return getArgument;
            }

            return GetReceiverCore(methodInfo.method, receiver);
        }

        static EscapeArgument GetReceiverCore(MethodSymbol method, BoundExpression receiver) {
            var refKind = RefKind.None;
            ParameterSymbol thisParameter = null;

            if (method is not null &&
                method.TryGetThisParameter(out thisParameter) &&
                thisParameter is not null) {
                if (receiver.type is TemplateParameterSymbol typeParameter)
                    thisParameter = new TemplateParameterThisParameterSymbol(thisParameter, typeParameter);

                refKind = thisParameter.refKind;
            }

            return new EscapeArgument(thisParameter, receiver, refKind);
        }
    }

    private static EscapeLevel? EscapeLevelFromScope(uint scope) => scope switch {
        ReturnOnlyScope => EscapeLevel.ReturnOnly,
        CallingMethodScope => EscapeLevel.CallingMethod,
        _ => null,
    };

    private static uint GetParameterValEscape(ParameterSymbol parameter) {
        return parameter switch {
            _ => CallingMethodScope
        };
    }

    private static EscapeLevel? GetParameterValEscapeLevel(ParameterSymbol parameter)
        => EscapeLevelFromScope(GetParameterValEscape(parameter));

    private static uint GetParameterRefEscape(ParameterSymbol parameter) {
        return parameter switch {
            { refKind: RefKind.None } => CurrentMethodScope,
            { effectiveScope: ScopedKind.ScopedRef } => CurrentMethodScope,
            _ => ReturnOnlyScope
        };
    }

    private static EscapeLevel? GetParameterRefEscapeLevel(ParameterSymbol parameter)
        => EscapeLevelFromScope(GetParameterRefEscape(parameter));

    private bool CheckParameterValEscape(
        SyntaxNode node,
        ParameterSymbol parameter,
        uint escapeTo,
        BelteDiagnosticQueue diagnostics) {
        if (GetParameterValEscape(parameter) > escapeTo) {
            diagnostics.Push(Error.EscapeLocal(node.location, parameter));
            return false;
        }

        return true;
    }

    private bool CheckParameterRefEscape(
        SyntaxNode node,
        BoundExpression parameter,
        ParameterSymbol parameterSymbol,
        uint escapeTo,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        var refSafeToEscape = GetParameterRefEscape(parameterSymbol);

        if (refSafeToEscape > escapeTo) {
            var isRefScoped = parameterSymbol.effectiveScope == ScopedKind.ScopedRef;

            if (parameter is BoundThisExpression) {
                // TODO Reachable?
                // Error(diagnostics, ErrorCode.ERR_RefReturnStructThis, node);
                return false;
            }

            switch ((checkingReceiver, isRefScoped, refSafeToEscape)) {
                case (checkingReceiver: true, isRefScoped: true, _):
                    diagnostics.Push(Error.RefReturnScopedParameter2(parameter.syntax.location, parameterSymbol.name));
                    break;
                case (checkingReceiver: true, isRefScoped: false, ReturnOnlyScope):
                    diagnostics.Push(Error.RefReturnOnlyParameter2(parameter.syntax.location, parameterSymbol.name));
                    break;
                case (checkingReceiver: true, isRefScoped: false, _):
                    diagnostics.Push(Error.RefReturnParameter2(parameter.syntax.location, parameterSymbol.name));
                    break;
                case (checkingReceiver: false, isRefScoped: true, _):
                    diagnostics.Push(Error.RefReturnScopedParameter(node.location, parameterSymbol.name));
                    break;
                case (checkingReceiver: false, isRefScoped: false, ReturnOnlyScope):
                    diagnostics.Push(Error.RefReturnOnlyParameter(node.location, parameterSymbol.name));
                    break;
                case (checkingReceiver: false, isRefScoped: false, _):
                    diagnostics.Push(Error.RefReturnParameter(node.location, parameterSymbol.name));
                    break;
            }

            return false;
        }

        return true;
    }

    private uint GetInvocationEscape(
        in MethodInfo methodInfo,
        BoundExpression receiver,
        ThreeState receiverIsSubjectToCloning,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<BoundExpression> argsOpt,
        ImmutableArray<RefKind> argRefKindsOpt,
        ImmutableArray<int> argsToParamsOpt,
        uint scopeOfTheContainingExpression,
        bool isRefEscape) {
        var escapeScope = CallingMethodScope;

        var argsAndParamsAll = ArrayBuilder<EscapeValue>.GetInstance();

        GetFilteredInvocationArgumentsForEscape(
            methodInfo,
            receiver,
            receiverIsSubjectToCloning,
            parameters,
            argsOpt,
            argRefKindsOpt,
            argsToParamsOpt,
            isRefEscape,
            argsAndParamsAll
        );

        var returnsRefToRefStruct = methodInfo.returnsRefToRefStruct;

        foreach (var (param, argument, _, isArgumentRefEscape) in argsAndParamsAll) {
            if (!returnsRefToRefStruct
                || ((param is null ||
                     (param is { refKind: not RefKind.None, type: { } type } && type.IsRefLikeOrAllowsRefLikeType())) &&
                    isArgumentRefEscape == isRefEscape)) {
                var argEscape = isArgumentRefEscape
                    ? GetRefEscape(argument, scopeOfTheContainingExpression)
                    : GetValEscape(argument, scopeOfTheContainingExpression);

                escapeScope = Math.Max(escapeScope, argEscape);

                if (escapeScope >= scopeOfTheContainingExpression)
                    break;
            }
        }

        argsAndParamsAll.Free();
        return escapeScope;
    }

    private uint GetFieldRefEscape(BoundFieldAccessExpression fieldAccess, uint scopeOfTheContainingExpression) {
        var fieldSymbol = fieldAccess.field;

        if (fieldSymbol.isStatic || fieldSymbol.containingType.isObjectType)
            return CallingMethodScope;

        if (fieldSymbol.refKind != RefKind.None)
            return GetValEscape(fieldAccess.receiver, scopeOfTheContainingExpression);

        return GetRefEscape(fieldAccess.receiver, scopeOfTheContainingExpression);
    }

    private bool CheckFieldRefEscape(
        SyntaxNode node,
        BoundFieldAccessExpression fieldAccess,
        uint escapeFrom,
        uint escapeTo,
        BelteDiagnosticQueue diagnostics) {
        var fieldSymbol = fieldAccess.field;

        if (fieldSymbol.isStatic || fieldSymbol.containingType.isObjectType)
            return true;


        if (fieldSymbol.refKind != RefKind.None)
            return CheckValEscape(node, fieldAccess.receiver, escapeFrom, escapeTo, true, diagnostics);

        return CheckRefEscape(node, fieldAccess.receiver, escapeFrom, escapeTo, true, diagnostics);
    }

    private bool CheckLocalRefEscape(
        SyntaxNode node,
        BoundDataContainerExpression local,
        uint escapeTo,
        bool checkingReceiver,
        BelteDiagnosticQueue diagnostics) {
        var localSymbol = local.dataContainer;

        if (GetLocalScopes(localSymbol).refEscapeScope <= escapeTo)
            return true;

        if (escapeTo is CallingMethodScope or ReturnOnlyScope) {
            if (localSymbol.refKind == RefKind.None) {
                if (checkingReceiver)
                    diagnostics.Push(Error.RefReturnLocal2(local.syntax.location, localSymbol));
                else
                    diagnostics.Push(Error.RefReturnLocal(node.location, localSymbol));

                return false;
            }

            if (checkingReceiver)
                diagnostics.Push(Error.RefReturnNonreturnableLocal2(local.syntax.location, localSymbol));
            else
                diagnostics.Push(Error.RefReturnNonreturnableLocal(node.location, localSymbol));

            return false;
        }

        diagnostics.Push(Error.EscapeLocal(node.location, localSymbol));
        return false;
    }

    private void ValidateAssignment(
        SyntaxNode node,
        BoundExpression op1,
        BoundExpression op2,
        bool isRef,
        BelteDiagnosticQueue diagnostics) {
        if (!op1.hasErrors) {
            var hasErrors = false;

            if (isRef) {
                var leftEscape = GetRefEscape(op1, _localScopeDepth);
                var rightEscape = GetRefEscape(op2, _localScopeDepth);

                if (leftEscape < rightEscape) {
                    if (rightEscape == ReturnOnlyScope)
                        diagnostics.Push(Error.RefAssignReturnOnly(node.location, GetName(op1), op1.syntax));
                    else
                        diagnostics.Push(Error.RefAssignNarrower(node.location, GetName(op1), op2.syntax));

                    hasErrors = true;
                } else if (op1.kind is BoundKind.DataContainerExpression or BoundKind.ParameterExpression) {
                    leftEscape = GetValEscape(op1, _localScopeDepth);
                    rightEscape = GetValEscape(op2, _localScopeDepth);

                    if (leftEscape > rightEscape) {
                        diagnostics.Push(Error.RefAssignValEscapeWider(node.location, GetName(op1), op2.syntax));
                        hasErrors = true;
                    }
                }
            }

            if (!hasErrors && op1.Type().IsRefLikeOrAllowsRefLikeType()) {
                var leftEscape = GetValEscape(op1, _localScopeDepth);
                ValidateEscape(op2, leftEscape, isByRef: false, diagnostics);
            }
        }

        static object GetName(BoundExpression expr) {
            if (expr.expressionSymbol is { name: var name })
                return name;

            if (expr is BoundArrayAccessExpression)
                return MessageID.IDS_ArrayAccess.Localize();

            return "";
        }
    }

    private bool CheckInvocationArgMixing(
        SyntaxNode syntax,
        in MethodInfo methodInfo,
        BoundExpression receiverOpt,
        ThreeState receiverIsSubjectToCloning,
        ImmutableArray<ParameterSymbol> parameters,
        ImmutableArray<BoundExpression> argsOpt,
        ImmutableArray<RefKind> argRefKindsOpt,
        ImmutableArray<int> argsToParamsOpt,
        uint scopeOfTheContainingExpression,
        BelteDiagnosticQueue diagnostics) {
        var mixableArguments = ArrayBuilder<MixableDestination>.GetInstance();
        var escapeValues = ArrayBuilder<EscapeValue>.GetInstance();
        GetEscapeValues(
            methodInfo,
            receiverOpt,
            receiverIsSubjectToCloning,
            parameters,
            argsOpt,
            argRefKindsOpt,
            argsToParamsOpt,
            mixableArguments,
            escapeValues
        );

        var valid = true;

        foreach (var mixableArg in mixableArguments) {
            var toArgEscape = GetValEscape(mixableArg.argument, scopeOfTheContainingExpression);

            foreach (var (fromParameter, fromArg, escapeKind, isRefEscape) in escapeValues) {
                if (mixableArg.parameter is not null && ReferenceEquals(mixableArg.parameter, fromParameter))
                    continue;

                if (!mixableArg.IsAssignableFrom(escapeKind))
                    continue;

                valid = isRefEscape
                    ? CheckRefEscape(
                        fromArg.syntax,
                        fromArg,
                        scopeOfTheContainingExpression,
                        toArgEscape,
                        checkingReceiver: false,
                        diagnostics
                    )
                    : CheckValEscape(
                        fromArg.syntax,
                        fromArg,
                        scopeOfTheContainingExpression,
                        toArgEscape,
                        checkingReceiver: false,
                        diagnostics
                    );

                if (!valid) {
                    var parameterName = GetInvocationParameterName(fromParameter);
                    diagnostics.Push(Error.CallArgMixing(syntax.location, methodInfo.symbol, parameterName));
                    break;
                }
            }

            if (!valid) {
                break;
            }
        }

        InferDeclarationExpressionValEscape();

        mixableArguments.Free();
        escapeValues.Free();
        return valid;

        void InferDeclarationExpressionValEscape() {
            var inferredDestinationValEscape = CallingMethodScope;

            foreach (var (_, fromArg, _, isRefEscape) in escapeValues) {
                inferredDestinationValEscape = Math.Max(inferredDestinationValEscape, isRefEscape
                    ? GetRefEscape(fromArg, scopeOfTheContainingExpression)
                    : GetValEscape(fromArg, scopeOfTheContainingExpression));
            }

            foreach (var argument in argsOpt) {
                if (ShouldInferDeclarationExpressionValEscape(argument, out var localSymbol)) {
                    SetLocalScopes(localSymbol, refEscapeScope: _localScopeDepth, valEscapeScope: inferredDestinationValEscape);
                }
            }
        }
    }

    private bool ShouldInferDeclarationExpressionValEscape(
        BoundExpression argument,
        out SourceDataContainerSymbol localSymbol) {
        var symbol = argument switch {
            BoundDataContainerExpression { dataContainer.declarationKind: not DataContainerDeclarationKind.None } l
                => l.dataContainer,
            _ => null
        };

        if (symbol is SourceDataContainerSymbol local &&
            GetLocalScopes(local).valEscapeScope == CallingMethodScope) {
            localSymbol = local;
            return true;
        } else {
            localSymbol = null;
            return false;
        }
    }

    private void ValidateRefConditionalOperator(
        SyntaxNode node,
        BoundExpression trueExpression,
        BoundExpression falseExpression,
        BelteDiagnosticQueue diagnostics) {
        var currentScope = _localScopeDepth;

        var whenTrueEscape = GetValEscape(trueExpression, currentScope);
        var whenFalseEscape = GetValEscape(falseExpression, currentScope);

        if (whenTrueEscape != whenFalseEscape) {
            if (whenTrueEscape < whenFalseEscape)
                CheckValEscape(falseExpression.syntax, falseExpression, currentScope, whenTrueEscape, false, diagnostics);
            else
                CheckValEscape(trueExpression.syntax, trueExpression, currentScope, whenFalseEscape, false, diagnostics);

            diagnostics.Push(Error.MismatchedRefEscapeInTernary(node.location));
        }
    }
}
