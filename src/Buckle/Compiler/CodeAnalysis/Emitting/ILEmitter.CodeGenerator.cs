using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Mono.Cecil;
using Mono.Cecil.Cil;
using static Buckle.CodeAnalysis.Binding.Binder;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    internal sealed partial class CodeGenerator {
        private static readonly OpCode[] CompOpCodes = [
            //  <            <=               >                >=
            OpCodes.Clt,    OpCodes.Cgt,    OpCodes.Cgt,    OpCodes.Clt,     // Signed
            OpCodes.Clt_Un, OpCodes.Cgt_Un, OpCodes.Cgt_Un, OpCodes.Clt_Un,  // Unsigned
            OpCodes.Clt,    OpCodes.Cgt_Un, OpCodes.Cgt,    OpCodes.Clt_Un,  // Float
        ];

        private const int IL_OP_CODE_ROW_LENGTH = 4;

        private static readonly OpCode[] CondJumpOpCodes = [
            //  <            <=               >                >=
            OpCodes.Blt,    OpCodes.Ble,    OpCodes.Bgt,    OpCodes.Bge,     // Signed
            OpCodes.Bge,    OpCodes.Bgt,    OpCodes.Ble,    OpCodes.Blt,     // Signed Invert
            OpCodes.Blt_Un, OpCodes.Ble_Un, OpCodes.Bgt_Un, OpCodes.Bge_Un,  // Unsigned
            OpCodes.Bge_Un, OpCodes.Bgt_Un, OpCodes.Ble_Un, OpCodes.Blt_Un,  // Unsigned Invert
            OpCodes.Blt,    OpCodes.Ble,    OpCodes.Bgt,    OpCodes.Bge,     // Float
            OpCodes.Bge_Un, OpCodes.Bgt_Un, OpCodes.Ble_Un, OpCodes.Blt_Un,  // Float Invert
        ];

        private readonly ILEmitter _module;
        private readonly MethodSymbol _method;
        private readonly BoundBlockStatement _body;
        private readonly MethodDefinition _definition;
        private readonly ILProcessor _iLProcessor;
        private readonly Dictionary<DataContainerSymbol, VariableDefinition> _locals = [];
        private readonly HashSet<DataContainerSymbol> _stackLocals = [];
        private readonly Dictionary<LabelSymbol, int> _labels = [];
        private readonly List<(int instructionIndex, LabelSymbol target)> _unhandledGotos = [];

        private ArrayBuilder<VariableDefinition> _expressionTemps;
        private VariableDefinition _returnTemp;
        private int _tryNestingLevel;

        internal CodeGenerator(
            ILEmitter module,
            MethodSymbol method,
            BoundBlockStatement methodBody,
            MethodDefinition methodDefinition) {
            _module = module;
            _method = method;
            _body = methodBody;
            _definition = methodDefinition;
            _iLProcessor = methodDefinition.Body.GetILProcessor();
        }

        private int _count => _iLProcessor.Body.Instructions.Count;

        private VariableDefinition _lazyReturnTemp {
            get {
                _returnTemp ??= AllocateTemp(_method.returnType);
                return _returnTemp;
            }
        }

        internal void Generate() {
            foreach (var statement in _body.statements)
                EmitStatement(statement);

            foreach (var (instructionIndex, target) in _unhandledGotos) {
                var targetLabel = target;
                var targetInstructionIndex = _labels[targetLabel];
                var targetInstruction = _iLProcessor.Body.Instructions[targetInstructionIndex];
                var instructionFix = _iLProcessor.Body.Instructions[instructionIndex];
                instructionFix.Operand = targetInstruction;
            }
        }

        internal static bool IsReferenceType(TypeSymbol type) {
            return (type.isObjectType && type.specialType != SpecialType.Nullable) ||
                type.specialType == SpecialType.String;
        }

        internal static bool IsValueType(TypeSymbol type) {
            return (type.isPrimitiveType || type.specialType == SpecialType.Nullable) &&
                type.specialType != SpecialType.String;
        }

        internal static bool IsStackLocal(DataContainerSymbol local, HashSet<DataContainerSymbol> stackLocals) {
            return stackLocals?.Contains(local) ?? false;
        }

        private bool IsStackLocal(DataContainerSymbol local) {
            return IsStackLocal(local, _stackLocals);
        }

        private VariableDefinition GetLocal(DataContainerSymbol local) {
            return _locals[local];
        }

        private ParameterDefinition GetParameter(ParameterSymbol parameter) {
            var slot = parameter.ordinal;

            if (!_method.isStatic)
                slot++;

            return _definition.Parameters[slot];
        }

        private void EnsureGlobalsClassIsBuilt() {
            if (_module._globalsClass is null)
                _module.EmitGlobalsClass();
        }

        private VariableDefinition EmitAddress(BoundExpression expression, AddressKind addressKind) {
            switch (expression.kind) {
                case BoundKind.DataContainerExpression:
                    return EmitLocalAddress((BoundDataContainerExpression)expression, addressKind);
                case BoundKind.ParameterExpression:
                    return EmitParameterAddress((BoundParameterExpression)expression, addressKind);
                case BoundKind.FieldAccessExpression:
                    return EmitFieldAddress((BoundFieldAccessExpression)expression, addressKind);
                case BoundKind.ArrayAccessExpression:
                    if (!HasHome(expression, addressKind))
                        goto default;

                    EmitArrayElementAddress((BoundArrayAccessExpression)expression, addressKind);
                    break;
                case BoundKind.ThisExpression:
                    if (IsValueType(expression.type)) {
                        if (!HasHome(expression, addressKind))
                            goto default;

                        EmitLdarg0();
                    } else {
                        EmitLdarga0();
                    }

                    break;
                case BoundKind.BaseExpression:
                    break;
                case BoundKind.CallExpression:
                    var call = (BoundCallExpression)expression;

                    if (UseCallResultAsAddress(call, addressKind)) {
                        EmitCallExpression(call, UseKind.UsedAsAddress);
                        break;
                    }

                    goto default;
                case BoundKind.ConditionalOperator:
                    if (!HasHome(expression, addressKind))
                        goto default;

                    EmitConditionalOperatorAddress((BoundConditionalOperator)expression, addressKind);
                    break;
                case BoundKind.AssignmentOperator:
                    var assignment = (BoundAssignmentOperator)expression;

                    if (!assignment.isRef || !HasHome(assignment, addressKind)) {
                        goto default;
                    } else {
                        EmitAssignmentOperator(assignment, UseKind.UsedAsAddress);
                        break;
                    }
                case BoundKind.ThrowExpression:
                    EmitExpression(expression, used: true);
                    return null;
                default:
                    return EmitAddressOfTempClone(expression);
            }

            return null;
        }

        private static bool UseCallResultAsAddress(BoundCallExpression call, AddressKind addressKind) {
            var methodRefKind = call.method.refKind;
            return methodRefKind == RefKind.Ref ||
                   (IsAnyReadOnly(addressKind) && methodRefKind == RefKind.RefConst);
        }

        private void EmitConditionalOperatorAddress(BoundConditionalOperator expression, AddressKind addressKind) {
            LabelSymbol consequenceLabel = new SynthesizedLabelSymbol("consequence");
            LabelSymbol doneLabel = new SynthesizedLabelSymbol("done");

            EmitConditionalBranch(expression.condition, ref consequenceLabel, sense: true);
            AddExpressionTemp(EmitAddress(expression.falseExpression, addressKind));

            EmitBranch(OpCodes.Br, doneLabel);

            MarkLabel(consequenceLabel);
            AddExpressionTemp(EmitAddress(expression.trueExpression, addressKind));

            MarkLabel(doneLabel);
        }

        private void AddExpressionTemp(VariableDefinition temp) {
            if (temp is null)
                return;

            var exprTemps = _expressionTemps;

            if (exprTemps is null) {
                exprTemps = ArrayBuilder<VariableDefinition>.GetInstance();
                _expressionTemps = exprTemps;
            }

            exprTemps.Add(temp);
        }

        private void EmitArrayElementAddress(BoundArrayAccessExpression arrayAccess, AddressKind addressKind) {
            EmitExpression(arrayAccess.receiver, used: true);
            EmitArrayIndex(arrayAccess.index);

            if (ShouldEmitReadOnlyPrefix(arrayAccess, addressKind))
                _iLProcessor.Emit(OpCodes.Readonly);

            if (((ArrayTypeSymbol)arrayAccess.receiver.type).isSZArray) {
                _iLProcessor.Emit(OpCodes.Ldelema, _module.GetType(arrayAccess.type));
            } else {
                // TODO We only have SZ arrays currently?
                // _builder.EmitArrayElementAddress(_module.Translate((ArrayTypeSymbol)arrayAccess.Expression.Type),
                //                                 arrayAccess.Syntax, _diagnostics.DiagnosticBag);
            }
        }

        private bool ShouldEmitReadOnlyPrefix(BoundArrayAccessExpression arrayAccess, AddressKind addressKind) {
            if (addressKind == AddressKind.Constrained)
                return true;

            if (!IsAnyReadOnly(addressKind))
                return false;

            return !IsValueType(arrayAccess.type);
        }

        private void EmitArrayIndex(BoundExpression index) {
            EmitExpression(index, used: true);
            TreatLongsAsNative(index.type.specialType);
        }

        private void EmitArrayIndices(ImmutableArray<BoundExpression> indices) {
            for (var i = 0; i < indices.Length; ++i) {
                var index = indices[i];
                EmitExpression(index, used: true);
                TreatLongsAsNative(index.type.specialType);
            }
        }

        private void EmitArrayIndices(ImmutableArray<int> indices) {
            for (var i = 0; i < indices.Length; ++i) {
                var index = indices[i];
                _iLProcessor.Emit(OpCodes.Ldc_I4, index);
            }
        }

        private void TreatLongsAsNative(SpecialType specialType) {
            if (specialType == SpecialType.Int)
                _iLProcessor.Emit(OpCodes.Conv_Ovf_I);
        }

        private VariableDefinition EmitLocalAddress(BoundDataContainerExpression localAccess, AddressKind addressKind) {
            var local = localAccess.dataContainer;

            if (!HasHome(localAccess, addressKind))
                return EmitAddressOfTempClone(localAccess);

            if (IsStackLocal(local)) {
                if (local.refKind == RefKind.None)
                    throw ExceptionUtilities.UnexpectedValue(local.refKind);
            } else {
                EmitLdloca(GetLocal(local));
            }

            return null;
        }

        private VariableDefinition EmitParameterAddress(BoundParameterExpression parameter, AddressKind addressKind) {
            var parameterSymbol = parameter.parameter;

            if (!HasHome(parameter, addressKind))
                return EmitAddressOfTempClone(parameter);

            if (parameterSymbol.refKind == RefKind.None)
                EmitLdarga(GetParameter(parameterSymbol));
            else
                EmitLdarg(GetParameter(parameterSymbol));

            return null;
        }

        private VariableDefinition EmitFieldAddress(BoundFieldAccessExpression fieldAccess, AddressKind addressKind) {
            var field = fieldAccess.field;

            if (!HasHome(fieldAccess, addressKind)) {
                return EmitAddressOfTempClone(fieldAccess);
            } else if (field.isStatic) {
                EmitStaticFieldAddress(field);
                return null;
            } else {
                return EmitInstanceFieldAddress(fieldAccess, addressKind);
            }
        }

        private void EmitStaticFieldAddress(FieldSymbol field) {
            _iLProcessor.Emit(OpCodes.Ldsflda, _module.GetField(field));
        }

        private VariableDefinition EmitInstanceFieldAddress(
            BoundFieldAccessExpression fieldAccess,
            AddressKind addressKind) {
            var field = fieldAccess.field;

            var tempOpt = EmitReceiverRef(
                fieldAccess.receiver,
                field.refKind == RefKind.None
                    ? (addressKind == AddressKind.Constrained ? AddressKind.Writeable : addressKind)
                    : (addressKind != AddressKind.ReadOnlyStrict ? AddressKind.ReadOnly : addressKind)
                );

            _iLProcessor.Emit(field.refKind == RefKind.None ? OpCodes.Ldflda : OpCodes.Ldfld, _module.GetField(field));
            return tempOpt;
        }

        private VariableDefinition EmitReceiverRef(BoundExpression receiver, AddressKind addressKind) {
            var receiverType = receiver.type;

            if (receiverType.IsVerifierReference()) {
                EmitExpression(receiver, used: true);
                return null;
            }

            if (BoxNonVerifierReferenceReceiver(receiverType, addressKind)) {
                EmitExpression(receiver, used: true);
                EmitBox(receiver.type);
                return null;
            }

            return EmitAddress(receiver, addressKind);
        }

        private static bool BoxNonVerifierReferenceReceiver(TypeSymbol receiverType, AddressKind addressKind) {
            return receiverType.typeKind == TypeKind.TemplateParameter && addressKind != AddressKind.Constrained;
        }

        private VariableDefinition EmitAddressOfTempClone(BoundExpression expression) {
            EmitExpression(expression, true);
            var value = AllocateTemp(expression.type);
            EmitStloc(value);
            EmitLdloca(value);
            return value;
        }

        private void EmitLdarg(ParameterDefinition parameter) {
            _iLProcessor.Emit(OpCodes.Ldarg, parameter);
        }

        private void EmitLdarga(ParameterDefinition parameter) {
            _iLProcessor.Emit(OpCodes.Ldarga, parameter);
        }

        private void EmitLdarg0() {
            _iLProcessor.Emit(OpCodes.Ldarg_0);
        }

        private void EmitLdarga0() {
            // TODO How do we get the this parameter?
            // _iLProcessor.Emit(OpCodes.Ldarga);
            EmitLdarg0();
        }

        private void EmitLdloc(VariableDefinition local) {
            _iLProcessor.Emit(OpCodes.Ldloc, local);
        }

        private void EmitStloc(VariableDefinition local) {
            _iLProcessor.Emit(OpCodes.Stloc, local);
        }

        private void EmitLdloca(VariableDefinition local) {
            if (local.VariableType.IsByReference)
                EmitLdloc(local);
            else
                _iLProcessor.Emit(OpCodes.Ldloca, local);
        }

        private void EmitInitObj(TypeSymbol type, bool used) {
            if (used) {
                var temp = AllocateTemp(type);
                EmitLdloca(temp);
                _iLProcessor.Emit(OpCodes.Initobj, _module.GetType(type));
                EmitLdloc(temp);
                FreeTemp(temp);
            }
        }

        private void EmitConstantValue(ConstantValue constant) {
            if (constant.value is null) {
                _iLProcessor.Emit(OpCodes.Ldnull);
                return;
            }

            switch (constant.specialType) {
                case SpecialType.Int:
                    _iLProcessor.Emit(OpCodes.Ldc_I8, (long)constant.value);
                    break;
                case SpecialType.Bool:
                    EmitBoolConstant((bool)constant.value);
                    break;
                case SpecialType.Decimal:
                    _iLProcessor.Emit(OpCodes.Ldc_R8, (double)constant.value);
                    break;
                case SpecialType.String:
                    _iLProcessor.Emit(OpCodes.Ldstr, (string)constant.value);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(constant.specialType);
            }
        }

        private void EmitBoolConstant(bool value) {
            _iLProcessor.Emit(OpCodes.Ldc_I4, value ? 1 : 0);
        }

        private VariableDefinition AllocateTemp(TypeSymbol type) {
            var typeReference = _module.GetType(type);
            var variableDefinition = new VariableDefinition(typeReference);
            _iLProcessor.Body.Variables.Add(variableDefinition);
            return variableDefinition;
        }

        private void FreeTemp(VariableDefinition temp) {
            // TODO Will this suffice?
            _iLProcessor.Body.Variables.Remove(temp);
        }

        private void FreeOptTemp(VariableDefinition temp) {
            if (temp is not null)
                FreeTemp(temp);
        }

        private bool HasHome(BoundExpression expression, AddressKind addressKind) {
            return Binder.HasHome(expression, addressKind, _method, _stackLocals);
        }

        #region Statements

        private void EmitStatement(BoundStatement statement) {
            switch (statement.kind) {
                case BoundKind.NopStatement:
                    EmitNopStatement((BoundNopStatement)statement);
                    break;
                case BoundKind.GotoStatement:
                    EmitGotoStatement((BoundGotoStatement)statement);
                    break;
                case BoundKind.LabelStatement:
                    EmitLabelStatement((BoundLabelStatement)statement);
                    break;
                case BoundKind.ConditionalGotoStatement:
                    EmitConditionalGotoStatement((BoundConditionalGotoStatement)statement);
                    break;
                case BoundKind.LocalDeclarationStatement:
                    EmitLocalDeclarationStatement((BoundLocalDeclarationStatement)statement);
                    break;
                case BoundKind.ReturnStatement:
                    EmitReturnStatement((BoundReturnStatement)statement);
                    break;
                case BoundKind.TryStatement:
                    EmitTryStatement((BoundTryStatement)statement);
                    break;
                case BoundKind.ExpressionStatement:
                    EmitExpression(((BoundExpressionStatement)statement).expression, false);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(statement.kind);
            }
        }

        private void EmitNopStatement(BoundNopStatement _) {
            _iLProcessor.Emit(OpCodes.Nop);
        }

        private void EmitLabelStatement(BoundLabelStatement statement) {
            MarkLabel(statement.label);
        }

        private void EmitGotoStatement(BoundGotoStatement statement) {
            // TODO Roslyn uses Br
            EmitBranch(OpCodes.Br_S, statement.label);
        }

        private void EmitBranch(OpCode opCode, LabelSymbol dest, OpCode? revCode = null) {
            // TODO What exactly is revCode used for?
            revCode ??= OpCodes.Nop;
            _unhandledGotos.Add((_count, dest));
            _iLProcessor.Emit(opCode, Instruction.Create(OpCodes.Nop));
        }

        private void MarkLabel(LabelSymbol label) {
            _labels.Add(label, _count);
        }

        private void EmitConditionalGotoStatement(BoundConditionalGotoStatement statement) {
            var label = statement.label;
            EmitConditionalBranch(statement.condition, ref label, statement.jumpIfTrue);
        }

        private void EmitConditionalBranch(BoundExpression condition, ref LabelSymbol dest, bool sense) {
oneMoreTime:

            OpCode iLCode;

            if (condition.constantValue is not null) {
                // TODO Add when default values are added
                // bool taken = condition.constantValue.IsDefaultValue != sense;
                var taken = false != sense;

                if (taken) {
                    dest ??= new SynthesizedLabelSymbol("dest");
                    EmitBranch(OpCodes.Br, dest);
                }

                return;
            }

            switch (condition.kind) {
                case BoundKind.BinaryOperator:
                    var binOp = (BoundBinaryOperator)condition;

                    if (binOp.operatorKind.OperatorWithConditional() is
                        BinaryOperatorKind.ConditionalOr or BinaryOperatorKind.ConditionalAnd) {
                        var stack = ArrayBuilder<(BoundExpression? condition, StrongBox<LabelSymbol> destBox, bool sense)>
                            .GetInstance();

                        var destBox = new StrongBox<LabelSymbol>(dest);
                        stack.Push((binOp, destBox, sense));

                        do {
                            var top = stack.Pop();

                            if (top.condition is null) {
                                var fallThrough = top.destBox.Value;

                                if (fallThrough is not null)
                                    MarkLabel(fallThrough);
                            } else if (top.condition.constantValue is null &&
                                       top.condition is BoundBinaryOperator binary &&
                                       binary.operatorKind.OperatorWithConditional()
                                        is BinaryOperatorKind.ConditionalOr or BinaryOperatorKind.ConditionalAnd) {
                                if (binary.operatorKind.OperatorWithConditional() is BinaryOperatorKind.ConditionalOr
                                    ? !top.sense : top.sense) {
                                    var fallThrough = new StrongBox<LabelSymbol>();

                                    stack.Push((null, fallThrough, true));
                                    stack.Push((binary.right, top.destBox, top.sense));
                                    stack.Push((binary.left, fallThrough, !top.sense));
                                } else {
                                    stack.Push((binary.right, top.destBox, top.sense));
                                    stack.Push((binary.left, top.destBox, top.sense));
                                }
                            } else if (stack.Count == 0 && ReferenceEquals(destBox, top.destBox)) {
                                condition = top.condition;
                                sense = top.sense;
                                dest = destBox.Value;
                                stack.Free();
                                goto oneMoreTime;
                            } else {
                                EmitConditionalBranch(top.condition, ref top.destBox.Value, top.sense);
                            }
                        } while (stack.Count != 0);

                        dest = destBox.Value;
                        stack.Free();
                        return;
                    }

                    switch (binOp.operatorKind.OperatorWithConditional()) {
                        case BinaryOperatorKind.ConditionalOr:
                        case BinaryOperatorKind.ConditionalAnd:
                            throw ExceptionUtilities.Unreachable();
                        case BinaryOperatorKind.Equal:
                        case BinaryOperatorKind.NotEqual:
                            var reduced = TryReduce(binOp, ref sense);

                            if (reduced is not null) {
                                condition = reduced;
                                goto oneMoreTime;
                            }

                            goto case BinaryOperatorKind.LessThan;
                        case BinaryOperatorKind.LessThan:
                        case BinaryOperatorKind.LessThanOrEqual:
                        case BinaryOperatorKind.GreaterThan:
                        case BinaryOperatorKind.GreaterThanOrEqual:
                            EmitExpression(binOp.left, true);
                            EmitExpression(binOp.right, true);
                            OpCode revOpCode;
                            iLCode = CodeForJump(binOp, sense, out revOpCode);
                            dest ??= new SynthesizedLabelSymbol("dest");
                            EmitBranch(iLCode, dest, revOpCode);
                            return;
                    }

                    goto default;
                case BoundKind.UnaryOperator:
                    var unOp = (BoundUnaryOperator)condition;

                    if (unOp.operatorKind == UnaryOperatorKind.BoolLogicalNegation) {
                        sense = !sense;
                        condition = unOp.operand;
                        goto oneMoreTime;
                    }

                    goto default;
                case BoundKind.IsOperator:
                    var isOp = (BoundIsOperator)condition;
                    var operand = isOp.left;
                    EmitExpression(operand, true);

                    if (!operand.type.IsVerifierReference())
                        EmitBox(operand.type);

                    _iLProcessor.Emit(OpCodes.Isinst, _module.GetType(isOp.right.type));
                    iLCode = sense ? OpCodes.Brtrue : OpCodes.Brfalse;
                    dest ??= new SynthesizedLabelSymbol("dest");
                    EmitBranch(iLCode, dest);
                    return;
                default:
                    EmitExpression(condition, true);

                    var conditionType = condition.type;

                    if (IsReferenceType(conditionType) && !conditionType.IsVerifierReference())
                        EmitBox(conditionType);

                    iLCode = sense ? OpCodes.Brtrue : OpCodes.Brfalse;
                    dest ??= new SynthesizedLabelSymbol("dest");
                    EmitBranch(iLCode, dest);
                    return;
            }
        }

        private static OpCode CodeForJump(BoundBinaryOperator op, bool sense, out OpCode revOpCode) {
            int opIdx;

            switch (op.operatorKind.Operator()) {
                case BinaryOperatorKind.Equal:
                    revOpCode = !sense ? OpCodes.Beq : OpCodes.Bne_Un;
                    return sense ? OpCodes.Beq : OpCodes.Bne_Un;
                case BinaryOperatorKind.NotEqual:
                    revOpCode = !sense ? OpCodes.Bne_Un : OpCodes.Beq;
                    return sense ? OpCodes.Bne_Un : OpCodes.Beq;
                case BinaryOperatorKind.LessThan:
                    opIdx = 0;
                    break;
                case BinaryOperatorKind.LessThanOrEqual:
                    opIdx = 1;
                    break;
                case BinaryOperatorKind.GreaterThan:
                    opIdx = 2;
                    break;
                case BinaryOperatorKind.GreaterThanOrEqual:
                    opIdx = 3;
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(op.operatorKind.Operator());
            }

            if (IsFloat(op.operatorKind))
                opIdx += 4 * IL_OP_CODE_ROW_LENGTH;

            var revOpIdx = opIdx;

            if (!sense)
                opIdx += IL_OP_CODE_ROW_LENGTH;
            else
                revOpIdx += IL_OP_CODE_ROW_LENGTH;

            revOpCode = CondJumpOpCodes[revOpIdx];
            return CondJumpOpCodes[opIdx];
        }

        private static BoundExpression TryReduce(BoundBinaryOperator condition, ref bool sense) {
            var opKind = condition.operatorKind.Operator();

            BoundExpression nonConstOp;
            var constOp = (condition.left.constantValue is not null) ? condition.left : null;

            if (constOp != null) {
                nonConstOp = condition.right;
            } else {
                constOp = (condition.right.constantValue is not null) ? condition.right : null;

                if (constOp is null)
                    return null;

                nonConstOp = condition.left;
            }

            var nonConstType = nonConstOp.type;

            if (!CanPassToBrfalse(nonConstType))
                return null;

            var isBool = nonConstType.specialType == SpecialType.Bool;
            var isZero = constOp.constantValue.isDefaultValue;

            if (!isBool && !isZero)
                return null;

            if (isZero)
                sense = !sense;

            if (opKind == BinaryOperatorKind.NotEqual)
                sense = !sense;

            return nonConstOp;
        }

        private static bool CanPassToBrfalse(TypeSymbol ts) {
            var st = ts.specialType;

            if (st == SpecialType.Decimal)
                return false;

            if (!st.IsPrimitiveType())
                return IsReferenceType(ts);

            return true;
        }

        private void EmitReturnStatement(BoundReturnStatement statement) {
            var expression = statement.expression;

            if (statement.refKind == RefKind.None) {
                EmitExpression(expression, true);
            } else {
                EmitAddress(
                    expression,
                    _method.refKind == RefKind.RefConst ? AddressKind.ReadOnlyStrict : AddressKind.Writeable
                );
            }

            if (ShouldUseIndirectReturn()) {
                if (expression is not null)
                    EmitStloc(_lazyReturnTemp);

                // TODO fill this out when Try is added
            } else {
                EmitRet(expression is null);
            }
        }

        private void EmitRet(bool isVoid) {
            // TODO Gets more complicated with blocks
            _iLProcessor.Emit(OpCodes.Ret);
        }

        private bool ShouldUseIndirectReturn() {
            // TODO return in exception handler
            return false;
        }

        private void EmitTryStatement(BoundTryStatement statement) {
            // TODO
        }

        private void EmitLocalDeclarationStatement(BoundLocalDeclarationStatement statement) {
            var declaration = statement.declaration;
            var local = declaration.dataContainer;
            var typeReference = _module.GetType(local.type);
            var variableDefinition = new VariableDefinition(typeReference);
            _locals.Add(local, variableDefinition);
            _iLProcessor.Body.Variables.Add(variableDefinition);

            // Essentially reporting the slot allocation then assigning
            // Could move this rewrite to the lowerer, but then we would need a way to keep track of slot allocation
            var assignment = BoundFactory.Assignment(
                null,
                new BoundDataContainerExpression(null, local, null, local.type),
                declaration.initializer,
                // TODO Should this just be false always:
                local.isRef,
                local.type
            );

            EmitAssignmentOperator(assignment, UseKind.Unused);
        }

        #endregion

        #region Expressions

        private void EmitExpression(BoundExpression expression, bool used) {
            if (expression is null)
                return;

            var constantValue = expression.constantValue;

            if (constantValue is not null) {
                EmitConstantExpression(expression.type, constantValue, used);
                return;
            }

            switch (expression.kind) {
                case BoundKind.ThisExpression:
                    if (used)
                        EmitThisExpression((BoundThisExpression)expression);

                    break;
                case BoundKind.BaseExpression:
                    if (used)
                        EmitBaseExpression((BoundBaseExpression)expression);

                    break;
                case BoundKind.CastExpression:
                    EmitCastExpression((BoundCastExpression)expression, used);
                    break;
                case BoundKind.DataContainerExpression:
                    EmitLocalLoad((BoundDataContainerExpression)expression, used);
                    break;
                case BoundKind.ParameterExpression:
                    if (used)
                        EmitParameterLoad((BoundParameterExpression)expression);

                    break;
                case BoundKind.FieldAccessExpression:
                    EmitFieldLoad((BoundFieldAccessExpression)expression, used);
                    break;
                case BoundKind.AssignmentOperator:
                    EmitAssignmentOperator((BoundAssignmentOperator)expression, used ? UseKind.UsedAsValue : UseKind.Unused);
                    break;
                case BoundKind.UnaryOperator:
                    EmitUnaryOperatorExpression((BoundUnaryOperator)expression, used);
                    break;
                case BoundKind.BinaryOperator:
                    EmitBinaryOperatorExpression((BoundBinaryOperator)expression, used);
                    break;
                case BoundKind.AsOperator:
                    EmitAsOperator((BoundAsOperator)expression, used);
                    break;
                case BoundKind.IsOperator:
                    EmitIsOperator((BoundIsOperator)expression, used, false);
                    break;
                case BoundKind.ConditionalOperator:
                    EmitConditionalOperator((BoundConditionalOperator)expression, used);
                    break;
                case BoundKind.NullAssertOperator:
                    EmitNullAssertOperator((BoundNullAssertOperator)expression, used);
                    break;
                case BoundKind.CallExpression:
                    EmitCallExpression((BoundCallExpression)expression, used ? UseKind.UsedAsValue : UseKind.Unused);
                    break;
                case BoundKind.ObjectCreationExpression:
                    EmitObjectCreationExpression((BoundObjectCreationExpression)expression, used);
                    break;
                case BoundKind.ArrayCreationExpression:
                    EmitArrayCreationExpression((BoundArrayCreationExpression)expression, used);
                    break;
                case BoundKind.InitializerList:
                    EmitInitializerList((BoundInitializerList)expression, used);
                    break;
                case BoundKind.ArrayAccessExpression:
                    EmitArrayElementLoad((BoundArrayAccessExpression)expression, used);
                    break;
                case BoundKind.TypeOfExpression:
                    if (used)
                        EmitTypeOfExpression((BoundTypeOfExpression)expression);

                    break;
                case BoundKind.TypeExpression:
                    EmitTypeExpression((BoundTypeExpression)expression);
                    break;
                case BoundKind.MethodGroup:
                    EmitMethodGroup((BoundMethodGroup)expression);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(expression.kind);
            }
        }

        private void EmitConstantExpression(TypeSymbol type, ConstantValue constant, bool used) {
            if (used) {
                if ((type is not null) && (type.typeKind == TypeKind.TemplateParameter) && constant.value is null)
                    EmitInitObj(type, used);
                else
                    EmitConstantValue(constant);
            }
        }

        private void EmitMethodGroup(BoundMethodGroup _) {
            // Unresolved method groups are only legal in scripts where the Evaluator returns something
            // Has no semantic meaning
            _iLProcessor.Emit(OpCodes.Nop);
        }

        private void EmitTypeExpression(BoundTypeExpression _) {
            // Isolated type expressions are only legal in scripts where the Evaluator returns something
            // Has no semantic meaning
            _iLProcessor.Emit(OpCodes.Nop);
        }

        private void EmitTypeOfExpression(BoundTypeOfExpression expression) {
            var type = expression.sourceType.type;
            _iLProcessor.Emit(OpCodes.Ldtoken, _module.GetType(type));
            _iLProcessor.Emit(OpCodes.Call, NetMethodReference.Type_GetTypeFromHandle);
        }

        private void EmitArrayElementLoad(BoundArrayAccessExpression expression, bool used) {
            EmitExpression(expression.receiver, used: true);
            EmitArrayIndex(expression.index);

            if (((ArrayTypeSymbol)expression.receiver.type).isSZArray) {
                var elementType = expression.type;

                switch (elementType.specialType) {
                    case SpecialType.Int:
                        _iLProcessor.Emit(OpCodes.Ldelem_I8);
                        break;
                    case SpecialType.Bool:
                        _iLProcessor.Emit(OpCodes.Ldelem_U1);
                        break;
                    case SpecialType.Decimal:
                        _iLProcessor.Emit(OpCodes.Ldelem_R8);
                        break;
                    default:
                        if (elementType.IsVerifierReference()) {
                            _iLProcessor.Emit(OpCodes.Ldelem_Ref);
                        } else {
                            if (used) {
                                // TODO Roslyn has Ldelem here, which we don't have?
                                _iLProcessor.Emit(OpCodes.Ldelem_Any, _module.GetType(elementType));
                            } else {
                                if (elementType.typeKind == TypeKind.TemplateParameter)
                                    _iLProcessor.Emit(OpCodes.Readonly);

                                _iLProcessor.Emit(OpCodes.Ldelema, _module.GetType(elementType));
                            }
                        }

                        break;
                }
            } else {
                // TODO Only SZ currently?
                // _builder.EmitArrayElementLoad(_module.Translate((ArrayTypeSymbol)arrayAccess.Expression.Type), arrayAccess.Expression.Syntax, _diagnostics.DiagnosticBag);
            }

            EmitPopIfUnused(used);
        }

        private void EmitInitializerList(BoundInitializerList expression, bool used) {
            throw new NotImplementedException();
        }

        private void EmitArrayCreationExpression(BoundArrayCreationExpression expression, bool used) {
            var arrayType = (ArrayTypeSymbol)expression.type;

            EmitArrayIndices(expression.sizes);

            if (arrayType.isSZArray) {
                _iLProcessor.Emit(OpCodes.Newarr, _module.GetType(arrayType.elementType));
            } else {
                // TODO Only SZ currently?
                // _builder.EmitArrayCreation(_module.Translate(arrayType), expression.Syntax, _diagnostics.DiagnosticBag);
            }

            if (expression.initializer is not null) {
                // TODO arrays
                // EmitArrayInitializers(arrayType, expression.initializer);
            }

            EmitPopIfUnused(used);
        }

        private void EmitObjectCreationExpression(BoundObjectCreationExpression expression, bool used) {
            var constructor = expression.constructor;

            if (constructor.IsDefaultValueTypeConstructor()) {
                EmitInitObj(expression.type, used);
            } else {
                if (!used && ConstructorNotSideEffecting(constructor)) {
                    foreach (var arg in expression.arguments)
                        EmitExpression(arg, used: false);

                    return;
                }

                EmitArguments(expression.arguments, constructor.parameters, expression.argumentRefKinds);

                _iLProcessor.Emit(OpCodes.Newobj, _module.GetMethod(constructor));

                EmitPopIfUnused(used);
            }
        }

        private bool ConstructorNotSideEffecting(MethodSymbol constructor) {
            var originalDef = constructor.originalDefinition;

            if (originalDef.containingType.specialType == SpecialType.Nullable)
                return true;

            return false;
        }

        private void EmitCallExpression(BoundCallExpression expression, UseKind useKind) {
            if (expression.method.RequiresInstanceReceiver())
                EmitInstanceCallExpression(expression, useKind);
            else
                EmitStaticCallExpression(expression, useKind);
        }

        private void EmitStaticCallExpression(BoundCallExpression expression, UseKind useKind) {
            var method = expression.method;
            var receiver = expression.receiver;
            var arguments = expression.arguments;

            if (method.containingType.Equals(Libraries.StandardLibrary.Random.underlyingNamedType)) {
                EmitRandomCall(method, arguments, expression.argumentRefKinds, useKind);
                return;
            }

            EmitArguments(arguments, method.parameters, expression.argumentRefKinds);

            if (method.isAbstract || method.isVirtual) {
                if (receiver is not BoundTypeExpression { type.typeKind: TypeKind.TemplateParameter })
                    throw ExceptionUtilities.Unreachable();

                _iLProcessor.Emit(OpCodes.Constrained, _module.GetType(receiver.type));
            }

            _iLProcessor.Emit(OpCodes.Call, _module.GetMethod(method));

            EmitCallCleanup(method, useKind);
        }

        private void EmitRandomCall(
            MethodSymbol method,
            ImmutableArray<BoundExpression> arguments,
            ImmutableArray<RefKind> argumentRefKinds,
            UseKind useKind) {
            EnsureGlobalsClassIsBuilt();

            switch (method.name) {
                case "RandInt": {
                        _iLProcessor.Emit(OpCodes.Ldsfld, _module._randomField);

                        var argument = Lowerer.CreateNullableGetValueCall(
                            null,
                            arguments[0],
                            arguments[0].type.StrippedType()
                        );

                        var refKind = GetArgumentRefKind(arguments, method.parameters, argumentRefKinds, 0);
                        EmitArgument(argument, refKind);

                        _iLProcessor.Emit(OpCodes.Conv_I4);
                        _iLProcessor.Emit(OpCodes.Callvirt, NetMethodReference.Random_Next_I);
                        _iLProcessor.Emit(OpCodes.Conv_I8);

                        EmitCallCleanup(method, useKind);
                    }

                    return;
                default:
                    throw ExceptionUtilities.UnexpectedValue(method.name);
            }
        }

        private void EmitArguments(
            ImmutableArray<BoundExpression> arguments,
            ImmutableArray<ParameterSymbol> parameters,
            ImmutableArray<RefKind> argRefKindsOpt) {
            for (var i = 0; i < arguments.Length; i++) {
                var argRefKind = GetArgumentRefKind(arguments, parameters, argRefKindsOpt, i);
                EmitArgument(arguments[i], argRefKind);
            }
        }

        private static RefKind GetArgumentRefKind(
            ImmutableArray<BoundExpression> arguments,
            ImmutableArray<ParameterSymbol> parameters,
            ImmutableArray<RefKind> argRefKindsOpt,
            int i) {
            RefKind argRefKind;

            if (i < parameters.Length) {
                if (!argRefKindsOpt.IsDefault && i < argRefKindsOpt.Length) {
                    argRefKind = argRefKindsOpt[i];
                } else {
                    // otherwise fallback to the refKind of the parameter
                    argRefKind = parameters[i].refKind switch {
                        // TODO:
                        // RefKind.RefReadOnlyParameter => RefKind.In, // should not happen, asserted above
                        var refKind => refKind
                    };
                }
            } else {
                argRefKind = RefKind.None;
            }

            return argRefKind;
        }

        private void EmitArgument(BoundExpression argument, RefKind refKind) {
            switch (refKind) {
                case RefKind.None:
                    EmitExpression(argument, true);
                    break;

                // TODO See TODO in GetArgumentRefKind
                // case RefKind.In:
                //     var temp = EmitAddress(argument, AddressKind.ReadOnly);
                //     AddExpressionTemp(temp);
                //     break;

                default:
                    // TODO See TODO in GetArgumentRefKind
                    // var unexpectedTemp = EmitAddress(argument, refKind == RefKindExtensions.StrictIn ? AddressKind.ReadOnlyStrict : AddressKind.Writeable);
                    var unexpectedTemp = EmitAddress(argument, AddressKind.Writeable);

                    if (unexpectedTemp is not null)
                        AddExpressionTemp(unexpectedTemp);

                    break;
            }
        }

        private void EmitInstanceCallExpression(BoundCallExpression call, UseKind useKind) {
            CallKind callKind;
            AddressKind? addressKind;
            bool box;
            VariableDefinition tempOpt;

            if (ReceiverIsInstanceCall(call, out var nested)) {
                var calls = ArrayBuilder<BoundCallExpression>.GetInstance();

                calls.Push(call);
                call = nested;

                while (ReceiverIsInstanceCall(call, out nested)) {
                    calls.Push(call);
                    call = nested;
                }

                callKind = DetermineEmitReceiverStrategy(call, out addressKind, out box);
                EmitReceiver(call, callKind, addressKind, box, out tempOpt);

                while (calls.Count != 0) {
                    var parentCall = calls.Pop();
                    var parentCallKind = DetermineEmitReceiverStrategy(parentCall, out addressKind, out box);

                    var parentCallReceiverType = call.type;
                    UseKind receiverUseKind;

                    if (addressKind is null) {
                        receiverUseKind = UseKind.UsedAsValue;
                    } else if (BoxNonVerifierReferenceReceiver(
                        parentCallReceiverType,
                        addressKind.GetValueOrDefault())) {
                        receiverUseKind = UseKind.UsedAsValue;
                        box = true;
                    } else {
                        var methodRefKind = call.method.refKind;

                        if (UseCallResultAsAddress(call, addressKind.GetValueOrDefault()))
                            receiverUseKind = UseKind.UsedAsAddress;
                        else
                            receiverUseKind = UseKind.UsedAsValue;
                    }

                    EmitArgumentsAndCallEpilogue(call, callKind, receiverUseKind);
                    FreeOptTemp(tempOpt);
                    tempOpt = null;

                    nested = call;
                    call = parentCall;
                    callKind = parentCallKind;

                    if (box) {
                        EmitBox(parentCallReceiverType);
                    } else if (addressKind is null) {
                    } else {
                        if (receiverUseKind != UseKind.UsedAsAddress)
                            tempOpt = AllocateTemp(parentCallReceiverType);

                        EmitGenericReceiverCloneIfNecessary(call, callKind, ref tempOpt);
                    }
                }

                calls.Free();
            } else {
                callKind = DetermineEmitReceiverStrategy(call, out addressKind, out box);
                EmitReceiver(call, callKind, addressKind, box, out tempOpt);
            }

            EmitArgumentsAndCallEpilogue(call, callKind, useKind);
            FreeOptTemp(tempOpt);

            return;

            CallKind DetermineEmitReceiverStrategy(
                BoundCallExpression call,
                out AddressKind? addressKind,
                out bool box) {
                var method = call.method;
                var receiver = call.receiver;
                CallKind callKind;
                var receiverType = receiver.type;
                box = false;

                if (receiverType.IsVerifierReference()) {
                    addressKind = null;

                    if (receiver.suppressVirtualCalls ||
                        (!method.IsMetadataVirtual() && CanUseCallOnRefTypeReceiver(receiver))) {
                        callKind = CallKind.Call;
                    } else {
                        callKind = CallKind.CallVirt;
                    }
                } else if (receiverType.IsVerifierValue()) {
                    var methodContainingType = method.containingType;

                    if (methodContainingType.IsVerifierValue()) {
                        addressKind = IsReadOnlyCall(method, methodContainingType)
                            ? AddressKind.ReadOnly
                            : AddressKind.Writeable;

                        if (MayUseCallForStructMethod(method))
                            callKind = CallKind.Call;
                        else
                            callKind = CallKind.ConstrainedCallVirt;
                    } else {
                        if (method.IsMetadataVirtual()) {
                            addressKind = AddressKind.Writeable;
                            callKind = CallKind.ConstrainedCallVirt;
                        } else {
                            addressKind = null;
                            box = true;
                            callKind = CallKind.Call;
                        }
                    }
                } else {
                    callKind = IsReferenceType(receiverType) &&
                        (!IsRef(receiver) ||
                        (!ReceiverIsKnownToReferToTempIfReferenceType(receiver) &&
                        !IsSafeToDereferenceReceiverRefAfterEvaluatingArguments(call.arguments)))
                            ? CallKind.CallVirt
                            : CallKind.ConstrainedCallVirt;

                    addressKind = (callKind == CallKind.ConstrainedCallVirt) ? AddressKind.Constrained : AddressKind.Writeable;
                }

                return callKind;
            }

            void EmitReceiver(
                BoundCallExpression call,
                CallKind callKind,
                AddressKind? addressKind,
                bool box,
                out VariableDefinition temp) {
                var receiver = call.receiver;
                var receiverType = receiver.type;
                temp = null;

                if (addressKind is null) {
                    EmitExpression(receiver, used: true);

                    if (box)
                        EmitBox(receiverType);
                } else {
                    temp = EmitReceiverRef(receiver, addressKind.GetValueOrDefault());
                    EmitGenericReceiverCloneIfNecessary(call, callKind, ref temp);
                }
            }

            void EmitArgumentsAndCallEpilogue(BoundCallExpression call, CallKind callKind, UseKind useKind) {
                var method = call.method;
                var receiver = call.receiver;
                var actualMethodTargetedByTheCall = method;

                if (method.isOverride && callKind != CallKind.Call) {
                    actualMethodTargetedByTheCall = method.GetConstructedLeastOverriddenMethod(
                        _method.containingType,
                        requireSameReturnType: true
                    );
                }

                if (callKind == CallKind.ConstrainedCallVirt &&
                    IsValueType(actualMethodTargetedByTheCall.containingType)) {
                    callKind = CallKind.Call;
                }

                if (callKind == CallKind.CallVirt) {
                    if (IsThisReceiver(receiver) && actualMethodTargetedByTheCall.containingType.isSealed)
                        callKind = CallKind.Call;
                }

                var arguments = call.arguments;
                EmitArguments(arguments, method.parameters, call.argumentRefKinds);

                switch (callKind) {
                    case CallKind.Call:
                        _iLProcessor.Emit(OpCodes.Call, _module.GetMethod(actualMethodTargetedByTheCall));
                        break;
                    case CallKind.CallVirt:
                        _iLProcessor.Emit(OpCodes.Callvirt, _module.GetMethod(actualMethodTargetedByTheCall));
                        break;
                    case CallKind.ConstrainedCallVirt:
                        _iLProcessor.Emit(OpCodes.Constrained, _module.GetType(receiver.type));
                        _iLProcessor.Emit(OpCodes.Callvirt, _module.GetMethod(actualMethodTargetedByTheCall));
                        break;
                }

                EmitCallCleanup(method, useKind);
            }

            void EmitGenericReceiverCloneIfNecessary(
                BoundCallExpression call,
                CallKind callKind,
                ref VariableDefinition temp) {
                var receiver = call.receiver;
                var receiverType = receiver.type;

                if (callKind == CallKind.ConstrainedCallVirt && temp is null && !IsValueType(receiverType) &&
                    !ReceiverIsKnownToReferToTempIfReferenceType(receiver) &&
                    !IsSafeToDereferenceReceiverRefAfterEvaluatingArguments(call.arguments)) {
                    LabelSymbol whenNotNullLabel = null;

                    if (!IsReferenceType(receiverType)) {
                        // TODO Is EmitDefaultValue reachable?
                        // if ((object)default(T) == null)
                        // EmitDefaultValue(receiverType, true, receiver.Syntax);
                        EmitBox(receiverType);
                        whenNotNullLabel = new SynthesizedLabelSymbol("whenNotNull");
                        EmitBranch(OpCodes.Brtrue, whenNotNullLabel);
                    }

                    EmitLoadIndirect(receiverType);
                    temp = AllocateTemp(receiverType);
                    EmitStloc(temp);
                    EmitLdloca(temp);

                    if (whenNotNullLabel is not null)
                        MarkLabel(whenNotNullLabel);
                }
            }

            static bool ReceiverIsInstanceCall(BoundCallExpression call, out BoundCallExpression nested) {
                if (call.receiver is
                    BoundCallExpression { method: { requiresInstanceReceiver: true } method } receiver) {
                    nested = receiver;
                    return true;
                }

                nested = null;
                return false;
            }
        }

        private static bool ReceiverIsKnownToReferToTempIfReferenceType(BoundExpression receiver) {
            // TODO
            // if (receiver is
            //         BoundLocal { LocalSymbol.IsKnownToReferToTempIfReferenceType: true } or
            //         BoundComplexConditionalReceiver or
            //         BoundConditionalReceiver { Type: { IsReferenceType: false, IsValueType: false } }) {
            //     return true;
            // }

            return false;
        }

        private bool IsReadOnlyCall(MethodSymbol method, NamedTypeSymbol methodContainingType) {
            if (method.isEffectivelyConst && method.methodKind != MethodKind.Constructor)
                return true;

            if (methodContainingType.IsNullableType()) {
                var originalMethod = method.originalDefinition;

                // TODO Reachable?
                // if ((object)originalMethod == this._module.Compilation.GetSpecialTypeMember(SpecialMember.System_Nullable_T_GetValueOrDefault) ||
                //     (object)originalMethod == this._module.Compilation.GetSpecialTypeMember(SpecialMember.System_Nullable_T_get_Value) ||
                //     (object)originalMethod == this._module.Compilation.GetSpecialTypeMember(SpecialMember.System_Nullable_T_get_HasValue)) {
                //     return true;
                // }
            }

            return false;
        }

        private static bool IsRef(BoundExpression receiver) {
            switch (receiver.kind) {
                case BoundKind.DataContainerExpression:
                    return ((BoundDataContainerExpression)receiver).dataContainer.refKind != RefKind.None;
                case BoundKind.ParameterExpression:
                    return ((BoundParameterExpression)receiver).parameter.refKind != RefKind.None;
                case BoundKind.CallExpression:
                    return ((BoundCallExpression)receiver).method.refKind != RefKind.None;
            }

            return false;
        }

        private static bool MayUseCallForStructMethod(MethodSymbol method) {
            if (!method.IsMetadataVirtual() || method.isStatic)
                return true;

            var overriddenMethod = method.overriddenMethod;

            if (overriddenMethod is null || overriddenMethod.isAbstract)
                return true;

            var containingType = method.containingType;
            // TODO Why are we even here? Struct's can't have methods, but just in case consider checking for this
            // Overrides in structs of some special types can be called directly.
            // We can assume that these special types will not be removing overrides.
            // This pattern can probably be applied to all special types,
            // but that would introduce a silent change every time a new special type is added,
            // so we constrain the check to a fixed range of types
            // return containingType.SpecialType.CanOptimizeBehavior();
            return true;
        }

        private static bool IsSafeToDereferenceReceiverRefAfterEvaluatingArguments(
            ImmutableArray<BoundExpression> arguments) {
            return arguments.All(IsSafeToDereferenceReceiverRefAfterEvaluatingArgument);

            static bool IsSafeToDereferenceReceiverRefAfterEvaluatingArgument(BoundExpression expression) {
                var current = expression;

                while (true) {
                    if (current.constantValue is not null)
                        return true;

                    switch (current.kind) {
                        case BoundKind.TypeExpression:
                        case BoundKind.ParameterExpression:
                        case BoundKind.DataContainerExpression:
                        case BoundKind.ThisExpression:
                            return true;
                        case BoundKind.FieldAccessExpression: {
                                var field = (BoundFieldAccessExpression)current;
                                current = field.receiver;

                                if (current is null)
                                    return true;

                                break;
                            }
                        case BoundKind.BinaryOperator: {
                                var b = (BoundBinaryOperator)current;

                                if (b.operatorKind.IsUserDefined() ||
                                    !IsSafeToDereferenceReceiverRefAfterEvaluatingArgument(b.right)) {
                                    return false;
                                }

                                current = b.left;
                                break;
                            }
                        case BoundKind.CastExpression: {
                                var conv = (BoundCastExpression)current;

                                if (conv.conversion.kind.IsUserDefinedConversion())
                                    return false;

                                current = conv.operand;
                                break;
                            }
                        default:
                            return false;
                    }
                }
            }
        }

        private bool CanUseCallOnRefTypeReceiver(BoundExpression receiver) {
            if (receiver.type.IsTemplateParameter())
                return false;

            var constVal = receiver.constantValue;

            if (constVal is not null)
                return constVal.value is not null;

            switch (receiver.kind) {
                case BoundKind.ArrayCreationExpression:
                    return true;
                case BoundKind.ObjectCreationExpression:
                    return true;
                case BoundKind.CastExpression:
                    var conversion = (BoundCastExpression)receiver;

                    switch (conversion.conversion.kind) {
                        case ConversionKind.AnyBoxing:
                            return true;
                        case ConversionKind.ExplicitReference:
                        case ConversionKind.ImplicitReference:
                            return CanUseCallOnRefTypeReceiver(conversion.operand);
                    }

                    break;
                case BoundKind.ThisExpression:
                    return true;
                case BoundKind.AssignmentOperator:
                    var rhs = ((BoundAssignmentOperator)receiver).right;
                    return CanUseCallOnRefTypeReceiver(rhs);
                case BoundKind.TypeOfExpression:
                    return true;
            }

            return false;
        }

        private bool IsThisReceiver(BoundExpression receiver) {
            return receiver.kind == BoundKind.ThisExpression;
        }

        private void EmitCallCleanup(MethodSymbol method, UseKind useKind) {
            if (!method.returnsVoid)
                EmitPopIfUnused(useKind != UseKind.Unused);

            if (useKind == UseKind.UsedAsValue && method.refKind != RefKind.None)
                EmitLoadIndirect(method.returnType);
        }

        private bool TryEmitComparison(BoundExpression condition, bool sense) {
            RemoveNegation(ref condition, ref sense);

            if (condition.constantValue is { } constantValue) {
                EmitBoolConstant(((bool)constantValue.value) == sense);
                return true;
            }

            if (condition is BoundBinaryOperator binOp) {
                if (binOp.operatorKind.IsComparison()) {
                    EmitBinaryCondOperator(binOp, sense: sense);
                    return true;
                }
            } else if (condition is BoundIsOperator isOp) {
                EmitIsOperator(isOp, used: true, omitBooleanConversion: true);

                _iLProcessor.Emit(OpCodes.Ldnull);
                _iLProcessor.Emit(sense ? OpCodes.Cgt_Un : OpCodes.Ceq);
                return true;
            } else {
                EmitExpression(condition, used: true);

                _iLProcessor.Emit(OpCodes.Ldc_I4_0);
                _iLProcessor.Emit(sense ? OpCodes.Cgt_Un : OpCodes.Ceq);
                return true;
            }

            return false;
        }

        private void EmitConditionalOperator(BoundConditionalOperator expression, bool used) {
            if (used &&
                (IsNumeric(expression.type.specialType) || expression.type.specialType == SpecialType.Bool) &&
                expression.trueExpression.constantValue?.IsIntegralValueZeroOrOne(out var isConsequenceOne) == true &&
                expression.falseExpression.constantValue?.IsIntegralValueZeroOrOne(out var isAlternativeOne) == true &&
                isConsequenceOne != isAlternativeOne &&
                TryEmitComparison(expression.condition, sense: isConsequenceOne)) {
                var toType = expression.type.specialType;

                if (toType != SpecialType.Bool)
                    EmitNumericConversion(SpecialType.Int, toType);

                return;
            }

            LabelSymbol consequenceLabel = new SynthesizedLabelSymbol("consequence");
            LabelSymbol doneLabel = new SynthesizedLabelSymbol("done");

            EmitConditionalBranch(expression.condition, ref consequenceLabel, sense: true);
            EmitExpression(expression.falseExpression, used);

            var mergeTypeOfAlternative = StackMergeType(expression.falseExpression);

            if (used) {
                if (IsVarianceCast(expression.type, mergeTypeOfAlternative)) {
                    EmitStaticCast(expression.type);
                    mergeTypeOfAlternative = expression.type;
                }
            }

            EmitBranch(OpCodes.Br, doneLabel);

            MarkLabel(consequenceLabel);
            EmitExpression(expression.trueExpression, used);

            if (used) {
                var mergeTypeOfConsequence = StackMergeType(expression.trueExpression);

                if (IsVarianceCast(expression.type, mergeTypeOfConsequence)) {
                    EmitStaticCast(expression.type);
                    mergeTypeOfConsequence = expression.type;
                }
            }

            MarkLabel(doneLabel);
        }

        private TypeSymbol StackMergeType(BoundExpression expr) {
            return expr.type;
            // TODO Need to do some extra work with interface or delegate types
        }

        private static bool IsVarianceCast(TypeSymbol to, TypeSymbol from) {
            if (TypeSymbol.Equals(to, from, TypeCompareKind.ConsiderEverything))
                return false;

            if (from is null)
                return true;

            if (to.IsArray()) {
                return IsVarianceCast(((ArrayTypeSymbol)to).elementType, ((ArrayTypeSymbol)from).elementType);
            }

            // TODO This becomes more interesting with delegate or interface types:
            return false;
        }

        private void EmitIsOperator(BoundIsOperator expression, bool used, bool omitBooleanConversion) {
            var operand = expression.left;
            EmitExpression(operand, used);

            if (used) {
                if (!operand.type.IsVerifierReference())
                    EmitBox(operand.type);

                _iLProcessor.Emit(OpCodes.Isinst, _module.GetType(expression.right.type));

                if (!omitBooleanConversion) {
                    _iLProcessor.Emit(OpCodes.Ldnull);
                    _iLProcessor.Emit(OpCodes.Cgt_Un);
                }
            }
        }

        private void EmitAsOperator(BoundAsOperator expression, bool used) {
            var operand = expression.left;
            EmitExpression(operand, used);

            if (used) {
                var operandType = operand.type;
                var targetType = expression.type;

                if (operandType is not null && !operandType.IsVerifierReference())
                    EmitBox(operandType);

                _iLProcessor.Emit(OpCodes.Isinst, _module.GetType(targetType));

                if (!targetType.IsVerifierReference())
                    _iLProcessor.Emit(OpCodes.Unbox_Any, _module.GetType(targetType));
            }
        }

        private void EmitNullAssertOperator(BoundNullAssertOperator expression, bool used) {
            if (!expression.throwIfNull) {
                EmitExpression(expression.operand, used);
                return;
            }

            EnsureGlobalsClassIsBuilt();

            EmitExpression(expression.operand, true);

            _iLProcessor.Emit(OpCodes.Call, _module.GetNullAssert(expression.type));

            EmitPopIfUnused(used);
        }

        private void EmitBinaryOperatorExpression(BoundBinaryOperator expression, bool used) {
            var operatorKind = expression.operatorKind;

            if (!used && !operatorKind.IsConditional() && !OperatorHasSideEffects(operatorKind)) {
                EmitExpression(expression.left, false);
                EmitExpression(expression.right, false);
                return;
            }

            if (IsConditional(operatorKind)) {
                EmitBinaryCondOperator(expression, true);
            } else {
                EmitBinaryOperator(expression);
            }

            EmitPopIfUnused(used);
        }

        private void EmitBinaryOperator(BoundBinaryOperator expression) {
            var child = expression.left;

            if (child.kind != BoundKind.BinaryOperator || child.constantValue is not null) {
                EmitBinaryOperatorSimple(expression);
                return;
            }

            var binary = (BoundBinaryOperator)child;
            var operatorKind = binary.operatorKind;

            if (IsConditional(operatorKind)) {
                EmitBinaryOperatorSimple(expression);
                return;
            }

            var stack = ArrayBuilder<BoundBinaryOperator>.GetInstance();
            stack.Push(expression);

            while (true) {
                stack.Push(binary);
                child = binary.left;

                if (child.kind != BoundKind.BinaryOperator || child.constantValue is not null)
                    break;

                binary = (BoundBinaryOperator)child;
                operatorKind = binary.operatorKind;

                if (IsConditional(operatorKind))
                    break;
            }

            EmitExpression(child, true);

            do {
                binary = stack.Pop();

                EmitExpression(binary.right, true);
                EmitBinaryOperatorInstruction(binary);
            } while (stack.Count > 0);

            stack.Free();
        }

        private void EmitBinaryOperatorSimple(BoundBinaryOperator expression) {
            EmitExpression(expression.left, true);
            EmitExpression(expression.right, true);
            EmitBinaryOperatorInstruction(expression);
        }

        private void EmitBinaryOperatorInstruction(BoundBinaryOperator expression) {
            switch (expression.operatorKind.Operator()) {
                case BinaryOperatorKind.Multiplication:
                    _iLProcessor.Emit(OpCodes.Mul);
                    break;
                case BinaryOperatorKind.Addition
                    when (expression.operatorKind & BinaryOperatorKind.TypeMask) == BinaryOperatorKind.String:
                    _iLProcessor.Emit(OpCodes.Call, NetMethodReference.String_Concat_SS);
                    break;
                case BinaryOperatorKind.Addition:
                    _iLProcessor.Emit(OpCodes.Add);
                    break;
                case BinaryOperatorKind.Subtraction:
                    _iLProcessor.Emit(OpCodes.Sub);
                    break;
                case BinaryOperatorKind.Division:
                    _iLProcessor.Emit(OpCodes.Div);
                    break;
                case BinaryOperatorKind.Modulo:
                    _iLProcessor.Emit(OpCodes.Rem);
                    break;
                case BinaryOperatorKind.LeftShift:
                    _iLProcessor.Emit(OpCodes.Shl);
                    break;
                case BinaryOperatorKind.RightShift:
                    _iLProcessor.Emit(OpCodes.Shr);
                    break;
                case BinaryOperatorKind.UnsignedRightShift:
                    _iLProcessor.Emit(OpCodes.Shr_Un);
                    break;
                case BinaryOperatorKind.And:
                    _iLProcessor.Emit(OpCodes.And);
                    break;
                case BinaryOperatorKind.Xor:
                    _iLProcessor.Emit(OpCodes.Xor);
                    break;
                case BinaryOperatorKind.Or:
                    _iLProcessor.Emit(OpCodes.Or);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(expression.operatorKind.Operator());
            }
        }

        private void EmitUnaryOperatorExpression(BoundUnaryOperator expression, bool used) {
            var operatorKind = expression.operatorKind;

            if (!used) {
                EmitExpression(expression.operand, used: false);
                return;
            }

            if (operatorKind == UnaryOperatorKind.BoolLogicalNegation) {
                EmitCondExpr(expression.operand, sense: false);
                return;
            }

            EmitExpression(expression.operand, used: true);

            switch (operatorKind.Operator()) {
                case UnaryOperatorKind.UnaryMinus:
                    _iLProcessor.Emit(OpCodes.Neg);
                    break;
                case UnaryOperatorKind.BitwiseComplement:
                    _iLProcessor.Emit(OpCodes.Not);
                    break;
                case UnaryOperatorKind.UnaryPlus:
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(operatorKind.Operator());
            }
        }

        private void EmitCondExpr(BoundExpression condition, bool sense) {
            RemoveNegation(ref condition, ref sense);

            var constantValue = condition.constantValue;

            if (constantValue is not null) {
                var constant = Convert.ToBoolean(constantValue.value);
                _iLProcessor.Emit(OpCodes.Ldc_I4, constant == sense ? 1 : 0);
                return;
            }

            if (condition.kind == BoundKind.BinaryOperator) {
                var binOp = (BoundBinaryOperator)condition;

                if (IsConditional(binOp.operatorKind)) {
                    EmitBinaryCondOperator(binOp, sense);
                    return;
                }
            }

            EmitExpression(condition, true);
            EmitIsSense(sense);
        }

        private void EmitBinaryCondOperator(BoundBinaryOperator binOp, bool sense) {
            var andOrSense = sense;
            int opIdx;

            switch (binOp.operatorKind.OperatorWithConditional()) {
                case BinaryOperatorKind.ConditionalOr:
                    andOrSense = !andOrSense;
                    goto case BinaryOperatorKind.ConditionalAnd;
                case BinaryOperatorKind.ConditionalAnd:
                    if (!andOrSense)
                        EmitShortCircuitingOperator(binOp, sense, sense, true);
                    else
                        EmitShortCircuitingOperator(binOp, sense, !sense, false);

                    return;
                case BinaryOperatorKind.And:
                    EmitBinaryCondOperatorHelper(OpCodes.And, binOp.left, binOp.right, sense);
                    return;
                case BinaryOperatorKind.Or:
                    EmitBinaryCondOperatorHelper(OpCodes.Or, binOp.left, binOp.right, sense);
                    return;
                case BinaryOperatorKind.Xor:
                    if (sense)
                        EmitBinaryCondOperatorHelper(OpCodes.Xor, binOp.left, binOp.right, true);
                    else
                        EmitBinaryCondOperatorHelper(OpCodes.Ceq, binOp.left, binOp.right, true);

                    return;
                case BinaryOperatorKind.NotEqual:
                    sense = !sense;
                    goto case BinaryOperatorKind.Equal;
                case BinaryOperatorKind.Equal:
                    var constant = binOp.left.constantValue;
                    var comparand = binOp.right;

                    if (constant is null) {
                        constant = comparand.constantValue;
                        comparand = binOp.left;
                    }

                    if (constant is not null) {
                        // TODO Add when we add default values
                        // if (constant.IsDefaultValue) {
                        //     if (!constant.IsFloating) {
                        //         if (comparand is BoundConversion { Type.SpecialType: SpecialType.System_Object, ConversionKind: ConversionKind.Boxing, Operand.Type: TypeParameterSymbol { AllowsRefLikeType: true } } &&
                        //             constant.IsNull) {
                        //             // Boxing is not supported for ref like type parameters, therefore the code that we usually emit 'box; ldnull; ceq/cgt'
                        //             // is not going to work. There is, however, an exception for 'box; brtrue/brfalse' sequence (https://github.com/dotnet/runtime/blob/main/docs/design/features/byreflike-generics.md#special-il-sequences).
                        //             EmitExpression(comparand, true);

                        //             object falseLabel = new object();
                        //             object endLabel = new object();
                        //             _builder.EmitBranch(sense ? ILOpCode.Brtrue_s : ILOpCode.Brfalse_s, falseLabel);
                        //             _builder.EmitBoolConstant(true);
                        //             _builder.EmitBranch(ILOpCode.Br, endLabel);

                        //             _builder.AdjustStack(-1);
                        //             _builder.MarkLabel(falseLabel);
                        //             _builder.EmitBoolConstant(false);
                        //             _builder.MarkLabel(endLabel);
                        //             return;
                        //         }

                        //         if (sense) {
                        //             EmitIsNullOrZero(comparand, constant);
                        //         } else {
                        //             //  obj != null/0   for pointers and integral numerics is emitted as cgt.un
                        //             EmitIsNotNullOrZero(comparand, constant);
                        //         }
                        //         return;
                        //     }
                        // } else
                        if (constant.specialType == SpecialType.Bool) {
                            EmitExpression(comparand, true);
                            EmitIsSense(sense);
                            return;
                        }
                    }

                    EmitBinaryCondOperatorHelper(OpCodes.Ceq, binOp.left, binOp.right, sense);
                    return;
                case BinaryOperatorKind.LessThan:
                    opIdx = 0;
                    break;
                case BinaryOperatorKind.LessThanOrEqual:
                    opIdx = 1;
                    sense = !sense;
                    break;
                case BinaryOperatorKind.GreaterThan:
                    opIdx = 2;
                    break;
                case BinaryOperatorKind.GreaterThanOrEqual:
                    opIdx = 3;
                    sense = !sense;
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(binOp.operatorKind.OperatorWithConditional());
            }

            if (IsFloat(binOp.operatorKind))
                opIdx += 8;

            EmitBinaryCondOperatorHelper(CompOpCodes[opIdx], binOp.left, binOp.right, sense);
            return;
        }

        private void EmitShortCircuitingOperator(
            BoundBinaryOperator condition,
            bool sense,
            bool stopSense,
            bool stopValue) {
            LabelSymbol lazyFallThrough = null;

            EmitConditionalBranch(condition.left, ref lazyFallThrough, stopSense);
            EmitCondExpr(condition.right, sense);

            if (lazyFallThrough is null)
                return;

            var labEnd = new SynthesizedLabelSymbol("labEnd");
            EmitBranch(OpCodes.Br, labEnd);

            MarkLabel(lazyFallThrough);
            EmitBoolConstant(stopValue);
            MarkLabel(labEnd);
        }

        private void EmitBinaryCondOperatorHelper(
            OpCode opCode,
            BoundExpression left,
            BoundExpression right,
            bool sense) {
            EmitExpression(left, true);
            EmitExpression(right, true);
            _iLProcessor.Emit(opCode);
            EmitIsSense(sense);
        }

        private static void RemoveNegation(ref BoundExpression condition, ref bool sense) {
            while (condition is BoundUnaryOperator unOp) {
                condition = unOp.operand;
                sense = !sense;
            }
        }

        private static bool IsFloat(BinaryOperatorKind opKind) {
            var type = opKind.OperandTypes();

            switch (type) {
                case BinaryOperatorKind.Decimal:
                    return true;
                default:
                    return false;
            }
        }

        private static bool OperatorHasSideEffects(BinaryOperatorKind kind) {
            switch (kind.Operator()) {
                case BinaryOperatorKind.Division:
                case BinaryOperatorKind.Modulo:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsConditional(BinaryOperatorKind opKind) {
            switch (opKind.OperatorWithConditional()) {
                case BinaryOperatorKind.ConditionalAnd:
                case BinaryOperatorKind.ConditionalOr:
                case BinaryOperatorKind.Equal:
                case BinaryOperatorKind.NotEqual:
                case BinaryOperatorKind.LessThan:
                case BinaryOperatorKind.LessThanOrEqual:
                case BinaryOperatorKind.GreaterThan:
                case BinaryOperatorKind.GreaterThanOrEqual:
                    return true;
                case BinaryOperatorKind.And:
                case BinaryOperatorKind.Or:
                case BinaryOperatorKind.Xor:
                    return opKind.OperandTypes() == BinaryOperatorKind.Bool;
            }

            return false;
        }

        private void EmitIsSense(bool sense) {
            if (!sense) {
                _iLProcessor.Emit(OpCodes.Ldc_I4_0);
                _iLProcessor.Emit(OpCodes.Ceq);
            }
        }

        private void EmitAssignmentOperator(BoundAssignmentOperator expression, UseKind useKind) {
            if (TryEmitAssignmentInPlace(expression, useKind != UseKind.Unused))
                return;

            var lhsUsesStack = EmitAssignmentPreamble(expression);
            EmitAssignmentValue(expression);
            var temp = EmitAssignmentDuplication(expression, useKind, lhsUsesStack);
            EmitStore(expression);
            EmitAssignmentPostfix(expression, temp, useKind);
        }

        private void EmitAssignmentPostfix(
            BoundAssignmentOperator assignment,
            VariableDefinition temp,
            UseKind useKind) {
            if (temp is not null) {
                if (useKind == UseKind.UsedAsAddress)
                    EmitLdloca(temp);
                else
                    EmitLdloc(temp);

                FreeTemp(temp);
            }

            if (useKind == UseKind.UsedAsValue && assignment.isRef)
                EmitLoadIndirect(assignment.type);
        }

        private void EmitAssignmentValue(BoundAssignmentOperator assignmentOperator) {
            if (!assignmentOperator.isRef) {
                EmitExpression(assignmentOperator.right, used: true);
            } else {
                var exprTempsBefore = _expressionTemps?.Count ?? 0;
                var lhs = assignmentOperator.left;

                var temp = EmitAddress(
                    assignmentOperator.right,
                    lhs.GetRefKind() is RefKind.RefConst or RefKind.RefConstParameter
                        ? AddressKind.ReadOnlyStrict
                        : AddressKind.Writeable
                );

                AddExpressionTemp(temp);

                var exprTempsAfter = _expressionTemps?.Count ?? 0;

                // TODO We aren't handling long-lived temporaries, but does the compiler create any ever?
            }
        }

        private void EmitStore(BoundAssignmentOperator assignment) {
            var expression = assignment.left;

            switch (expression.kind) {
                case BoundKind.FieldAccessExpression:
                    EmitFieldStore((BoundFieldAccessExpression)expression, assignment.isRef);
                    break;
                case BoundKind.DataContainerExpression:
                    var local = (BoundDataContainerExpression)expression;

                    if (local.dataContainer.refKind != RefKind.None && !assignment.isRef) {
                        EmitIndirectStore(local.dataContainer.type);
                    } else {
                        if (IsStackLocal(local.dataContainer))
                            break;
                        else
                            EmitStloc(GetLocal(local.dataContainer));
                    }

                    break;
                case BoundKind.ArrayAccessExpression:
                    var array = ((BoundArrayAccessExpression)expression).receiver;
                    var arrayType = (ArrayTypeSymbol)array.type;
                    EmitArrayElementStore(arrayType);
                    break;
                case BoundKind.ThisExpression:
                    EmitThisStore((BoundThisExpression)expression);
                    break;
                case BoundKind.ParameterExpression:
                    EmitParameterStore((BoundParameterExpression)expression, assignment.isRef);
                    break;
                case BoundKind.ConditionalOperator:
                    EmitIndirectStore(expression.type);
                    break;
                case BoundKind.CallExpression:
                    EmitIndirectStore(expression.type);
                    break;
                case BoundKind.AssignmentOperator:
                    var nested = (BoundAssignmentOperator)expression;

                    if (!nested.isRef)
                        goto default;

                    EmitIndirectStore(nested.type);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(expression.kind);
            }
        }

        private void EmitThisStore(BoundThisExpression thisRef) {
            _iLProcessor.Emit(OpCodes.Stobj, _module.GetType(thisRef.type));
        }

        private void EmitArrayElementStore(ArrayTypeSymbol arrayType) {
            if (arrayType.isSZArray) {
                EmitVectorElementStore(arrayType);
            } else {
                // TODO Only SZ?
                // _builder.EmitArrayElementStore(_module.Translate(arrayType), syntaxNode, _diagnostics.DiagnosticBag);
            }
        }

        private void EmitVectorElementStore(ArrayTypeSymbol arrayType) {
            var elementType = arrayType.elementType;

            switch (elementType.specialType) {
                case SpecialType.Bool:
                    _iLProcessor.Emit(OpCodes.Stelem_I1);
                    break;
                case SpecialType.Int:
                    _iLProcessor.Emit(OpCodes.Stelem_I8);
                    break;
                case SpecialType.Decimal:
                    _iLProcessor.Emit(OpCodes.Stelem_R8);
                    break;

                default:
                    if (elementType.IsVerifierReference()) {
                        _iLProcessor.Emit(OpCodes.Stelem_Ref);
                    } else {
                        // TODO Roslyn uses Stelem
                        _iLProcessor.Emit(OpCodes.Stelem_Any, _module.GetType(elementType));
                    }

                    break;
            }
        }

        private void EmitFieldStore(BoundFieldAccessExpression fieldAccess, bool refAssign) {
            var field = fieldAccess.field;

            if (field.refKind != RefKind.None && !refAssign)
                EmitIndirectStore(field.type);
            else
                _iLProcessor.Emit(field.isStatic ? OpCodes.Stsfld : OpCodes.Stfld, _module.GetField(field));
        }

        private void EmitParameterStore(BoundParameterExpression parameter, bool refAssign) {
            if (parameter.parameter.refKind != RefKind.None && !refAssign)
                EmitIndirectStore(parameter.parameter.type);
            else
                _iLProcessor.Emit(OpCodes.Starg, GetParameter(parameter.parameter));
        }

        private void EmitIndirectStore(TypeSymbol type) {
            switch (type.specialType) {
                case SpecialType.Bool:
                    _iLProcessor.Emit(OpCodes.Stind_I1);
                    break;
                case SpecialType.Int:
                    _iLProcessor.Emit(OpCodes.Stind_I8);
                    break;
                case SpecialType.Decimal:
                    _iLProcessor.Emit(OpCodes.Stind_R8);
                    break;
                default:
                    if (type.IsVerifierReference())
                        _iLProcessor.Emit(OpCodes.Stind_Ref);
                    else
                        _iLProcessor.Emit(OpCodes.Stobj, _module.GetType(type));

                    break;
            }
        }

        private VariableDefinition EmitAssignmentDuplication(
            BoundAssignmentOperator assignmentOperator,
            UseKind useKind,
            bool lhsUsesStack) {
            VariableDefinition temp = null;

            if (useKind != UseKind.Unused) {
                _iLProcessor.Emit(OpCodes.Dup);

                if (lhsUsesStack) {
                    temp = AllocateTemp(assignmentOperator.left.type);
                    EmitStloc(temp);
                }
            }

            return temp;
        }

        private bool EmitAssignmentPreamble(BoundAssignmentOperator assignmentOperator) {
            var assignmentTarget = assignmentOperator.left;
            var lhsUsesStack = false;

            switch (assignmentTarget.kind) {
                case BoundKind.FieldAccessExpression: {
                        var left = (BoundFieldAccessExpression)assignmentTarget;

                        if (left.field.refKind != RefKind.None && !assignmentOperator.isRef) {
                            EmitFieldLoadNoIndirection(left, used: true);
                            lhsUsesStack = true;
                        } else if (!left.field.isStatic) {
                            var temp = EmitReceiverRef(left.receiver, AddressKind.Writeable);
                            lhsUsesStack = true;
                        }
                    }

                    break;
                case BoundKind.ParameterExpression: {
                        var left = (BoundParameterExpression)assignmentTarget;

                        if (left.parameter.refKind != RefKind.None && !assignmentOperator.isRef) {
                            EmitLdarg(GetParameter(left.parameter));
                            lhsUsesStack = true;
                        }
                    }

                    break;
                case BoundKind.DataContainerExpression: {
                        var left = (BoundDataContainerExpression)assignmentTarget;

                        if (left.dataContainer.refKind != RefKind.None && !assignmentOperator.isRef) {
                            if (!IsStackLocal(left.dataContainer)) {
                                var localDefinition = GetLocal(left.dataContainer);
                                EmitLdloc(localDefinition);
                            }

                            lhsUsesStack = true;
                        }
                    }

                    break;
                case BoundKind.ArrayAccessExpression: {
                        var left = (BoundArrayAccessExpression)assignmentTarget;
                        EmitExpression(left.receiver, used: true);
                        EmitArrayIndex(left.index);
                        lhsUsesStack = true;
                    }

                    break;
                case BoundKind.ThisExpression: {
                        var left = (BoundThisExpression)assignmentTarget;

                        var temp = EmitAddress(left, AddressKind.Writeable);
                        lhsUsesStack = true;
                    }

                    break;
                case BoundKind.ConditionalOperator: {
                        var left = (BoundConditionalOperator)assignmentTarget;
                        var temp = EmitAddress(left, AddressKind.Writeable);
                        lhsUsesStack = true;
                    }

                    break;
                case BoundKind.CallExpression: {
                        var left = (BoundCallExpression)assignmentTarget;
                        EmitCallExpression(left, UseKind.UsedAsAddress);
                        lhsUsesStack = true;
                    }

                    break;
                case BoundKind.AssignmentOperator:
                    var assignment = (BoundAssignmentOperator)assignmentTarget;

                    if (!assignment.isRef)
                        goto default;

                    EmitAssignmentOperator(assignment, UseKind.UsedAsAddress);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(assignmentTarget.kind);
            }

            return lhsUsesStack;
        }

        private bool TryEmitAssignmentInPlace(BoundAssignmentOperator assignmentOperator, bool used) {
            if (assignmentOperator.isRef)
                return false;

            var left = assignmentOperator.left;

            if (used && !TargetIsNotOnHeap(left)) {
                return false;
            }

            if (!SafeToGetWriteableReference(left))
                return false;

            var right = assignmentOperator.right;
            var rightType = right.type;

            if (!rightType.IsTemplateParameter()) {
                if (IsReferenceType(rightType) || (right.constantValue is not null))
                    return false;
            }

            if (right is BoundObjectCreationExpression objCreation) {
                if (PartialCtorResultCannotEscape(left)) {
                    var ctor = objCreation.constructor;

                    if (System.Linq.ImmutableArrayExtensions.All(ctor.parameters, p => p.refKind == RefKind.None) &&
                        TryInPlaceCtorCall(left, objCreation, used)) {
                        return true;
                    }
                }
            }

            return false;
        }

        private bool PartialCtorResultCannotEscape(BoundExpression left) {
            if (TargetIsNotOnHeap(left)) {
                if (_tryNestingLevel != 0)
                    return false;

                return true;
            }

            return false;
        }

        private bool TryInPlaceCtorCall(BoundExpression target, BoundObjectCreationExpression objCreation, bool used) {
            var temp = EmitAddress(target, AddressKind.Writeable);

            var constructor = objCreation.constructor;
            EmitArguments(objCreation.arguments, constructor.parameters, objCreation.argumentRefKinds);

            _iLProcessor.Emit(OpCodes.Call, _module.GetMethod(constructor));

            if (used)
                EmitExpression(target, used: true);

            return true;
        }

        private bool SafeToGetWriteableReference(BoundExpression left) {
            if (!HasHome(left, AddressKind.Writeable))
                return false;

            if (left.kind == BoundKind.ArrayAccessExpression &&
                left.type.typeKind == TypeKind.TemplateParameter &&
                !IsValueType(left.type)) {
                return false;
            }

            return true;
        }

        private static bool TargetIsNotOnHeap(BoundExpression left) {
            switch (left.kind) {
                case BoundKind.ParameterExpression:
                    return ((BoundParameterExpression)left).parameter.refKind == RefKind.None;
                case BoundKind.DataContainerExpression:
                    return ((BoundDataContainerExpression)left).dataContainer.refKind == RefKind.None;
            }

            return false;
        }

        private void EmitFieldLoad(BoundFieldAccessExpression expression, bool used) {
            var field = expression.field;

            if (!used) {
                if (!field.isStatic && expression.receiver.type.IsVerifierValue() && field.refKind == RefKind.None) {
                    EmitExpression(expression.receiver, used: false);
                    return;
                }
            }

            EmitFieldLoadNoIndirection(expression, used);

            if (field.refKind != RefKind.None)
                EmitLoadIndirect(field.type);

            EmitPopIfUnused(used);
        }

        private void EmitFieldLoadNoIndirection(BoundFieldAccessExpression fieldAccess, bool used) {
            var field = fieldAccess.field;

            if (field.isStatic) {
                _iLProcessor.Emit(OpCodes.Ldsfld, _module.GetField(field));
            } else {
                var receiver = fieldAccess.receiver;
                var fieldType = field.type;

                if (IsValueType(fieldType) && (object)fieldType == (object)receiver.type) {
                    EmitExpression(receiver, used);
                } else {
                    var temp = EmitFieldLoadReceiver(receiver);

                    if (temp is not null)
                        FreeTemp(temp);

                    _iLProcessor.Emit(OpCodes.Ldfld, _module.GetField(field));
                }
            }
        }

        private VariableDefinition EmitFieldLoadReceiver(BoundExpression receiver) {
            if (FieldLoadMustUseRef(receiver) || FieldLoadPrefersRef(receiver)) {
                return EmitFieldLoadReceiverAddress(receiver) ? null : EmitReceiverRef(receiver, AddressKind.ReadOnly);
            }

            EmitExpression(receiver, true);
            return null;
        }

        private bool FieldLoadPrefersRef(BoundExpression receiver) {
            if (!receiver.type.IsVerifierValue())
                return true;

            if (receiver.kind == BoundKind.CastExpression &&
                ((BoundCastExpression)receiver).conversion.kind == ConversionKind.AnyUnboxing) {
                return true;
            }

            if (!HasHome(receiver, AddressKind.ReadOnly))
                return false;

            switch (receiver.kind) {
                case BoundKind.ParameterExpression:
                    return ((BoundParameterExpression)receiver).parameter.refKind != RefKind.None;
                case BoundKind.DataContainerExpression:
                    return ((BoundDataContainerExpression)receiver).dataContainer.refKind != RefKind.None;
                case BoundKind.FieldAccessExpression:
                    var fieldAccess = (BoundFieldAccessExpression)receiver;
                    var field = fieldAccess.field;

                    if (field.isStatic || field.refKind != RefKind.None)
                        return true;

                    return FieldLoadPrefersRef(fieldAccess.receiver);
            }

            return true;
        }

        private bool EmitFieldLoadReceiverAddress(BoundExpression receiver) {
            if (receiver is null || !IsValueType(receiver.type)) {
                return false;
            } else if (receiver.kind == BoundKind.CastExpression) {
                var conversion = (BoundCastExpression)receiver;
                if (conversion.conversion.kind == ConversionKind.AnyUnboxing) {
                    EmitExpression(conversion.operand, true);
                    _iLProcessor.Emit(OpCodes.Unbox, _module.GetType(receiver.type));
                    return true;
                }
            } else if (receiver.kind == BoundKind.FieldAccessExpression) {
                var fieldAccess = (BoundFieldAccessExpression)receiver;
                var field = fieldAccess.field;

                if (!field.isStatic && EmitFieldLoadReceiverAddress(fieldAccess.receiver)) {
                    _iLProcessor.Emit(OpCodes.Ldflda, _module.GetField(field));
                    return true;
                }
            }

            return false;
        }

        private static bool FieldLoadMustUseRef(BoundExpression expr) {
            var type = expr.type;

            if (type.IsTemplateParameter())
                return true;

            switch (type.specialType) {
                case SpecialType.Int:
                case SpecialType.Decimal:
                case SpecialType.Bool:
                    return true;
            }

            return false;
        }

        private void EmitParameterLoad(BoundParameterExpression expression) {
            var parameter = expression.parameter;
            EmitLdarg(GetParameter(parameter));

            if (parameter.refKind != RefKind.None) {
                var parameterType = parameter.type;
                EmitLoadIndirect(parameterType);
            }
        }

        private void EmitLocalLoad(BoundDataContainerExpression expression, bool used) {
            var local = expression.dataContainer;
            var isRefLocal = local.refKind != RefKind.None;

            if (IsStackLocal(local)) {
                EmitPopIfUnused(used || isRefLocal);
            } else {
                if (used || isRefLocal) {
                    var definition = GetLocal(local);
                    _iLProcessor.Emit(OpCodes.Ldloc, definition);
                } else {
                    return;
                }
            }

            if (isRefLocal) {
                EmitLoadIndirect(local.type);
                EmitPopIfUnused(used);
            }
        }

        private void EmitCastExpression(BoundCastExpression expression, bool used) {
            var operand = expression.operand;

            if (!used && !expression.ConversionHasSideEffects()) {
                EmitExpression(operand, false);
                return;
            }

            EmitExpression(operand, true);
            EmitCast(expression);

            EmitPopIfUnused(used);
        }

        private void EmitCast(BoundCastExpression cast) {
            switch (cast.conversion.kind) {
                case ConversionKind.Identity:
                    break;
                case ConversionKind.ImplicitReference:
                case ConversionKind.AnyBoxing:
                    EmitImplicitReferenceConversion(cast);
                    break;
                case ConversionKind.ExplicitReference:
                case ConversionKind.AnyUnboxing:
                    EmitExplicitReferenceConversion(cast);
                    break;
                case ConversionKind.Implicit:
                case ConversionKind.Explicit:
                    EmitConvertCallOrNumericConversion(cast);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(cast.conversion.kind);
            }
        }

        private void EmitConvertCallOrNumericConversion(BoundCastExpression cast) {
            var fromType = cast.operand.type;
            var fromPredefTypeKind = fromType.specialType;

            var toType = cast.type;
            var toPredefTypeKind = toType.specialType;

            if (IsNumeric(fromPredefTypeKind) && IsNumeric(toPredefTypeKind))
                EmitNumericConversion(fromPredefTypeKind, toPredefTypeKind);

            EmitConvertCall(fromPredefTypeKind, toPredefTypeKind);
        }

        private void EmitConvertCall(SpecialType from, SpecialType to) {
            switch (from, to) {
                case (SpecialType.String, SpecialType.Bool):
                    _iLProcessor.Emit(OpCodes.Call, NetMethodReference.Convert_ToBoolean_S);
                    break;
                case (SpecialType.String, SpecialType.Int):
                    _iLProcessor.Emit(OpCodes.Call, NetMethodReference.Convert_ToInt64_S);
                    break;
                case (SpecialType.Decimal, SpecialType.Int):
                    _iLProcessor.Emit(OpCodes.Call, NetMethodReference.Convert_ToInt64_D);
                    break;
                case (SpecialType.String, SpecialType.Decimal):
                    _iLProcessor.Emit(OpCodes.Call, NetMethodReference.Convert_ToDouble_S);
                    break;
                case (SpecialType.Int, SpecialType.Decimal):
                    _iLProcessor.Emit(OpCodes.Call, NetMethodReference.Convert_ToDouble_I);
                    break;
                case (SpecialType.Int, SpecialType.String):
                    _iLProcessor.Emit(OpCodes.Call, NetMethodReference.Convert_ToString_I);
                    break;
                case (SpecialType.Decimal, SpecialType.String):
                    _iLProcessor.Emit(OpCodes.Call, NetMethodReference.Convert_ToString_D);
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue((from, to));
            }
        }

        private void EmitNumericConversion(SpecialType from, SpecialType to) {
            if (to == SpecialType.Int) {
                if (from == SpecialType.Decimal)
                    _iLProcessor.Emit(OpCodes.Conv_I8);
            }

            if (to == SpecialType.Decimal) {
                if (from == SpecialType.Int)
                    _iLProcessor.Emit(OpCodes.Conv_R8);
            }
        }

        private static bool IsNumeric(SpecialType specialType) {
            return specialType is SpecialType.Int or SpecialType.Decimal;
        }

        private void EmitImplicitReferenceConversion(BoundCastExpression conversion) {
            if (!conversion.operand.type.IsVerifierReference())
                EmitBox(conversion.operand.type);

            var resultType = conversion.type;

            if (!resultType.IsVerifierReference())
                _iLProcessor.Emit(OpCodes.Unbox_Any, _module.GetType(conversion.type));
            else if (resultType.IsArray())
                EmitStaticCast(conversion.type);

            return;
        }

        private void EmitExplicitReferenceConversion(BoundCastExpression conversion) {
            if (!conversion.operand.type.IsVerifierReference())
                EmitBox(conversion.operand.type);

            if (conversion.type.IsVerifierReference())
                _iLProcessor.Emit(OpCodes.Castclass, _module.GetType(conversion.type));
            else
                _iLProcessor.Emit(OpCodes.Unbox_Any, _module.GetType(conversion.type));
        }

        private void EmitStaticCast(TypeSymbol to) {
            var temp = AllocateTemp(to);
            _iLProcessor.Emit(OpCodes.Stloc, temp);
            _iLProcessor.Emit(OpCodes.Ldloc, temp);
            FreeTemp(temp);
        }

        private void EmitBaseExpression(BoundBaseExpression expression) {
            EmitLdarg0();
            var thisType = _method.containingType;

            if (IsValueType(thisType)) {
                EmitLoadIndirect(thisType);
                EmitBox(thisType);
            }
        }

        private void EmitThisExpression(BoundThisExpression expression) {
            EmitLdarg0();
            var thisType = expression.type;

            if (IsValueType(thisType))
                EmitLoadIndirect(thisType);
        }

        private void EmitLoadIndirect(TypeSymbol type) {
            switch (type.specialType) {
                case SpecialType.Int:
                    _iLProcessor.Emit(OpCodes.Ldind_I8);
                    break;
                case SpecialType.Bool:
                    _iLProcessor.Emit(OpCodes.Ldind_I1);
                    break;
                case SpecialType.Decimal:
                    _iLProcessor.Emit(OpCodes.Ldind_R8);
                    break;
                default:
                    if (type.IsVerifierReference())
                        _iLProcessor.Emit(OpCodes.Ldind_Ref);
                    else
                        _iLProcessor.Emit(OpCodes.Ldobj, _module.GetType(type));

                    break;
            }
        }

        private void EmitBox(TypeSymbol type) {
            _iLProcessor.Emit(OpCodes.Box, _module.GetType(type));
        }

        private void EmitPopIfUnused(bool used) {
            if (!used)
                _iLProcessor.Emit(OpCodes.Pop);
        }

        #endregion
    }
}
