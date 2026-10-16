using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class ILOptimizer {
    internal sealed partial class StackOptimizerPass1 : BoundTreeRewriter {
        internal static readonly DummyLocal Empty = new DummyLocal();

        private readonly bool _debugFriendly;
        private readonly ArrayBuilder<(BoundExpression, ExprContext)> _evalStack;
        private readonly Dictionary<DataContainerSymbol, LocalDefUseInfo> _locals;
        // TODO Perf: use SmallDictionary instead
        private readonly Dictionary<object, DummyLocal> _dummyVariables
            = new Dictionary<object, DummyLocal>(ReferenceEqualityComparer.Instance);

        private int _counter;
        private ExprContext _context;
        private BoundDataContainerExpression _assignmentLocal;
        private int _recursionDepth;

        private bool _sawInlineIL;

        private StackOptimizerPass1(
            Dictionary<DataContainerSymbol, LocalDefUseInfo> locals,
            ArrayBuilder<(BoundExpression, ExprContext)> evalStack,
            bool debugFriendly) {
            _locals = locals;
            _evalStack = evalStack;
            _debugFriendly = debugFriendly;

            DeclareLocal(Empty, 0);
            RecordDummyWrite(Empty);
        }

        internal static BoundNode Analyze(
            BoundNode node,
            Dictionary<DataContainerSymbol, LocalDefUseInfo> locals,
            bool debugFriendly,
            out bool containsInlineIL) {
            var evalStack = ArrayBuilder<(BoundExpression, ExprContext)>.GetInstance();
            var analyzer = new StackOptimizerPass1(locals, evalStack, debugFriendly);
            var rewritten = analyzer.Visit(node);
            evalStack.Free();
            containsInlineIL = analyzer._sawInlineIL;
            return rewritten;
        }

        internal override BoundNode Visit(BoundNode node) {
            BoundNode result;

            if (node is BoundExpression expr) {
                Debug.Assert(expr.kind != BoundKind.LabelStatement);
                result = VisitExpression(expr, ExprContext.Value);
            } else {
                result = VisitStatement(node);
            }

            return result;
        }

        private BoundExpression VisitExpressionCore(BoundExpression node, ExprContext context) {
            var prevContext = _context;
            var prevStack = StackDepth();
            _context = context;

            var result = node.constantValue is null
                ? node = (BoundExpression)base.Visit(node)
                : node;

            _context = prevContext;
            _counter++;

            switch (context) {
                case ExprContext.SideEffects:
                    SetStackDepth(prevStack);
                    break;
                case ExprContext.AssignmentTarget:
                    break;
                case ExprContext.Value:
                case ExprContext.Address:
                case ExprContext.Box:
                    SetStackDepth(prevStack);
                    PushEvalStack(node, context);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(context);
            }

            return result;
        }

        private BoundExpression VisitExpression(BoundExpression node, ExprContext context) {
            BoundExpression result;
            _recursionDepth++;

            if (_recursionDepth > 1) {
                StackGuard.EnsureSufficientExecutionStack(_recursionDepth);
                result = VisitExpressionCore(node, context);
            } else {
                result = VisitExpressionCoreWithStackGuard(node, context);
            }

            _recursionDepth--;
            return result;
        }

        private BoundExpression VisitExpressionCoreWithStackGuard(BoundExpression node, ExprContext context) {
            Debug.Assert(_recursionDepth == 1);

            try {
                var result = VisitExpressionCore(node, context);
                Debug.Assert(_recursionDepth == 1);
                return result;
            } catch (InsufficientExecutionStackException ex) {
                throw new CancelledByStackGuardException(ex, node);
            }
        }

        private protected override BoundExpression VisitExpressionWithoutStackGuard(BoundExpression node) {
            throw ExceptionUtilities.Unreachable();
        }

        private void PushEvalStack(BoundExpression result, ExprContext context) {
            Debug.Assert(result is not null || context == ExprContext.None);
            _evalStack.Add((result, context));
        }

        private int StackDepth() {
            return _evalStack.Count;
        }

        private bool EvalStackIsEmpty() {
            return StackDepth() == 0;
        }

        private void SetStackDepth(int depth) {
            _evalStack.Clip(depth);
        }

        private void PopEvalStack() {
            SetStackDepth(_evalStack.Count - 1);
        }

        internal BoundNode VisitStatement(BoundNode node) {
            Debug.Assert(node is null || EvalStackIsEmpty());
            return VisitSideEffect(node);
        }

        internal BoundNode VisitSideEffect(BoundNode node) {
            var origStack = StackDepth();
            var prevContext = _context;

            var result = base.Visit(node);

            if (_debugFriendly)
                EnsureOnlyEvalStack();

            _context = prevContext;
            SetStackDepth(origStack);
            _counter++;

            return result;
        }

        internal override BoundNode VisitCastExpression(BoundCastExpression node) {
            var context = _context == ExprContext.SideEffects && !node.ConversionHasSideEffects()
                ? ExprContext.SideEffects
                : ExprContext.Value;

            return node.Update(
                VisitExpression(node.operand, context),
                node.conversion,
                node.isChecked,
                node.constantValue,
                node.type
            );
        }

        // TODO PassByCopy

        internal override BoundNode VisitInlineILStatement(BoundInlineILStatement node) {
            _sawInlineIL = true;
            return base.VisitInlineILStatement(node);
        }

        internal override BoundNode VisitBlockStatement(BoundBlockStatement node) {
            Debug.Assert(EvalStackIsEmpty());
            DeclareLocals(node.locals, stack: 0);
            return base.VisitBlockStatement(node);
        }

        internal override BoundNode VisitExpressionStatement(BoundExpressionStatement node) {
            return node.Update(
                VisitExpression(node.expression, ExprContext.SideEffects)
            );
        }

        internal override BoundNode VisitDataContainerExpression(BoundDataContainerExpression node) {
            if (node.constantValue is null) {
                switch (_context) {
                    case ExprContext.Address:
                        if (node.dataContainer.refKind != RefKind.None)
                            RecordVarRead(node.dataContainer);
                        else
                            RecordVarRef(node.dataContainer);

                        break;
                    case ExprContext.AssignmentTarget:
                        Debug.Assert(_assignmentLocal is null);
                        _assignmentLocal = node;
                        break;
                    case ExprContext.SideEffects:
                        if (node.dataContainer.refKind != RefKind.None)
                            RecordVarRead(node.dataContainer);

                        break;
                    case ExprContext.Value:
                    case ExprContext.Box:
                        RecordVarRead(node.dataContainer);
                        break;
                }
            }

            return base.VisitDataContainerExpression(node);
        }

        internal override BoundNode VisitLocalDeclarationStatement(BoundLocalDeclarationStatement node) {
            if (node.declaration.initializer is not null) {
                var local = node.declaration.dataContainer;
                var initializer = node.declaration.initializer;

                var assignment = BoundFactory.Assignment(
                    null,
                    new BoundDataContainerExpression(null, local, null, local.type),
                    initializer,
                    local.isRef,
                    local.type
                );

                var rewritten = (BoundAssignmentOperator)VisitExpression(assignment, ExprContext.SideEffects);
                Debug.Assert(rewritten.left is BoundDataContainerExpression d && d.dataContainer == local);

                if ((object)rewritten != assignment) {
                    return new BoundLocalDeclarationStatement(
                        node.syntax,
                        new BoundDataContainerDeclaration(
                            node.declaration.syntax,
                            local,
                            rewritten.right
                        )
                    );
                }

                return node;
            }

            // Pretend this node doesn't exist
            _counter--;
            return node;
        }

        internal override BoundNode VisitAssignmentOperator(BoundAssignmentOperator node) {
            var isIndirectAssignment = IsIndirectAssignment(node);

            var left = VisitExpression(
                node.left,
                isIndirectAssignment
                    ? ExprContext.Address
                    : ExprContext.AssignmentTarget
            );

            var assignmentLocal = _assignmentLocal;
            _assignmentLocal = null;

            Debug.Assert(_context != ExprContext.AssignmentTarget);

            ExprContext rhsContext;

            if (node.isRef || _context == ExprContext.Address) {
                rhsContext = ExprContext.Address;
            } else {
                Debug.Assert(_context is ExprContext.Value or ExprContext.Box or ExprContext.SideEffects);
                rhsContext = ExprContext.Value;
            }

            var right = node.right;
            var mayPushReceiver = right.kind == BoundKind.ObjectCreationExpression &&
                right.type.IsVerifierValue() &&
                ((BoundObjectCreationExpression)right).constructor.parameterCount != 0;

            if (mayPushReceiver)
                PushEvalStack(null, ExprContext.None);

            right = VisitExpression(node.right, rhsContext);

            if (mayPushReceiver)
                PopEvalStack();

            if (assignmentLocal is not null) {
                Debug.Assert(
                    node.left.type.Equals(node.right.type, TypeCompareKind.RuntimeTypeComparison) ||
                    IsFixedBufferAssignmentToRefLocal(node.left, node.right, node.isRef));

                Debug.Assert(!isIndirectAssignment);
                var dataContainerSymbol = assignmentLocal.dataContainer;

                // TODO RefConst proper to treat as RefReadonly?
                if (dataContainerSymbol.refKind is RefKind.RefConst &&
                    (_context == ExprContext.Address || _context == ExprContext.Value)) {
                    ShouldNotSchedule(dataContainerSymbol);
                } else if (CanScheduleToStack(dataContainerSymbol) &&
                    assignmentLocal.type.IsPointerOrFunctionPointer() && right.kind == BoundKind.CastExpression &&
                    ((BoundCastExpression)right).conversion.kind.IsPointerConversion()) {
                    ShouldNotSchedule(dataContainerSymbol);
                } else if (dataContainerSymbol.refKind != RefKind.None &&
                      dataContainerSymbol.synthesizedKind == SynthesizedLocalKind.UserDefined &&
                      PointerIndirectionMayFlowToRefResultVisitor.Check(right, _recursionDepth)) {
                    ShouldNotSchedule(dataContainerSymbol);
                }

                RecordVarWrite(dataContainerSymbol);
                assignmentLocal = null;
            }

            return node.Update(left, right, node.isRef, node.type);
        }

        internal static bool IsFixedBufferAssignmentToRefLocal(
            BoundExpression left,
            BoundExpression right,
            bool isRef) {
            return isRef &&
                right is BoundFieldAccessExpression fieldAccess &&
                fieldAccess.field.isFixedSizeBuffer &&
                left.type.Equals(((PointerTypeSymbol)right.type).pointedAtType, TypeCompareKind.RuntimeTypeComparison
            );
        }

        private static bool IsIndirectAssignment(BoundAssignmentOperator node) {
            var lhs = node.left;

            Debug.Assert(!node.isRef ||
                (lhs.kind is BoundKind.DataContainerExpression or BoundKind.ParameterExpression or BoundKind.FieldAccessExpression or
                    BoundKind.StackSlotExpression or BoundKind.FieldSlotExpression && lhs.GetRefKind() != RefKind.None));

            switch (lhs.kind) {
                case BoundKind.ThisExpression:
                    Debug.Assert(lhs.type.isValueType);
                    return true;
                case BoundKind.ParameterExpression:
                    if (((BoundParameterExpression)lhs).parameter.refKind != RefKind.None)
                        return !node.isRef;

                    return false;
                case BoundKind.DataContainerExpression:
                    if (((BoundDataContainerExpression)lhs).dataContainer.refKind != RefKind.None)
                        return !node.isRef;

                    return false;
                case BoundKind.StackSlotExpression:
                    if (((BoundStackSlotExpression)lhs).symbol.GetRefKind() != RefKind.None)
                        return !node.isRef;

                    return false;
                case BoundKind.CallExpression:
                    Debug.Assert(((BoundCallExpression)lhs).method.refKind == RefKind.Ref);
                    return true;
                case BoundKind.FunctionPointerCallExpression:
                    Debug.Assert(((BoundFunctionPointerCallExpression)lhs).functionPointer.signature.refKind == RefKind.Ref);
                    return true;
                case BoundKind.ConditionalOperator:
                    Debug.Assert(((BoundConditionalOperator)lhs).isRef);
                    return true;
                case BoundKind.AssignmentOperator:
                    Debug.Assert(((BoundAssignmentOperator)lhs).isRef);
                    return true;
                // TODO RefValueOperator
                // case BoundKind.RefValueOperator:
                case BoundKind.PointerIndirectionOperator:
                    // TODO PseudoVariable
                    // case BoundKind.PseudoVariable:
                    return true;
                case BoundKind.FieldAccessExpression:
                case BoundKind.FieldSlotExpression:
                case BoundKind.ArrayAccessExpression:
                    return false;
                default:
                    throw ExceptionUtilities.UnexpectedValue(lhs.kind);
            }
        }

        private static bool IsIndirectOrInstanceFieldAssignment(BoundAssignmentOperator node) {
            var lhs = node.left;

            if (lhs.kind == BoundKind.FieldAccessExpression)
                return !((BoundFieldAccessExpression)lhs).field.isStatic;

            return IsIndirectAssignment(node);
        }

        internal override BoundNode VisitCallExpression(BoundCallExpression node) {
            if (node.receiver is BoundCallExpression receiver) {
                var prevStack = StackDepth();
                var calls = ArrayBuilder<BoundCallExpression>.GetInstance();

                calls.Push(node);

                node = receiver;

                while (node.receiver is BoundCallExpression receiver2) {
                    calls.Push(node);
                    node = receiver2;
                }

                var rewrittenReceiver = VisitReceiver(node);

                while (true) {
                    rewrittenReceiver = VisitArgumentsAndUpdateCall(node, rewrittenReceiver);
                    receiver = node;

                    if (!calls.TryPop(out node))
                        break;

                    CheckCallReceiver(receiver, node);

                    _counter++;
                    SetStackDepth(prevStack);
                    PushEvalStack(receiver, GetReceiverContext(receiver));
                }

                calls.Free();

                return rewrittenReceiver;
            } else {
                var rewrittenReceiver = VisitReceiver(node);
                return VisitArgumentsAndUpdateCall(node, rewrittenReceiver);
            }

            BoundExpression VisitReceiver(BoundCallExpression node) {
                var receiver = node.receiver;
                var method = node.method;

                if (method.requiresInstanceReceiver) {
                    receiver = VisitCallOrConditionalAccessReceiver(receiver, node);
                } else {
                    Debug.Assert(method.isStatic);
                    _counter++;

                    if ((method.isAbstract || method.isVirtual) && receiver is
                        BoundTypeExpression { type.typeKind: TypeKind.TemplateParameter } typeExpression) {
                        receiver = typeExpression.Update(
                            typeExpression.typeWithAnnotations,
                            alias: null,
                            VisitType(typeExpression.type)
                        );
                    } else {
                        receiver = null;
                    }
                }

                return receiver;
            }

            BoundCallExpression VisitArgumentsAndUpdateCall(BoundCallExpression node, BoundExpression receiver) {
                var rewrittenArguments = VisitArguments(node.arguments, node.method.parameters, node.argumentRefKinds);

                return node.Update(
                    receiver,
                    node.method,
                    rewrittenArguments,
                    node.argumentRefKinds,
                    node.defaultArguments,
                    node.resultKind,
                    node.type
                );
            }
        }

        private BoundExpression VisitCallOrConditionalAccessReceiver(
            BoundExpression receiver,
            BoundCallExpression callOpt) {
            var receiverType = receiver.type;

            if (callOpt is { } call)
                CheckCallReceiver(receiver, call);

            var context = GetReceiverContext(receiver);

            receiver = VisitExpression(receiver, context);
            return receiver;
        }

        private void CheckCallReceiver(BoundExpression receiver, BoundCallExpression call) {
            if (CodeGenerator.IsRef(receiver) &&
                    CodeGenerator.IsPossibleReferenceTypeReceiverOfConstrainedCall(receiver) &&
                    !CodeGenerator.IsSafeToDereferenceReceiverRefAfterEvaluatingArguments(call.arguments)) {
                if (receiver is
                    BoundDataContainerExpression { dataContainer: { refKind: not RefKind.None } dataContainerSymbol }) {
                    ShouldNotSchedule(dataContainerSymbol);
                } else if (receiver is
                    BoundStackSlotExpression { original: BoundDataContainerExpression { dataContainer: { refKind: not RefKind.None } stackSymbol } }) {
                    ShouldNotSchedule(stackSymbol);
                }
            }
        }

        private static ExprContext GetReceiverContext(BoundExpression receiver) {
            var receiverType = receiver.type;
            ExprContext context;

            if (receiverType.isReferenceType) {
                if (receiverType.IsTemplateParameter())
                    context = ExprContext.Box;
                else
                    context = ExprContext.Value;
            } else {
                context = ExprContext.Address;
            }

            return context;
        }

        private ImmutableArray<BoundExpression> VisitArguments(
            ImmutableArray<BoundExpression> arguments,
            ImmutableArray<ParameterSymbol> parameters,
            ImmutableArray<RefKind> argRefKindsOpt) {
            Debug.Assert(!arguments.IsDefault);
            Debug.Assert(!parameters.IsDefault);
            Debug.Assert(arguments.Length == parameters.Length || arguments.Length == parameters.Length + 1);

            ArrayBuilder<BoundExpression> rewrittenArguments = null;

            for (var i = 0; i < arguments.Length; i++) {
                var argRefKind = CodeGenerator.GetArgumentRefKind(arguments, parameters, argRefKindsOpt, i);
                VisitArgument(arguments, ref rewrittenArguments, i, argRefKind);
            }

            return rewrittenArguments != null ? rewrittenArguments.ToImmutableAndFree() : arguments;
        }

        private void VisitArgument(
            ImmutableArray<BoundExpression> arguments,
            ref ArrayBuilder<BoundExpression> rewrittenArguments,
            int i,
            RefKind argRefKind) {
            var context = (argRefKind == RefKind.None) ? ExprContext.Value : ExprContext.Address;

            var arg = arguments[i];
            var rewrittenArg = VisitExpression(arg, context);

            if (rewrittenArguments is null && arg != rewrittenArg) {
                rewrittenArguments = ArrayBuilder<BoundExpression>.GetInstance();
                rewrittenArguments.AddRange(arguments, i);
            }

            rewrittenArguments?.Add(rewrittenArg);
        }

        // TODO ArgListOperator
        // TODO MakeRefOperator

        internal override BoundNode VisitObjectCreationExpression(BoundObjectCreationExpression node) {
            var constructor = node.constructor;
            var rewrittenArguments = VisitArguments(node.arguments, constructor.parameters, node.argumentRefKinds);

            return node.Update(
                constructor,
                rewrittenArguments,
                node.argumentRefKinds,
                node.argsToParams,
                node.defaultArguments,
                node.wasTargetTyped,
                node.type
            );
        }

        internal override BoundNode VisitArrayAccessExpression(BoundArrayAccessExpression node) {
            var oldContext = _context;
            _context = ExprContext.Value;

            var result = base.VisitArrayAccessExpression(node);

            _context = oldContext;
            return result;
        }

        internal override BoundNode VisitFieldAccessExpression(BoundFieldAccessExpression node) {
            var field = node.field;
            var receiver = node.receiver;

            if (!field.isStatic) {
                if (receiver.type.IsTemplateParameter()) {
                    receiver = VisitExpression(receiver, ExprContext.Box);
                } else {
                    if (receiver.type.isValueType && (
                        _context == ExprContext.AssignmentTarget ||
                        _context == ExprContext.Address ||
                        CodeGenerator.FieldLoadMustUseRef(receiver))) {
                        receiver = VisitExpression(receiver, ExprContext.Address);
                    } else {
                        receiver = VisitExpression(receiver, ExprContext.Value);
                    }
                }
            } else {
                _counter++;
                receiver = null;
            }

            return node.Update(receiver, field, node.constantValue, node.type);
        }

        internal override BoundNode VisitLabelStatement(BoundLabelStatement node) {
            RecordLabel(node.label);
            return base.VisitLabelStatement(node);
        }

        internal override BoundNode VisitIsPatternExpression(BoundIsPatternExpression node) {
            Debug.Assert(false);
            return node;
        }

        internal override BoundNode VisitGotoStatement(BoundGotoStatement node) {
            Debug.Assert(node.caseExpression is null);

            var result = base.VisitGotoStatement(node);
            RecordBranch(node.label);

            return result;
        }

        internal override BoundNode VisitConditionalGotoStatement(BoundConditionalGotoStatement node) {
            var result = base.VisitConditionalGotoStatement(node);
            PopEvalStack();
            RecordBranch(node.label);
            return result;
        }

        internal override BoundNode VisitSwitchDispatch(BoundSwitchDispatch node) {
            Debug.Assert(EvalStackIsEmpty());

            var boundExpression = node.expression;

            if (boundExpression.kind == BoundKind.DataContainerExpression) {
                var localSym = ((BoundDataContainerExpression)boundExpression).dataContainer;

                if (localSym.refKind == RefKind.None)
                    ShouldNotSchedule(localSym);
            } else if (boundExpression.kind == BoundKind.StackSlotExpression) {
                var localSym = (DataContainerSymbol)((BoundStackSlotExpression)boundExpression).symbol;

                if (localSym.refKind == RefKind.None)
                    ShouldNotSchedule(localSym);
            }

            boundExpression = (BoundExpression)Visit(boundExpression);

            PopEvalStack();
            EnsureOnlyEvalStack();

            RecordBranch(node.defaultLabel);

            foreach ((_, var label) in node.cases)
                RecordBranch(label);

            return node.Update(boundExpression, node.cases, node.defaultLabel);
        }

        internal override BoundNode VisitConditionalOperator(BoundConditionalOperator node) {
            var origStack = StackDepth();
            var condition = VisitExpression(node.condition, ExprContext.Value);

            var cookie = GetStackStateCookie();

            var context = node.isRef ? ExprContext.Address : ExprContext.Value;

            SetStackDepth(origStack);
            var consequence = VisitExpression(node.trueExpression, context);

            EnsureStackState(cookie);

            SetStackDepth(origStack);
            var alternative = VisitExpression(node.falseExpression, context);

            EnsureStackState(cookie);

            return node.Update(
                condition,
                node.isRef,
                consequence,
                alternative,
                node.constantValue,
                node.type
            );
        }

        internal override BoundNode VisitBinaryOperator(BoundBinaryOperator node) {
            var child = node.left;

            if (child.kind != BoundKind.BinaryOperator || child.constantValue is not null)
                return VisitBinaryOperatorSimple(node);

            var stack = ArrayBuilder<BoundBinaryOperator>.GetInstance();
            stack.Push(node);

            var binary = (BoundBinaryOperator)child;

            while (true) {
                stack.Push(binary);
                child = binary.left;

                if (child.kind != BoundKind.BinaryOperator || child.constantValue is not null)
                    break;

                binary = (BoundBinaryOperator)child;
            }

            var prevContext = _context;
            var prevStack = StackDepth();

            var left = (BoundExpression)Visit(child);

            while (true) {
                binary = stack.Pop();

                var isLogical = (binary.operatorKind & BinaryOperatorKind.Conditional) != 0;

                object cookie = null;

                if (isLogical) {
                    Debug.Assert(false);
                    cookie = GetStackStateCookie();
                    SetStackDepth(prevStack);
                }

                var right = (BoundExpression)Visit(binary.right);

                if (isLogical)
                    EnsureStackState(cookie);

                var type = VisitType(binary.type);
                left = binary.Update(
                    left,
                    right,
                    binary.operatorKind,
                    binary.method,
                    binary.constantValue,
                    type
                );

                if (stack.Count == 0)
                    break;

                _context = prevContext;
                _counter++;
                SetStackDepth(prevStack);
                PushEvalStack(binary, ExprContext.Value);
            }

            Debug.Assert((object)binary == node);
            stack.Free();

            return left;
        }

        private BoundNode VisitBinaryOperatorSimple(BoundBinaryOperator node) {
            var isLogical = (node.operatorKind & BinaryOperatorKind.Conditional) != 0;

            if (isLogical) {
                var origStack = StackDepth();
                var left = (BoundExpression)Visit(node.left);

                var cookie = GetStackStateCookie();

                SetStackDepth(origStack);
                var right = (BoundExpression)Visit(node.right);

                EnsureStackState(cookie);

                return node.Update(
                    left,
                    right,
                    node.operatorKind,
                    node.method,
                    node.constantValue,
                    node.type
                );
            }

            return base.VisitBinaryOperator(node);
        }

        internal override BoundNode VisitNullCoalescingOperator(BoundNullCoalescingOperator node) {
            Debug.Assert(false);
            var origStack = StackDepth();
            var left = (BoundExpression)Visit(node.left);

            var cookie = GetStackStateCookie();

            SetStackDepth(origStack);
            var right = (BoundExpression)Visit(node.right);

            EnsureStackState(cookie);

            return node.Update(
                left,
                right,
                node.isPropagation,
                node.constantValue,
                node.type
            );
        }

        internal override BoundNode VisitUnaryOperator(BoundUnaryOperator node) {
            if (node.operatorKind.IsChecked() && node.operatorKind.Operator() == UnaryOperatorKind.UnaryMinus) {
                var origStack = StackDepth();
                PushEvalStack(
                    new BoundDefaultExpression(node.syntax, false, null, null, node.operand.type),
                    ExprContext.Value
                );

                var operand = (BoundExpression)Visit(node.operand);
                return node.Update(operand, node.operatorKind, node.method, node.constantValue, node.type);
            } else {
                return base.VisitUnaryOperator(node);
            }
        }

        internal override BoundNode VisitTryStatement(BoundTryStatement node) {
            EnsureOnlyEvalStack();
            var tryBlock = (BoundBlockStatement)Visit(node.body);
            var catchBody = (BoundBlockStatement)Visit(node.catchBody);

            EnsureOnlyEvalStack();
            var finallyBlock = (BoundBlockStatement)Visit(node.finallyBody);

            EnsureOnlyEvalStack();

            return node.Update(tryBlock, catchBody, finallyBlock);
        }

        internal override BoundNode VisitConvertedStackAllocExpression(BoundConvertedStackAllocExpression node) {
            EnsureOnlyEvalStack();
            return base.VisitConvertedStackAllocExpression(node);
        }
        internal override BoundNode VisitAddressOfOperator(BoundAddressOfOperator node) {
            var visitedOperand = VisitExpression(node.operand, ExprContext.Address);
            return node.Update(visitedOperand, node.isLoweredFixedField, node.type);
        }

        internal override BoundNode VisitReturnStatement(BoundReturnStatement node) {
            var expressionOpt = (BoundExpression)Visit(node.expression);
            EnsureOnlyEvalStack();
            return node.Update(node.refKind, expressionOpt);
        }

        private void EnsureOnlyEvalStack() {
            RecordVarRead(Empty);
        }

        private object GetStackStateCookie() {
            var dummy = new DummyLocal();
            _dummyVariables.Add(dummy, dummy);
            _locals.Add(dummy, LocalDefUseInfo.GetInstance(StackDepth()));
            RecordDummyWrite(dummy);
            return dummy;
        }

        private void EnsureStackState(object cookie) {
            RecordVarRead(_dummyVariables[cookie]);
        }

        private void RecordBranch(LabelSymbol label) {
            if (_dummyVariables.TryGetValue(label, out var dummy)) {
                RecordVarRead(dummy);
            } else {
                dummy = new DummyLocal();
                _dummyVariables.Add(label, dummy);
                _locals.Add(dummy, LocalDefUseInfo.GetInstance(StackDepth()));
                RecordDummyWrite(dummy);
            }
        }

        private void RecordLabel(LabelSymbol label) {
            if (_dummyVariables.TryGetValue(label, out var dummy)) {
                RecordVarRead(dummy);
            } else {
                dummy = Empty;
                _dummyVariables.Add(label, dummy);
                RecordVarRead(dummy);
            }
        }

        private void ShouldNotSchedule(DataContainerSymbol dataContainerSymbol) {
            if (_locals.TryGetValue(dataContainerSymbol, out var localDefInfo))
                localDefInfo.ShouldNotSchedule();
        }

        private void RecordVarRef(DataContainerSymbol local) {
            Debug.Assert(local.refKind == RefKind.None);

            if (!CanScheduleToStack(local))
                return;

            ShouldNotSchedule(local);
        }

        private void RecordVarRead(DataContainerSymbol local) {
            if (!CanScheduleToStack(local))
                return;

            var locInfo = _locals[local];

            if (locInfo.cannotSchedule)
                return;

            var defs = locInfo.localDefs;

            if (defs.Count == 0) {
                locInfo.ShouldNotSchedule();
                return;
            }

            if (local.synthesizedKind != SynthesizedLocalKind.OptimizerTemp) {
                if (locInfo.stackAtDeclaration != StackDepth() &&
                    !EvalStackHasLocal(local)) {
                    locInfo.ShouldNotSchedule();
                    return;
                }
            } else {
                Debug.Assert(local == Empty || locInfo.stackAtDeclaration == StackDepth());
            }

            var last = defs.Count - 1;
            defs[last] = defs[last].WithEnd(_counter);

            var nextDef = new LocalDefUseSpan(_counter);
            defs.Add(nextDef);
        }

        private bool EvalStackHasLocal(DataContainerSymbol local) {
            var top = _evalStack.Last();

            return top.Item2 == (local.refKind == RefKind.None ? ExprContext.Value : ExprContext.Address) &&
                ((top.Item1.kind == BoundKind.DataContainerExpression &&
                 ((BoundDataContainerExpression)top.Item1).dataContainer == local) ||
                (top.Item1.kind == BoundKind.StackSlotExpression &&
                 ((BoundStackSlotExpression)top.Item1).symbol == local));
        }

        private void RecordDummyWrite(DataContainerSymbol local) {
            Debug.Assert(local.synthesizedKind == SynthesizedLocalKind.OptimizerTemp);

            var locInfo = _locals[local];

            Debug.Assert(local == Empty || locInfo.stackAtDeclaration == StackDepth());

            var locDef = new LocalDefUseSpan(_counter);
            locInfo.localDefs.Add(locDef);
        }

        private void RecordVarWrite(DataContainerSymbol local) {
            Debug.Assert(local.synthesizedKind != SynthesizedLocalKind.OptimizerTemp);

            if (!CanScheduleToStack(local))
                return;

            var locInfo = _locals[local];

            if (locInfo.cannotSchedule)
                return;

            var evalStack = StackDepth() - 1;

            if (locInfo.stackAtDeclaration != evalStack) {
                locInfo.ShouldNotSchedule();
                return;
            }

            var locDef = new LocalDefUseSpan(_counter);
            locInfo.localDefs.Add(locDef);
        }

        private bool CanScheduleToStack(DataContainerSymbol local) {
            return local.canScheduleToStack &&
                (!_debugFriendly || !local.synthesizedKind.IsLongLived());
        }

        private void DeclareLocals(ImmutableArray<DataContainerSymbol> locals, int stack) {
            foreach (var local in locals)
                DeclareLocal(local, stack);
        }

        private void DeclareLocal(DataContainerSymbol local, int stack) {
            if (local is not null) {
                if (CanScheduleToStack(local)) {
                    if (!_locals.TryGetValue(local, out var info)) {
                        _locals.Add(local, LocalDefUseInfo.GetInstance(stack));
                    } else {
                        Debug.Assert(false);

                        if (info.stackAtDeclaration != stack)
                            info.ShouldNotSchedule();
                    }
                }
            }
        }
    }
}
