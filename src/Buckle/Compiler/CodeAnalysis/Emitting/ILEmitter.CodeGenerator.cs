using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Mono.Cecil;
using Mono.Cecil.Cil;
using static Buckle.CodeAnalysis.Binding.Binder;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    internal sealed partial class CodeGenerator {
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
            return type.isObjectType && type.specialType != SpecialType.Nullable;
        }

        internal static bool IsValueType(TypeSymbol type) {
            return type.isPrimitiveType || type.specialType == SpecialType.Nullable;
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
            _labels.Add(statement.label, _count);
        }

        private void EmitGotoStatement(BoundGotoStatement statement) {
            _unhandledGotos.Add((_count, statement.label));
            _iLProcessor.Emit(OpCodes.Br_S, Instruction.Create(OpCodes.Nop));
        }

        private void EmitConditionalGotoStatement(BoundConditionalGotoStatement statement) {
            // TODO Potential for optimization by manually handling operators
            EmitExpression(statement.condition, true);
            _unhandledGotos.Add((_count, statement.label));
            _iLProcessor.Emit(statement.jumpIfTrue ? OpCodes.Brtrue : OpCodes.Brfalse, Instruction.Create(OpCodes.Nop));
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
                    EmitUnaryOperator((BoundUnaryOperator)expression, used);
                    break;
                case BoundKind.BinaryOperator:
                    EmitBinaryOperator((BoundBinaryOperator)expression, used);
                    break;
                case BoundKind.NullAssertOperator:
                    EmitNullAssertOperator((BoundNullAssertOperator)expression, used);
                    break;
                case BoundKind.AsOperator:
                    EmitAsOperator((BoundAsOperator)expression, used);
                    break;
                case BoundKind.IsOperator:
                    EmitIsOperator((BoundIsOperator)expression, used);
                    break;
                case BoundKind.ConditionalOperator:
                    EmitConditionalOperator((BoundConditionalOperator)expression, used);
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

            EmitArguments(arguments, method.parameters, expression.argumentRefKinds);

            if (method.isAbstract || method.isVirtual) {
                if (receiver is not BoundTypeExpression { type.typeKind: TypeKind.TemplateParameter })
                    throw ExceptionUtilities.Unreachable();

                _iLProcessor.Emit(OpCodes.Constrained, _module.GetType(receiver.type));
            }

            _iLProcessor.Emit(OpCodes.Call, _module.GetMethod(method));

            EmitCallCleanup(method, useKind);
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
                    var whenNotNullLabel = new SynthesizedLabelSymbol("whenNotNull");

                    if (!IsReferenceType(receiverType)) {
                        // TODO Is EmitDefaultValue reachable?
                        // if ((object)default(T) == null)
                        // EmitDefaultValue(receiverType, true, receiver.Syntax);
                        EmitBox(receiverType);
                        _unhandledGotos.Add((_count, whenNotNullLabel));
                        _iLProcessor.Emit(OpCodes.Brtrue, Instruction.Create(OpCodes.Nop));
                    }

                    EmitLoadIndirect(receiverType);
                    temp = AllocateTemp(receiverType);
                    EmitStloc(temp);
                    EmitLdloca(temp);

                    _labels.Add(whenNotNullLabel, _count);
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

        private void EmitConditionalOperator(BoundConditionalOperator expression, bool used) {
            throw new NotImplementedException();
        }

        private void EmitIsOperator(BoundIsOperator expression, bool used) {
            throw new NotImplementedException();
        }

        private void EmitAsOperator(BoundAsOperator expression, bool used) {
            throw new NotImplementedException();
        }

        private void EmitNullAssertOperator(BoundNullAssertOperator expression, bool used) {
            throw new NotImplementedException();
        }

        private void EmitBinaryOperator(BoundBinaryOperator expression, bool used) {
            throw new NotImplementedException();
        }

        private void EmitUnaryOperator(BoundUnaryOperator expression, bool used) {
            throw new NotImplementedException();
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
                    EmitNumericConversion(cast);
                    break;
                case ConversionKind.ImplicitNullable:
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(cast.conversion.kind);
            }
        }

        private void EmitNumericConversion(BoundCastExpression cast) {
            var fromType = cast.operand.type;
            var fromPredefTypeKind = fromType.specialType;

            var toType = cast.type;
            var toPredefTypeKind = toType.specialType;

            if (!IsNumeric(fromPredefTypeKind) || !IsNumeric(toPredefTypeKind))
                throw ExceptionUtilities.UnexpectedValue(cast.kind);

            EmitNumericConversion(fromPredefTypeKind, toPredefTypeKind);
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
            var consequenceLabel = new SynthesizedLabelSymbol("consequence");
            var doneLabel = new SynthesizedLabelSymbol("done");

            EmitExpression(expression.condition, true);
            _unhandledGotos.Add((_count, consequenceLabel));
            _iLProcessor.Emit(OpCodes.Brtrue, Instruction.Create(OpCodes.Nop));

            AddExpressionTemp(EmitAddress(expression.falseExpression, addressKind));

            _unhandledGotos.Add((_count, doneLabel));
            _iLProcessor.Emit(OpCodes.Br_S, Instruction.Create(OpCodes.Nop));

            _labels.Add(consequenceLabel, _count);

            AddExpressionTemp(EmitAddress(expression.trueExpression, addressKind));

            _labels.Add(doneLabel, _count);

            // TODO Double check the following was correctly recreated above
            // EmitCondBranch(expression.condition, ref consequenceLabel, sense: true);
            // AddExpressionTemp(EmitAddress(expression.alternative, addressKind));

            // _builder.EmitBranch(ILOpCode.Br, doneLabel);

            // // If we get to consequenceLabel, we should not have Alternative on stack, adjust for that.
            // _builder.AdjustStack(-1);

            // _builder.MarkLabel(consequenceLabel);
            // AddExpressionTemp(EmitAddress(expression.Consequence, addressKind));

            // _builder.MarkLabel(doneLabel);
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
            if (constant.value is null)
                _iLProcessor.Emit(OpCodes.Ldnull);

            switch (constant.specialType) {
                case SpecialType.Int:
                    _iLProcessor.Emit(OpCodes.Ldc_I8, (long)constant.value);
                    break;
                case SpecialType.Bool:
                    _iLProcessor.Emit(OpCodes.Ldc_I4, (bool)constant.value ? 1 : 0);
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

        private bool HasHome(BoundExpression expression, AddressKind addressKind)
            => Binder.HasHome(expression, addressKind, _method, _stackLocals);

    }
}
