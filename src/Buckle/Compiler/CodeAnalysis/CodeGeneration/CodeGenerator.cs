using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using static Buckle.CodeAnalysis.Binding.Binder;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class CodeGenerator {
    private static readonly OpCode[] CompOpCodes = [
        //  <            <=               >                >=
        OpCode.Clt,    OpCode.Cgt,    OpCode.Cgt,    OpCode.Clt,     // Signed
        OpCode.Clt_Un, OpCode.Cgt_Un, OpCode.Cgt_Un, OpCode.Clt_Un,  // Unsigned
        OpCode.Clt,    OpCode.Cgt_Un, OpCode.Cgt,    OpCode.Clt_Un,  // Float
    ];

    private const int IL_OP_CODE_ROW_LENGTH = 4;

    private static readonly OpCode[] CondJumpOpCodes = [
        //  <            <=               >                >=
        OpCode.Blt,    OpCode.Ble,    OpCode.Bgt,    OpCode.Bge,     // Signed
        OpCode.Bge,    OpCode.Bgt,    OpCode.Ble,    OpCode.Blt,     // Signed Invert
        OpCode.Blt_Un, OpCode.Ble_Un, OpCode.Bgt_Un, OpCode.Bge_Un,  // Unsigned
        OpCode.Bge_Un, OpCode.Bgt_Un, OpCode.Ble_Un, OpCode.Blt_Un,  // Unsigned Invert
        OpCode.Blt,    OpCode.Ble,    OpCode.Bgt,    OpCode.Bge,     // Float
        OpCode.Bge_Un, OpCode.Bgt_Un, OpCode.Ble_Un, OpCode.Blt_Un,  // Float Invert
    ];

    private readonly ModuleBuilder _module;
    private readonly MethodSymbol _method;
    private readonly BoundBlockStatement _body;
    private readonly ILBuilder _builder;
    private readonly HashSet<DataContainerSymbol> _stackLocals = [];
    private readonly HashSet<DataContainerSymbol> _evaluatorProxies = [];
    private readonly List<(int instructionIndex, LabelSymbol target)> _unhandledGotos = [];
    private readonly SyntaxNode _methodBodySyntax;
    private readonly ILEmitStyle _ilEmitStyle;
    private readonly bool _emitPdbSequencePoints;

    private ArrayBuilder<VariableDefinition> _expressionTemps;

    internal CodeGenerator(
        ModuleBuilder module,
        MethodSymbol method,
        BoundBlockStatement methodBody,
        ILBuilder iLBuilder,
        bool debugMode) {
        _module = module;
        _method = method;
        _body = methodBody;
        _builder = iLBuilder;
        _ilEmitStyle = debugMode ? ILEmitStyle.Debug : ILEmitStyle.Release;
        _emitPdbSequencePoints = debugMode;

        var sourceMethod = method as SourceMemberMethodSymbol;
        _methodBodySyntax = sourceMethod?.body ?? sourceMethod?.syntaxNode;
    }

    internal void Generate() {
        if (_emitPdbSequencePoints && _method.isImplicitlyDeclared)
            _builder.DefineInitialHiddenSequencePoint();

        EmitBlock(_body);
        _builder.Finish();
        _expressionTemps?.Free();
    }

    internal static bool IsReferenceType(SpecialType type) {
        return type == SpecialType.String ||
               type == SpecialType.Array ||
               type == SpecialType.Any ||
               type == SpecialType.Type;
    }

    internal static bool IsValueType(SpecialType type) {
        return type != SpecialType.String &&
               type != SpecialType.Array &&
               type != SpecialType.Any &&
               type != SpecialType.Type;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsReferenceType(TypeSymbol type) {
        var isReferenceType = (type.isObjectType && !type.IsStructType() && !type.IsEnumType() &&
                              !IsTrueNullable(type)) || IsReferenceType(type.specialType);

        if (type.StrippedType() is TemplateParameterSymbol t)
            isReferenceType &= t.hasObjectTypeConstraint;

        return isReferenceType;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsValueType(TypeSymbol type) {
        var isValueType = (type.isPrimitiveType || type.IsStructType() || type.IsEnumType() ||
                           IsTrueNullable(type)) && IsValueType(type.specialType);

        // TODO Double check there is no edge case where a primitive constraint can result in a reference type
        if (type.StrippedType() is TemplateParameterSymbol t)
            isValueType &= t.hasPrimitiveTypeConstraint;

        return isValueType;
    }

    private VariableDefinition AllocateTemp(
        TypeSymbol type,
        LocalSlotConstraints slotConstraints = LocalSlotConstraints.None) {
        return _builder.AllocateSlot(type, slotConstraints);
    }

    private static bool IsTrueNullable(TypeSymbol type) {
        if (type.specialType != SpecialType.Nullable)
            return false;

        // This happens when looking at the containing type of methods on Nullable, in which case yes it is nullable
        if (((NamedTypeSymbol)type).templateArguments.Length == 0)
            return true;

        var underlyingType = type.GetNullableUnderlyingType();
        return IsValueType(underlyingType);
    }

    internal static bool IsStackLocal(DataContainerSymbol local, HashSet<DataContainerSymbol> stackLocals) {
        return stackLocals?.Contains(local) ?? false;
    }

    private bool IsStackLocal(DataContainerSymbol local) {
        return IsStackLocal(local, _stackLocals);
    }

    private int ParameterSlot(ParameterSymbol parameter) {
        var slot = parameter.ordinal;

        if (!_method.isStatic)
            slot++;

        return slot;
    }

    private void EnsureGlobalsClassIsBuilt() {
        if (!_module.hasGeneratedGlobalsClass)
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

                    _builder.EmitLoadArgument(0);
                } else {
                    _builder.EmitLoadArgumentAddr(0);
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
            case BoundKind.FunctionPointerCallExpression:
                var funcPtrInvocation = (BoundFunctionPointerCallExpression)expression;
                var funcPtrRefKind = funcPtrInvocation.functionPointer.signature.refKind;

                if (funcPtrRefKind == RefKind.Ref ||
                    (IsAnyReadOnly(addressKind) && funcPtrRefKind == RefKind.RefConst)) {
                    EmitCalli(funcPtrInvocation, UseKind.UsedAsAddress);
                    break;
                }

                goto default;
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
            case BoundKind.PointerIndirectionOperator:
                var operand = ((BoundPointerIndirectionOperator)expression).operand;
                EmitExpression(operand, used: true);
                break;
            default:
                return EmitAddressOfTempClone(expression);
        }

        return null;
    }

    private void EmitCalli(BoundFunctionPointerCallExpression ptrInvocation, UseKind useKind) {
        EmitExpression(ptrInvocation.invokedExpression, used: true);
        VariableDefinition temp = null;

        if (ptrInvocation.arguments.Length > 0) {
            temp = AllocateTemp(ptrInvocation.invokedExpression.type);
            _builder.EmitLocalStore(temp);
        }

        var method = ptrInvocation.functionPointer.signature;
        EmitArguments(ptrInvocation.arguments, method.parameters, ptrInvocation.argumentRefKindsOpt);

        if (temp is not null) {
            _builder.EmitLocalLoad(temp);
            _builder.FreeTemp(temp);
        }

        _builder.EmitCalli(ptrInvocation.functionPointer);
        EmitCallCleanup(method, useKind);
    }

    private void EmitStackAllocExpression(BoundConvertedStackAllocExpression expression, bool used) {
        if (used) {
            EmitStackAlloc(expression.type, expression.count);
        } else {
            EmitExpression(expression.count, used: false);
        }
    }

    private void EmitStackAlloc(TypeSymbol type, BoundExpression count) {
        EmitExpression(count, used: true);
        _builder.Emit(OpCode.Localloc);
        // TODO If we ever encode to metadata we need to keep track of this
        // _sawStackalloc = true;
    }

    internal static bool UseCallResultAsAddress(BoundCallExpression call, AddressKind addressKind) {
        var methodRefKind = call.method.refKind;
        return methodRefKind == RefKind.Ref ||
               (IsAnyReadOnly(addressKind) && methodRefKind == RefKind.RefConst);
    }

    private void EmitConditionalOperatorAddress(BoundConditionalOperator expression, AddressKind addressKind) {
        var consequenceLabel = new object();
        var doneLabel = new object();

        EmitConditionalBranch(expression.condition, ref consequenceLabel, sense: true);
        AddExpressionTemp(EmitAddress(expression.falseExpression, addressKind));

        _builder.EmitBranch(OpCode.Br, doneLabel);
        _builder.MarkLabel(consequenceLabel);

        AddExpressionTemp(EmitAddress(expression.trueExpression, addressKind));

        _builder.MarkLabel(doneLabel);
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
            _builder.Emit(OpCode.Readonly);

        if (((ArrayTypeSymbol)arrayAccess.receiver.StrippedType()).isSZArray)
            _builder.EmitWithSymbolToken(OpCode.Ldelema, arrayAccess.type);
        else
            EmitArrayElementAddressInternal((ArrayTypeSymbol)arrayAccess.index.type);
    }

    private void EmitArrayElementAddressInternal(ArrayTypeSymbol type) {
        _builder.EmitArrayAddress(type);
    }

    private void EmitArrayElementStoreInternal(ArrayTypeSymbol type) {
        _builder.EmitArraySet(type);
    }

    private void EmitArrayElementLoadInternal(ArrayTypeSymbol type) {
        _builder.EmitArrayGet(type);
    }

    private void EmitArrayCreation(ArrayTypeSymbol type) {
        _builder.EmitArrayCreate(type);
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
        for (var i = 0; i < indices.Length; i++) {
            var index = indices[i];
            _builder.Emit(OpCode.Ldc_I4, index);
        }
    }

    private void TreatLongsAsNative(SpecialType specialType) {
        if (specialType == SpecialType.Int)
            _builder.Emit(OpCode.Conv_Ovf_I);
    }

    private VariableDefinition EmitLocalAddress(BoundDataContainerExpression localAccess, AddressKind addressKind) {
        var local = localAccess.dataContainer;

        if (!HasHome(localAccess, addressKind))
            return EmitAddressOfTempClone(localAccess);

        if (IsStackLocal(local)) {
            if (local.refKind == RefKind.None)
                throw ExceptionUtilities.UnexpectedValue(local.refKind);
        } else {
            _builder.EmitLocalAddress(local);
        }

        return null;
    }

    private VariableDefinition EmitParameterAddress(BoundParameterExpression parameter, AddressKind addressKind) {
        var parameterSymbol = parameter.parameter;

        if (!HasHome(parameter, addressKind))
            return EmitAddressOfTempClone(parameter);

        var slot = ParameterSlot(parameterSymbol);
        if (parameterSymbol.refKind == RefKind.None)
            _builder.EmitLoadArgumentAddr(slot);
        else
            _builder.EmitLoadArgument(slot);

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
        _builder.EmitWithSymbolToken(OpCode.Ldsflda, field);
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

        _builder.EmitWithSymbolToken(field.refKind == RefKind.None ? OpCode.Ldflda : OpCode.Ldfld, field);

        if (field.isFixedSizeBuffer) {
            var fixedImpl = _module.GetFixedImplementationType(field as SourceFixedFieldSymbol);
            var fixedElementField = fixedImpl.fixedElementField;

            if (fixedElementField is not null)
                _builder.EmitWithSymbolToken(OpCode.Ldflda, fixedElementField);
        }

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
        return receiverType.StrippedType().typeKind == TypeKind.TemplateParameter &&
            addressKind != AddressKind.Constrained;
    }

    private VariableDefinition EmitAddressOfTempClone(BoundExpression expression) {
        EmitExpression(expression, true);
        var value = AllocateTemp(expression.type);
        _builder.EmitLocalStore(value);
        _builder.EmitLocalAddress(value);
        return value;
    }

    private void EmitInitObj(TypeSymbol type, bool used) {
        if (used) {
            var temp = AllocateTemp(type);
            _builder.EmitLocalAddress(temp);
            _builder.EmitWithSymbolToken(OpCode.Initobj, type);
            _builder.EmitLocalLoad(temp);
            _builder.FreeTemp(temp);
        }
    }

    internal void EmitConstantValue(ConstantValue constant, TypeSymbol type, bool promoteToLong = true) {
        var value = constant.value;

        if (value is null) {
            if (IsReferenceType(type.StrippedType()))
                _builder.Emit(OpCode.Ldnull);
            else
                EmitInitObj(type, true);

            return;
        }

        // TODO Weird case where imported enum underlying types are bigger than we think they are
        var discriminator = (type.originalDefinition is PENamedTypeSymbol && constant.specialType != SpecialType.None)
            ? constant.specialType
            : type.IsEnumType() ? type.GetEnumUnderlyingType().specialType : type.specialType;

        switch (discriminator) {
            case SpecialType.Int:
            case SpecialType.Int64:
                EmitLongConstant((long)value);
                break;
            case SpecialType.Int8:
                if (promoteToLong)
                    EmitLongConstant((sbyte)value);
                else
                    EmitIntConstant((sbyte)value);

                break;
            case SpecialType.Int16:
                if (promoteToLong)
                    EmitLongConstant((short)value);
                else
                    EmitIntConstant((short)value);

                break;
            case SpecialType.Int32:
                if (promoteToLong)
                    EmitLongConstant((int)value);
                else
                    EmitIntConstant((int)value);

                break;
            case SpecialType.UInt8:
                if (promoteToLong)
                    EmitLongConstant((byte)value);
                else
                    EmitIntConstant((byte)value);

                break;
            case SpecialType.UInt16:
                if (promoteToLong)
                    EmitLongConstant((ushort)value);
                else
                    EmitIntConstant((ushort)value);

                break;
            case SpecialType.UInt32:
                if (promoteToLong)
                    EmitLongConstant((uint)value);
                else
                    EmitIntConstant(unchecked((int)(uint)value));

                break;
            case SpecialType.UInt64:
                EmitLongConstant((long)(ulong)value);
                break;
            case SpecialType.Char:
                EmitCharConstant((char)value);
                break;
            case SpecialType.Bool:
                EmitBoolConstant((bool)value);
                break;
            case SpecialType.Decimal:
            case SpecialType.Float64:
                EmitDoubleConstant((double)value);
                break;
            case SpecialType.Float32:
                EmitSingleConstant((float)value);
                break;
            case SpecialType.String:
                EmitStringConstant((string)value);
                break;
            case SpecialType.Nullable: {
                    var underlyingType = type.GetNullableUnderlyingType();
                    var underlyingDiscriminator = underlyingType.IsEnumType()
                        ? underlyingType.EnumUnderlyingTypeOrSelf().specialType
                        : underlyingType.specialType;

                    if (IsValueType(underlyingType)) {
                        EmitConstantValue(new ConstantValue(value, underlyingDiscriminator), underlyingType);
                        _builder.EmitNewobjNullable(underlyingType);
                    } else if (underlyingType.specialType == SpecialType.Any) {
                        goto case SpecialType.Any;
                    } else {
                        var inferredType = InferType(value);
                        EmitConstantValue(constant, inferredType);
                    }
                }

                break;
            case SpecialType.Any: {
                    // TODO Ensure constantValue is never lying to us
                    var inferredType = constant.specialType == SpecialType.None
                        ? InferType(value)
                        : CorLibrary.GetSpecialType(constant.specialType);

                    EmitConstantValue(constant, inferredType);
                    EmitBox(inferredType);
                }

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(discriminator);
        }
    }

    private TypeSymbol InferType(object value) {
        return CorLibrary.GetSpecialType(SpecialTypeExtensions.SpecialTypeFromLiteralValue(value));
    }

    private void EmitDoubleConstant(double value) {
        _builder.Emit(OpCode.Ldc_R8, value);
    }

    private void EmitSingleConstant(float value) {
        _builder.Emit(OpCode.Ldc_R4, value);
    }

    private void EmitStringConstant(string value) {
        _builder.Emit(OpCode.Ldstr, value);
    }

    private void EmitCharConstant(char value) {
        EmitIntConstant(value);
    }

    internal void EmitLongConstant(long value) {
        if (value >= int.MinValue && value <= int.MaxValue) {
            EmitIntConstant((int)value);
            _builder.Emit(OpCode.Conv_I8);
        } else if (value >= uint.MinValue && value <= uint.MaxValue) {
            EmitIntConstant(unchecked((int)value));
            _builder.Emit(OpCode.Conv_U8);
        } else {
            _builder.Emit(OpCode.Ldc_I8, value);
        }
    }

    internal void EmitIntConstant(int value) {
        var code = OpCode.Nop;

        switch (value) {
            case -1: code = OpCode.Ldc_I4_M1; break;
            case 0: code = OpCode.Ldc_I4_0; break;
            case 1: code = OpCode.Ldc_I4_1; break;
            case 2: code = OpCode.Ldc_I4_2; break;
            case 3: code = OpCode.Ldc_I4_3; break;
            case 4: code = OpCode.Ldc_I4_4; break;
            case 5: code = OpCode.Ldc_I4_5; break;
            case 6: code = OpCode.Ldc_I4_6; break;
            case 7: code = OpCode.Ldc_I4_7; break;
            case 8: code = OpCode.Ldc_I4_8; break;
        }

        if (code != OpCode.Nop) {
            _builder.Emit(code);
        } else {
            if (unchecked((sbyte)value == value))
                _builder.Emit(OpCode.Ldc_I4_S, unchecked((sbyte)value));
            else
                _builder.Emit(OpCode.Ldc_I4, value);
        }
    }

    private void EmitBoolConstant(bool value) {
        EmitIntConstant(value ? 1 : 0);
    }

    private void FreeOptTemp(VariableDefinition temp) {
        if (temp is not null)
            _builder.FreeTemp(temp);
    }

    private bool HasHome(BoundExpression expression, AddressKind addressKind) {
        return Binder.HasHome(expression, addressKind, _method, _stackLocals);
    }

    #region Statements

    private void EmitBlock(BoundBlockStatement block) {
        foreach (var statement in block.statements)
            EmitStatement(statement);
    }

    private void EmitStatement(BoundStatement statement) {
        switch (statement.kind) {
            case BoundKind.NopStatement:
                EmitNopStatement();
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
            case BoundKind.SequencePoint:
                EmitSequencePoint((BoundSequencePoint)statement);
                break;
            case BoundKind.SequencePointWithLocation:
                EmitSequencePointWithLocation((BoundSequencePointWithLocation)statement);
                break;
            case BoundKind.InlineILStatement:
                EmitInlineILStatement((BoundInlineILStatement)statement);
                break;
            case BoundKind.SwitchDispatch:
                EmitSwitchDispatch((BoundSwitchDispatch)statement);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(statement.kind);
        }
    }

    private void EmitSwitchDispatch(BoundSwitchDispatch dispatch) {
        EmitSwitchHeader(
            dispatch.expression,
            dispatch.cases.Select(p => new KeyValuePair<ConstantValue, object>(p.value, p.label)).ToArray(),
            dispatch.defaultLabel
        );
    }

    private void EmitSwitchHeader(
        BoundExpression expression,
        KeyValuePair<ConstantValue, object>[] switchCaseLabels,
        LabelSymbol fallThroughLabel) {
        VariableDefinition temp = null;
        LocalOrParameter key;

        switch (expression.kind) {
            case BoundKind.DataContainerExpression:
                var local = ((BoundDataContainerExpression)expression).dataContainer;

                if (local.refKind == RefKind.None && !IsStackLocal(local)) {
                    key = _builder.GetLocal(local);
                    break;
                }

                goto default;
            case BoundKind.ParameterExpression:
                var parameter = (BoundParameterExpression)expression;
                if (parameter.parameter.refKind == RefKind.None) {
                    key = ParameterSlot(parameter.parameter);
                    break;
                }
                goto default;

            default:
                EmitExpression(expression, true);
                temp = AllocateTemp(expression.type);
                _builder.EmitLocalStore(temp);
                key = temp;
                break;
        }

        if (expression.type.specialType == SpecialType.String) {
            // if (lengthBasedSwitchStringJumpTableOpt is null) {
            EmitStringSwitchJumpTable(switchCaseLabels, fallThroughLabel, key, expression.syntax, expression.type);
            // } else {
            // this.EmitLengthBasedStringSwitchJumpTable(lengthBasedSwitchStringJumpTableOpt, fallThroughLabel, key, expression.Syntax, expression.Type);
            // }
        } else {
            EmitIntegerSwitchJumpTable(
                switchCaseLabels,
                fallThroughLabel,
                key,
                expression.type.EnumUnderlyingTypeOrSelf().specialType
            );
        }

        FreeOptTemp(temp);
    }

    private void EmitStringSwitchJumpTable(
        KeyValuePair<ConstantValue, object>[] switchCaseLabels,
        LabelSymbol fallThroughLabel,
        LocalOrParameter key,
        SyntaxNode syntaxNode,
        TypeSymbol keyType) {
        // TODO
    }

    private void EmitIntegerSwitchJumpTable(
        KeyValuePair<ConstantValue, object>[] caseLabels,
        object fallThroughLabel,
        LocalOrParameter key,
        SpecialType keyTypeCode) {
        var emitter = new SwitchIntegralJumpTableEmitter(this, _builder, caseLabels, fallThroughLabel, keyTypeCode, key);
        emitter.EmitJumpTable();
    }

    private void EmitNopStatement() {
        if (_ilEmitStyle == ILEmitStyle.Debug)
            _builder.Emit(OpCode.Nop);
    }

    private void EmitLabelStatement(BoundLabelStatement statement) {
        _builder.MarkLabel(statement.label);
    }

    private void EmitGotoStatement(BoundGotoStatement statement) {
        _builder.EmitBranch(OpCode.Br, statement.label);
    }

    private void EmitConditionalGotoStatement(BoundConditionalGotoStatement statement) {
        object label = statement.label;
        EmitConditionalBranch(statement.condition, ref label, statement.jumpIfTrue);
    }

    private void EmitConditionalBranch(BoundExpression condition, ref object dest, bool sense) {
oneMoreTime:

        OpCode iLCode;

        if (condition.constantValue is not null) {
            var taken = condition.constantValue.isDefaultValue != sense;

            if (taken) {
                dest ??= new object();
                _builder.EmitBranch(OpCode.Br, dest);
            }

            return;
        }

        switch (condition.kind) {
            case BoundKind.BinaryOperator:
                var binOp = (BoundBinaryOperator)condition;

                if (binOp.operatorKind.OperatorWithConditional() is
                    BinaryOperatorKind.ConditionalOr or BinaryOperatorKind.ConditionalAnd) {
                    var stack = ArrayBuilder<(BoundExpression condition, StrongBox<object> destBox, bool sense)>
                        .GetInstance();

                    var destBox = new StrongBox<object>(dest);
                    stack.Push((binOp, destBox, sense));

                    do {
                        var top = stack.Pop();

                        if (top.condition is null) {
                            var fallThrough = top.destBox.Value;

                            if (fallThrough is not null)
                                _builder.MarkLabel(fallThrough);
                        } else if (top.condition.constantValue is null &&
                                   top.condition is BoundBinaryOperator binary &&
                                   binary.operatorKind.OperatorWithConditional()
                                    is BinaryOperatorKind.ConditionalOr or BinaryOperatorKind.ConditionalAnd) {
                            if (binary.operatorKind.OperatorWithConditional() is BinaryOperatorKind.ConditionalOr
                                ? !top.sense : top.sense) {
                                var fallThrough = new StrongBox<object>();

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
                    case BinaryOperatorKind.Equal when binOp.left.type.specialType != SpecialType.String:
                    case BinaryOperatorKind.NotEqual when binOp.left.type.specialType != SpecialType.String:
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
                        dest ??= new object();
                        _builder.EmitBranch(iLCode, dest, revOpCode);
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

                if (isOp.right.IsLiteralNull()) {
                    _builder.Emit(OpCode.Ldnull);
                    _builder.Emit(isOp.isNot ? OpCode.Cgt_Un : OpCode.Ceq);
                } else {
                    _builder.EmitWithSymbolToken(OpCode.Isinst, isOp.right.type);
                }

                iLCode = sense ? OpCode.Brtrue : OpCode.Brfalse;
                dest ??= new object();
                _builder.EmitBranch(iLCode, dest);
                return;
            default:
                EmitExpression(condition, true);

                var conditionType = condition.type;

                if (IsReferenceType(conditionType) && !conditionType.IsVerifierReference())
                    EmitBox(conditionType);

                iLCode = sense ? OpCode.Brtrue : OpCode.Brfalse;
                dest ??= new object();
                _builder.EmitBranch(iLCode, dest);
                return;
        }
    }

    private static OpCode CodeForJump(BoundBinaryOperator op, bool sense, out OpCode revOpCode) {
        int opIdx;

        switch (op.operatorKind.Operator()) {
            case BinaryOperatorKind.Equal:
                revOpCode = !sense ? OpCode.Beq : OpCode.Bne_Un;
                return sense ? OpCode.Beq : OpCode.Bne_Un;
            case BinaryOperatorKind.NotEqual:
                revOpCode = !sense ? OpCode.Bne_Un : OpCode.Beq;
                return sense ? OpCode.Bne_Un : OpCode.Beq;
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

        if (IsUnsignedBinaryOperator(op))
            opIdx += 2 * IL_OP_CODE_ROW_LENGTH;
        else if (IsFloat(op.operatorKind))
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
        if (ts.IsEnumType())
            return true;

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

        _builder.EmitReturn();
    }

    private void EmitTryStatement(BoundTryStatement statement) {
        var hasCatch = statement.catchBody is not null;
        var hasFinally = statement.finallyBody is not null;

        _builder.BeginTry();

        EmitBlock((BoundBlockStatement)statement.body);

        if (hasCatch) {
            _builder.BeginCatch();
            EmitBlock((BoundBlockStatement)statement.catchBody);
        }

        if (hasFinally) {
            _builder.BeginFinally();
            EmitBlock((BoundBlockStatement)statement.finallyBody);
        }

        _builder.EndTry(hasFinally);
    }

    private void EmitSequencePoint(BoundSequencePoint node) {
        var syntax = node.syntax;

        var statement = node.statement;
        var instructionsEmitted = 0;
        var index = _builder.instructionsEmitted;

        if (statement is not null)
            instructionsEmitted = EmitStatementAndCountInstructions(statement);

        if (instructionsEmitted == 0 && _ilEmitStyle == ILEmitStyle.Debug)
            _builder.Emit(OpCode.Nop);

        if (_emitPdbSequencePoints) {
            if (syntax is null)
                EmitHiddenSequencePoint(index);
            else
                EmitSequencePoint(syntax, index);
        }
    }

    private void EmitHiddenSequencePoint(int instructionIndex) {
        _builder.DefineHiddenSequencePoint(instructionIndex);
    }

    private void EmitSequencePoint(SyntaxNode syntax, int instructionIndex) {
        EmitSequencePoint(syntax.syntaxTree, syntax.location, instructionIndex);
    }

    private void EmitSequencePoint(SyntaxTree syntaxTree, TextLocation location, int instructionIndex) {
        _builder.DefineSequencePoint(syntaxTree, location, instructionIndex);
    }

    private int EmitStatementAndCountInstructions(BoundStatement statement) {
        var n = _builder.instructionsEmitted;
        EmitStatement(statement);
        return _builder.instructionsEmitted - n;
    }

    private void EmitSequencePointWithLocation(BoundSequencePointWithLocation node) {
        var location = node.location;

        var statement = node.statement;
        var instructionsEmitted = 0;
        var index = _builder.instructionsEmitted;

        if (statement is not null)
            instructionsEmitted = EmitStatementAndCountInstructions(statement);

        if (instructionsEmitted == 0 && location is not null && _ilEmitStyle == ILEmitStyle.Debug)
            _builder.Emit(OpCode.Nop);

        if (location is not null && _emitPdbSequencePoints)
            EmitSequencePoint(node.syntax.syntaxTree, location, index);
    }

    private void EmitInlineILStatement(BoundInlineILStatement statement) {
        foreach (var (opCode, constant, symbol) in statement.instructions) {
            if (opCode == OpCode.Calli) {
                _builder.EmitCalli(symbol as FunctionPointerTypeSymbol);
                continue;
            }

            if (symbol is not null) {
                switch (symbol) {
                    case FieldSymbol field:
                        _builder.EmitWithSymbolToken(opCode, field);
                        continue;
                    case MethodSymbol method:
                        _builder.EmitWithSymbolToken(opCode, method);
                        continue;
                    case TypeSymbol type:
                        _builder.EmitWithSymbolToken(opCode, type);
                        continue;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(symbol.kind);
                }
            }

            _builder.Emit(opCode);

            if (constant is not null) {
                var type = CorLibrary.GetSpecialType(constant.specialType);
                EmitConstantValue(constant, type);
            }
        }
    }

    private void EmitLocalDeclarationStatement(BoundLocalDeclarationStatement statement) {
        var declaration = statement.declaration;
        var local = declaration.dataContainer;

        _builder.DeclareLocal(
            local.type,
            local,
            local.name,
            local.synthesizedKind,
            local.isRef ? LocalSlotConstraints.ByRef : LocalSlotConstraints.None,
            false
        );

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
            if (!used)
                return;

            EmitConstantExpression(expression.type, constantValue, used);
            return;
        }

        switch (expression.kind) {
            case BoundKind.ThisExpression:
                if (used)
                    EmitThisExpression((BoundThisExpression)expression);

                break;
            case BoundKind.DefaultExpression:
                EmitDefaultExpression((BoundDefaultExpression)expression, used);
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
            case BoundKind.AddressOfOperator:
                EmitAddressOfOperator((BoundAddressOfOperator)expression, used);
                break;
            case BoundKind.PointerIndirectionOperator:
                EmitPointerIndirectionOperator((BoundPointerIndirectionOperator)expression, used);
                break;
            case BoundKind.FunctionPointerLoad:
                EmitFunctionPointerLoad((BoundFunctionPointerLoad)expression, used);
                break;
            case BoundKind.FunctionLoad:
                EmitFunctionLoad((BoundFunctionLoad)expression, used);
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
            case BoundKind.ArrayAccessExpression:
                EmitArrayElementLoad((BoundArrayAccessExpression)expression, used);
                break;
            case BoundKind.IndexerAccessExpression:
                EmitIndexerAccessExpression((BoundIndexerAccessExpression)expression, used);
                break;
            case BoundKind.TypeOfExpression:
                if (used)
                    EmitTypeOfExpression((BoundTypeOfExpression)expression);

                break;
            case BoundKind.SizeOfOperator:
                if (used)
                    EmitSizeOfExpression((BoundSizeOfOperator)expression);

                break;
            case BoundKind.TypeExpression:
                EmitTypeExpression((BoundTypeExpression)expression);
                break;
            case BoundKind.MethodGroup:
                EmitMethodGroup((BoundMethodGroup)expression);
                break;
            case BoundKind.ThrowExpression:
                EmitThrowExpression((BoundThrowExpression)expression, used);
                break;
            case BoundKind.FunctionPointerCallExpression:
                EmitCalli((BoundFunctionPointerCallExpression)expression, used ? UseKind.UsedAsValue : UseKind.Unused);
                break;
            case BoundKind.ConvertedStackAllocExpression:
                EmitStackAllocExpression((BoundConvertedStackAllocExpression)expression, used);
                break;
            case BoundKind.StackSlotExpression:
                EmitStackSlotExpression((BoundStackSlotExpression)expression, used);
                break;
            case BoundKind.FieldSlotExpression:
                EmitFieldSlotExpression((BoundFieldSlotExpression)expression, used);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(expression.kind);
        }
    }

    private void EmitDefaultExpression(BoundDefaultExpression expression, bool used) {
        EmitDefaultValue(expression.type, used, expression.syntax);
    }

    private void EmitConstantExpression(TypeSymbol type, ConstantValue constant, bool used) {
        if (used) {
            if ((type is not null) && (type.typeKind == TypeKind.TemplateParameter) && constant.value is null)
                EmitInitObj(type, used);
            else
                EmitConstantValue(constant, type);
        }
    }

    private void EmitMethodGroup(BoundMethodGroup _) {
        // Unresolved method groups are only legal in scripts where the Evaluator returns something
        // Has no semantic meaning
        _builder.Emit(OpCode.Nop);
    }

    private void EmitTypeExpression(BoundTypeExpression _) {
        // Isolated type expressions are only legal in scripts where the Evaluator returns something
        // Has no semantic meaning
        _builder.Emit(OpCode.Nop);
    }

    private void EmitAddressOfOperator(BoundAddressOfOperator expression, bool used) {
        var temp = EmitAddress(expression.operand, AddressKind.ReadOnlyStrict);

        if (used)
            _builder.Emit(OpCode.Conv_U);

        EmitPopIfUnused(used);
    }

    private void EmitPointerIndirectionOperator(BoundPointerIndirectionOperator expression, bool used) {
        EmitExpression(expression.operand, used: true);

        if (!expression.refersToLocation)
            EmitLoadIndirect(expression.type);

        EmitPopIfUnused(used);
    }

    private void EmitFunctionPointerLoad(BoundFunctionPointerLoad load, bool used) {
        if (used) {
            if ((load.targetMethod.isAbstract || load.targetMethod.isVirtual) && load.targetMethod.isStatic) {
                if (load.constrainedToTypeOpt is not { typeKind: TypeKind.TemplateParameter }) {
                    throw ExceptionUtilities.Unreachable();
                }

                _builder.EmitWithSymbolToken(OpCode.Constrained, load.constrainedToTypeOpt);
            }

            _builder.EmitWithSymbolToken(OpCode.Ldftn, load.targetMethod);
        }
    }

    private void EmitFunctionLoad(BoundFunctionLoad load, bool used) {
        if (used) {
            _builder.Emit(OpCode.Ldnull);
            _builder.EmitWithSymbolToken(OpCode.Ldftn, load.targetMethod);
            _builder.EmitNewobjFunc(load.type.StrippedType() as FunctionTypeSymbol);
        }
    }

    private void EmitTypeOfExpression(BoundTypeOfExpression expression) {
        var type = expression.sourceType.type;
        _builder.EmitWithSymbolToken(OpCode.Ldtoken, type);
        _builder.EmitGetTypeFromHandle(type);
    }

    private void EmitSizeOfExpression(BoundSizeOfOperator boundSizeOfOperator) {
        var type = boundSizeOfOperator.sourceType.type;
        _builder.EmitWithSymbolToken(OpCode.Sizeof, type);
    }

    private void EmitThrowExpression(BoundThrowExpression expression, bool used) {
        var thrown = expression.expression;

        if (thrown is not null) {
            EmitExpression(thrown, true);

            var exprType = thrown.type;

            if (exprType?.typeKind == TypeKind.TemplateParameter)
                EmitBox(exprType);
        }

        _builder.Emit(thrown is null ? OpCode.Rethrow : OpCode.Throw);

        EmitDefaultValue(expression.type, used, expression.syntax);
    }

    private void EmitArrayElementLoad(BoundArrayAccessExpression expression, bool used) {
        EmitExpression(expression.receiver, used: true);
        EmitArrayIndex(expression.index);

        if (((ArrayTypeSymbol)expression.receiver.StrippedType()).isSZArray) {
            var elementType = expression.type;

            if (elementType.IsEnumType())
                elementType = ((NamedTypeSymbol)elementType).enumUnderlyingType;

            switch (elementType.specialType) {
                case SpecialType.Int:
                    _builder.Emit(OpCode.Ldelem_I8);
                    break;
                case SpecialType.Bool:
                    _builder.Emit(OpCode.Ldelem_U1);
                    break;
                case SpecialType.Decimal:
                    _builder.Emit(OpCode.Ldelem_R8);
                    break;
                default:
                    if (elementType.IsVerifierReference()) {
                        _builder.Emit(OpCode.Ldelem_Ref);
                    } else {
                        if (used) {
                            _builder.EmitWithSymbolToken(OpCode.Ldelem, elementType);
                        } else {
                            if (elementType.StrippedType().typeKind == TypeKind.TemplateParameter)
                                _builder.Emit(OpCode.Readonly);

                            _builder.EmitWithSymbolToken(OpCode.Ldelema, elementType);
                        }
                    }

                    break;
            }
        } else {
            EmitArrayElementLoadInternal((ArrayTypeSymbol)expression.type);
        }

        EmitPopIfUnused(used);
    }

    private void EmitIndexerAccessExpression(BoundIndexerAccessExpression expression, bool used) {
        EmitExpression(expression.receiver, used: true);
        EmitArrayIndex(expression.index);

        _builder.EmitStringChars();

        EmitPopIfUnused(used);
    }

    private void EmitArrayCreationExpression(BoundArrayCreationExpression expression, bool used) {
        var arrayType = (ArrayTypeSymbol)expression.StrippedType();

        EmitArrayIndices(expression.sizes);

        if (arrayType.isSZArray)
            _builder.EmitWithSymbolToken(OpCode.Newarr, arrayType.elementType);
        else
            EmitArrayCreation(arrayType);

        if (expression.initializer is not null)
            EmitArrayInitializers(arrayType, expression.initializer as BoundInitializerList);

        EmitPopIfUnused(used);
    }

    private void EmitArrayInitializers(ArrayTypeSymbol arrayType, BoundInitializerList initList) {
        var initExprs = initList.items;
        EmitElementInitializers(arrayType, initExprs, true);
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

            _builder.EmitWithSymbolToken(OpCode.Newobj, constructor);

            EmitPopIfUnused(used);
        }
    }

    private void EmitElementInitializers(
        ArrayTypeSymbol arrayType,
        ImmutableArray<BoundExpression> inits,
        bool includeConstants) {
        if (!IsMultidimensionalInitializer(inits)) {
            EmitVectorElementInitializers(arrayType, inits, includeConstants);
        } else {
            EmitMultidimensionalElementInitializers(arrayType, inits, includeConstants);
        }
    }

    private void EmitVectorElementInitializers(
        ArrayTypeSymbol arrayType,
        ImmutableArray<BoundExpression> inits,
        bool includeConstants) {
        for (var i = 0; i < inits.Length; i++) {
            var init = inits[i];

            if (ShouldEmitInitExpression(includeConstants, init)) {
                _builder.Emit(OpCode.Dup);
                EmitIntConstant(i);
                EmitExpression(init, true);
                EmitVectorElementStore(arrayType);
            }
        }
    }

    private void EmitMultidimensionalElementInitializers(
        ArrayTypeSymbol arrayType,
        ImmutableArray<BoundExpression> inits,
        bool includeConstants) {
        var indices = new ArrayBuilder<IndexDesc>();

        for (var i = 0; i < inits.Length; i++) {
            indices.Push(new IndexDesc(i, ((BoundInitializerList)inits[i]).items));
            EmitAllElementInitializersRecursive(arrayType, indices, includeConstants);
        }
    }

    private void EmitAllElementInitializersRecursive(
        ArrayTypeSymbol arrayType,
        ArrayBuilder<IndexDesc> indices,
        bool includeConstants) {
        var top = indices.Peek();
        var inits = top.initializers;

        if (IsMultidimensionalInitializer(inits)) {
            for (var i = 0; i < inits.Length; i++) {
                indices.Push(new IndexDesc(i, ((BoundInitializerList)inits[i]).items));
                EmitAllElementInitializersRecursive(arrayType, indices, includeConstants);
            }
        } else {
            for (var i = 0; i < inits.Length; i++) {
                var init = inits[i];

                if (ShouldEmitInitExpression(includeConstants, init)) {
                    _builder.Emit(OpCode.Dup);

                    foreach (var row in indices)
                        EmitIntConstant(row.index);

                    EmitIntConstant(i);

                    var initExpr = inits[i];
                    EmitExpression(initExpr, true);
                    EmitArrayElementStore(arrayType);
                }
            }
        }

        indices.Pop();
    }

    private static bool ShouldEmitInitExpression(bool includeConstants, BoundExpression init) {
        return includeConstants || init.constantValue is null;
    }

    private static bool IsMultidimensionalInitializer(ImmutableArray<BoundExpression> inits) {
        return inits.Length != 0 && inits[0].kind == BoundKind.InitializerList;
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

        if (method.containingType.Equals(StandardLibrary.LowLevel.underlyingNamedType)) {
            switch (method.name) {
                case "ThrowNullConditionException": {
                        _builder.EmitThrowNullCondition();
                        // This is to balance the stack
                        EmitDefaultValue(
                            CorLibrary.GetSpecialType(SpecialType.Exception),
                            useKind != UseKind.Unused,
                            expression.syntax
                        );
                    }

                    return;
                case "Sort": {
                        EmitArguments(arguments, method.parameters, expression.argumentRefKinds);
                        _builder.EmitSort(method.templateArguments[0].type.type);
                        EmitCallCleanup(method, useKind);
                    }

                    return;
                case "Length": {
                        EmitArguments(arguments, method.parameters, expression.argumentRefKinds);
                        _builder.EmitLength(method.templateArguments[0].type.type);
                        EmitCallCleanup(method, useKind);
                    }

                    return;
                case "SizeOf":
                    if (useKind != UseKind.Unused)
                        _builder.EmitWithSymbolToken(OpCode.Sizeof, method.templateArguments[0].type.type);

                    return;
            }
        }

        if (method.containingType.Equals(StandardLibrary.Random.underlyingNamedType)) {
            EmitRandomCall(method, arguments, expression.argumentRefKinds, useKind);
            return;
        }

        EmitArguments(arguments, method.parameters, expression.argumentRefKinds);

        if (method.isAbstract || method.isVirtual) {
            if (receiver is not BoundTypeExpression { type.typeKind: TypeKind.TemplateParameter })
                throw ExceptionUtilities.Unreachable();

            _builder.EmitWithSymbolToken(OpCode.Constrained, receiver.type);
        }

        _builder.EmitWithSymbolToken(OpCode.Call, method);

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
                    _builder.EmitLdsfldRandom();

                    var argument = Lowerer.CreateNullableGetValueCall(
                        null,
                        arguments[0],
                        arguments[0].StrippedType()
                    );

                    var refKind = GetArgumentRefKind(arguments, method.parameters, argumentRefKinds, 0);
                    EmitArgument(argument, refKind);

                    _builder.EmitRandomNextInt64();

                    EmitCallCleanup(method, useKind);
                }

                return;
            case "Random": {
                    _builder.EmitLdsfldRandom();
                    _builder.EmitRandomNextDouble();

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
                    if (receiverUseKind != UseKind.UsedAsAddress) {
                        tempOpt = AllocateTemp(parentCallReceiverType);
                        _builder.EmitLocalStore(tempOpt);
                        _builder.EmitLocalAddress(tempOpt);
                    }

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

                addressKind = (callKind == CallKind.ConstrainedCallVirt)
                    ? AddressKind.Constrained
                    : AddressKind.Writeable;
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
                else if (actualMethodTargetedByTheCall.isMetadataFinal && CanUseCallOnRefTypeReceiver(receiver))
                    callKind = CallKind.Call;
            }

            var arguments = call.arguments;
            EmitArguments(arguments, method.parameters, call.argumentRefKinds);

            switch (callKind) {
                case CallKind.Call:
                    _builder.EmitWithSymbolToken(OpCode.Call, actualMethodTargetedByTheCall);
                    break;
                case CallKind.CallVirt:
                    _builder.EmitWithSymbolToken(OpCode.Callvirt, actualMethodTargetedByTheCall);
                    break;
                case CallKind.ConstrainedCallVirt:
                    _builder.EmitWithSymbolToken(OpCode.Constrained, receiver.type);
                    _builder.EmitWithSymbolToken(OpCode.Callvirt, actualMethodTargetedByTheCall);
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
                object whenNotNullLabel = null;

                if (!IsReferenceType(receiverType)) {
                    EmitDefaultValue(receiverType, true, receiver.syntax);
                    EmitBox(receiverType);
                    whenNotNullLabel = new object();
                    _builder.EmitBranch(OpCode.Brtrue, whenNotNullLabel);
                }

                EmitLoadIndirect(receiverType);
                temp = AllocateTemp(receiverType);
                _builder.EmitLocalStore(temp);
                _builder.EmitLocalAddress(temp);

                if (whenNotNullLabel is not null)
                    _builder.MarkLabel(whenNotNullLabel);
            }
        }
    }

    private void EmitDefaultValue(TypeSymbol type, bool used, SyntaxNode syntaxNode) {
        if (used) {
            if (!type.IsTemplateParameter()) {
                var constantValue = type.IsVerifierValue() ? LiteralUtilities.GetDefaultValue(type.specialType) : null;

                if (constantValue is not null) {
                    EmitConstantValue(new ConstantValue(constantValue, type.specialType), type);
                    return;
                }
            }

            if (type.IsPointerOrFunctionPointer() || type.specialType == SpecialType.UIntPtr) {
                _builder.Emit(OpCode.Ldc_I4_0);
                _builder.Emit(OpCode.Conv_U);
            } else if (type.specialType == SpecialType.IntPtr) {
                _builder.Emit(OpCode.Ldc_I4_0);
                _builder.Emit(OpCode.Conv_I);
            } else {
                EmitInitObj(type, true);
            }
        }
    }

    internal static bool ReceiverIsInstanceCall(BoundCallExpression call, out BoundCallExpression nested) {
        if (call.receiver is
            BoundCallExpression { method: { requiresInstanceReceiver: true } method } receiver) {
            nested = receiver;
            return true;
        }

        nested = null;
        return false;
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

            if ((object)originalMethod == CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_getValue) ||
                (object)originalMethod == CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_getHasValue) ||
                (object)originalMethod == CorLibrary.GetWellKnownMember(WellKnownMembers.Nullable_GetValueOrDefault)) {
                return true;
            }
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
            case BoundKind.FunctionPointerCallExpression:
                return ((BoundFunctionPointerCallExpression)receiver).functionPointer.signature.refKind != RefKind.None;
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
                    case ConversionKind.MethodGroup:
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

            _builder.Emit(OpCode.Ldnull);
            _builder.Emit(sense ? OpCode.Cgt_Un : OpCode.Ceq);
            return true;
        } else {
            EmitExpression(condition, used: true);

            _builder.Emit(OpCode.Ldc_I4_0);
            _builder.Emit(sense ? OpCode.Cgt_Un : OpCode.Ceq);
            return true;
        }

        return false;
    }

    private void EmitConditionalOperator(BoundConditionalOperator expression, bool used) {
        if (used &&
            (expression.type.specialType.IsNumeric() || expression.type.specialType == SpecialType.Bool) &&
            expression.trueExpression.constantValue?.IsIntegralValueZeroOrOne(out var isConsequenceOne) == true &&
            expression.falseExpression.constantValue?.IsIntegralValueZeroOrOne(out var isAlternativeOne) == true &&
            isConsequenceOne != isAlternativeOne &&
            TryEmitComparison(expression.condition, sense: isConsequenceOne)) {
            var toType = expression.type.specialType;

            if (toType != SpecialType.Bool)
                EmitNumericConversion(SpecialType.Int, toType);

            return;
        }

        var consequenceLabel = new object();
        var doneLabel = new object();

        EmitConditionalBranch(expression.condition, ref consequenceLabel, sense: true);
        EmitExpression(expression.falseExpression, used);

        var mergeTypeOfAlternative = StackMergeType(expression.falseExpression);

        if (used) {
            if (IsVarianceCast(expression.type, mergeTypeOfAlternative)) {
                EmitStaticCast(expression.type);
                mergeTypeOfAlternative = expression.type;
            }
        }

        _builder.EmitBranch(OpCode.Br, doneLabel);

        _builder.MarkLabel(consequenceLabel);
        EmitExpression(expression.trueExpression, used);

        if (used) {
            var mergeTypeOfConsequence = StackMergeType(expression.trueExpression);

            if (IsVarianceCast(expression.type, mergeTypeOfConsequence)) {
                EmitStaticCast(expression.type);
                mergeTypeOfConsequence = expression.type;
            }
        }

        _builder.MarkLabel(doneLabel);
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

            if (expression.right.IsLiteralNull()) {
                _builder.Emit(OpCode.Ldnull);
                _builder.Emit(expression.isNot ? OpCode.Cgt_Un : OpCode.Ceq);
            } else {
                _builder.EmitWithSymbolToken(OpCode.Isinst, expression.right.type);

                if (!omitBooleanConversion) {
                    _builder.Emit(OpCode.Ldnull);
                    _builder.Emit(OpCode.Cgt_Un);
                }
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

            _builder.EmitWithSymbolToken(OpCode.Isinst, targetType);

            if (!targetType.IsVerifierReference())
                _builder.EmitWithSymbolToken(OpCode.Unbox_Any, targetType);
        }
    }

    private void EmitNullAssertOperator(BoundNullAssertOperator expression, bool used) {
        if (!expression.throwIfNull) {
            EmitExpression(expression.operand, true);
            EmitPopIfUnused(used);
            return;
        }

        EnsureGlobalsClassIsBuilt();

        var operand = expression.operand;
        EmitExpression(operand, true);

        _builder.EmitNullAssert(expression.type);

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
            EmitConversionToEnumUnderlyingType(binary);
        } while (stack.Count > 0);

        stack.Free();
    }

    private void EmitBinaryOperatorSimple(BoundBinaryOperator expression) {
        EmitExpression(expression.left, true);
        EmitExpression(expression.right, true);
        EmitBinaryOperatorInstruction(expression);
        EmitConversionToEnumUnderlyingType(expression);
    }

    private void EmitConversionToEnumUnderlyingType(BoundBinaryOperator expression) {
        TypeSymbol enumType;

        switch (expression.operatorKind.Operator() | expression.operatorKind.OperandTypes()) {
            case BinaryOperatorKind.EnumAndUnderlyingAddition:
            case BinaryOperatorKind.EnumSubtraction:
            case BinaryOperatorKind.EnumAndUnderlyingSubtraction:
                enumType = expression.left.type;
                break;
            case BinaryOperatorKind.EnumAnd:
            case BinaryOperatorKind.EnumOr:
            case BinaryOperatorKind.EnumXor:
                enumType = null;
                break;
            case BinaryOperatorKind.UnderlyingAndEnumSubtraction:
            case BinaryOperatorKind.UnderlyingAndEnumAddition:
                enumType = expression.right.type;
                break;
            default:
                enumType = null;
                break;
        }

        if (enumType is null)
            return;

        var type = enumType.GetEnumUnderlyingType().specialType;

        switch (type) {
            case SpecialType.UInt8:
                EmitNumericConversion(SpecialType.Int32, SpecialType.UInt8);
                break;
            case SpecialType.Int8:
                EmitNumericConversion(SpecialType.Int32, SpecialType.Int8);
                break;
            case SpecialType.Int16:
                EmitNumericConversion(SpecialType.Int32, SpecialType.Int16);
                break;
            case SpecialType.UInt16:
                EmitNumericConversion(SpecialType.Int32, SpecialType.UInt16);
                break;
        }
    }

    private void EmitBinaryOperatorInstruction(BoundBinaryOperator expression) {
        switch (expression.operatorKind.Operator()) {
            case BinaryOperatorKind.Multiplication:
                _builder.Emit(OpCode.Mul);
                break;
            case BinaryOperatorKind.Addition
                when (expression.operatorKind & BinaryOperatorKind.TypeMask) == BinaryOperatorKind.String:
                _builder.EmitStringConcat2();
                break;
            case BinaryOperatorKind.Addition:
                _builder.Emit(OpCode.Add);
                break;
            case BinaryOperatorKind.Subtraction:
                _builder.Emit(OpCode.Sub);
                break;
            case BinaryOperatorKind.Division:
                if (IsUnsignedBinaryOperator(expression))
                    _builder.Emit(OpCode.Div_Un);
                else
                    _builder.Emit(OpCode.Div);

                break;
            case BinaryOperatorKind.Modulo:
                if (IsUnsignedBinaryOperator(expression))
                    _builder.Emit(OpCode.Rem_Un);
                else
                    _builder.Emit(OpCode.Rem);

                break;
            case BinaryOperatorKind.LeftShift:
                _builder.Emit(OpCode.Shl);
                break;
            case BinaryOperatorKind.RightShift:
                if (IsUnsignedBinaryOperator(expression))
                    _builder.Emit(OpCode.Shr_Un);
                else
                    _builder.Emit(OpCode.Shr);

                break;
            case BinaryOperatorKind.UnsignedRightShift:
                _builder.Emit(OpCode.Shr_Un);
                break;
            case BinaryOperatorKind.And:
                _builder.Emit(OpCode.And);
                break;
            case BinaryOperatorKind.Xor:
                _builder.Emit(OpCode.Xor);
                break;
            case BinaryOperatorKind.Or:
                _builder.Emit(OpCode.Or);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(expression.operatorKind.Operator());
        }
    }

    private static bool IsUnsigned(SpecialType type) {
        switch (type) {
            case SpecialType.UInt8:
            case SpecialType.UInt16:
            case SpecialType.UInt32:
            case SpecialType.UInt64:
                return true;
        }
        return false;
    }

    private static bool IsUnsignedBinaryOperator(BoundBinaryOperator op) {
        var opKind = op.operatorKind;
        var type = opKind.OperandTypes();

        switch (type) {
            case BinaryOperatorKind.Enum:
            case BinaryOperatorKind.EnumAndUnderlying:
                return IsUnsigned(GetEnumPromotedType(op.left.type.GetEnumUnderlyingType().specialType));
            case BinaryOperatorKind.UnderlyingAndEnum:
                return IsUnsigned(GetEnumPromotedType(op.right.type.GetEnumUnderlyingType().specialType));
            case BinaryOperatorKind.UInt:
                return true;
            default:
                return false;
        }
    }

    private void EmitUnaryOperatorExpression(BoundUnaryOperator expression, bool used) {
        var operatorKind = expression.operatorKind;

        if (!used) {
            EmitExpression(expression.operand, used: false);
            return;
        }

        if (operatorKind.Operator() == UnaryOperatorKind.LogicalNegation &&
            operatorKind.OperandTypes() == UnaryOperatorKind.Bool) {
            EmitCondExpr(expression.operand, sense: false);
            return;
        }

        EmitExpression(expression.operand, used: true);

        switch (operatorKind.Operator()) {
            case UnaryOperatorKind.UnaryMinus:
                _builder.Emit(OpCode.Neg);
                break;
            case UnaryOperatorKind.BitwiseComplement:
                _builder.Emit(OpCode.Not);
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
            EmitBoolConstant(constant == sense);
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
                EmitBinaryCondOperatorHelper(OpCode.And, binOp.left, binOp.right, sense);
                return;
            case BinaryOperatorKind.Or:
                EmitBinaryCondOperatorHelper(OpCode.Or, binOp.left, binOp.right, sense);
                return;
            case BinaryOperatorKind.Xor:
                if (sense)
                    EmitBinaryCondOperatorHelper(OpCode.Xor, binOp.left, binOp.right, true);
                else
                    EmitBinaryCondOperatorHelper(OpCode.Ceq, binOp.left, binOp.right, true);

                return;
            case BinaryOperatorKind.NotEqual:
                sense = !sense;
                goto case BinaryOperatorKind.Equal;
            case BinaryOperatorKind.Equal:
                if (binOp.left.type.specialType == SpecialType.String) {
                    EmitStringEqualityOperator(binOp, sense);
                    return;
                }

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

                EmitBinaryCondOperatorHelper(OpCode.Ceq, binOp.left, binOp.right, sense);
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

        if (IsUnsignedBinaryOperator(binOp))
            opIdx += 4;
        else if (IsFloat(binOp.operatorKind))
            opIdx += 8;

        EmitBinaryCondOperatorHelper(CompOpCodes[opIdx], binOp.left, binOp.right, sense);
        return;
    }

    private void EmitStringEqualityOperator(BoundBinaryOperator condition, bool sense) {
        EmitExpression(condition.left, true);
        EmitExpression(condition.right, true);
        _builder.EmitStringEquality();
        EmitIsSense(sense);
    }

    private void EmitShortCircuitingOperator(
        BoundBinaryOperator condition,
        bool sense,
        bool stopSense,
        bool stopValue) {
        object lazyFallThrough = null;

        EmitConditionalBranch(condition.left, ref lazyFallThrough, stopSense);
        EmitCondExpr(condition.right, sense);

        if (lazyFallThrough is null)
            return;

        var labEnd = new object();
        _builder.EmitBranch(OpCode.Br, labEnd);

        _builder.MarkLabel(lazyFallThrough);
        EmitBoolConstant(stopValue);
        _builder.MarkLabel(labEnd);
    }

    private void EmitBinaryCondOperatorHelper(
        OpCode opCode,
        BoundExpression left,
        BoundExpression right,
        bool sense) {
        EmitExpression(left, true);
        EmitExpression(right, true);
        _builder.Emit(opCode);
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
            case BinaryOperatorKind.Float32:
            case BinaryOperatorKind.Float64:
                return true;
            default:
                return false;
        }
    }

    internal static bool OperatorHasSideEffects(BinaryOperatorKind kind) {
        switch (kind.Operator()) {
            case BinaryOperatorKind.Division:
            case BinaryOperatorKind.Modulo:
                return true;
            default:
                return false;
        }
    }

    internal static bool IsConditional(BinaryOperatorKind opKind) {
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
            _builder.Emit(OpCode.Ldc_I4_0);
            _builder.Emit(OpCode.Ceq);
        }
    }

    private void EmitAssignmentOperator(BoundAssignmentOperator expression, UseKind useKind) {
        EmitLocalDeclarationIfApplicable(expression);

        if (TryEmitAssignmentInPlace(expression, useKind != UseKind.Unused))
            return;

        var lhsUsesStack = EmitAssignmentPreamble(expression);
        EmitAssignmentValue(expression);
        var temp = EmitAssignmentDuplication(expression, useKind, lhsUsesStack);
        EmitStore(expression);
        EmitAssignmentPostfix(expression, temp, useKind);
    }

    private void EmitLocalDeclarationIfApplicable(BoundAssignmentOperator expression) {
        if (expression.left is BoundStackSlotExpression stackSlot) {
            var local = stackSlot.symbol as DataContainerSymbol;

            if (local is not null && _evaluatorProxies.Add(local)) {
                _builder.DeclareLocal(
                    local.type,
                    local,
                    local.name,
                    local.synthesizedKind,
                    local.isRef ? LocalSlotConstraints.ByRef : LocalSlotConstraints.None,
                    false
                );
            }
        }
    }

    private void EmitAssignmentPostfix(
        BoundAssignmentOperator assignment,
        VariableDefinition temp,
        UseKind useKind) {
        if (temp is not null) {
            if (useKind == UseKind.UsedAsAddress)
                _builder.EmitLocalAddress(temp);
            else
                _builder.EmitLocalLoad(temp);

            _builder.FreeTemp(temp);
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
            case BoundKind.FieldSlotExpression:
                var left = (BoundFieldSlotExpression)expression;
                EmitFieldStore(left.field, assignment.isRef);
                break;
            case BoundKind.DataContainerExpression:
                var local = (BoundDataContainerExpression)expression;

                if (local.dataContainer.refKind != RefKind.None && !assignment.isRef) {
                    EmitIndirectStore(local.dataContainer.type);
                } else {
                    if (IsStackLocal(local.dataContainer))
                        break;
                    else
                        _builder.EmitLocalStore(local.dataContainer);
                }

                break;
            case BoundKind.StackSlotExpression:
                var symbol = ((BoundStackSlotExpression)expression).symbol;

                if (symbol is DataContainerSymbol dataContainer) {
                    if (dataContainer.refKind != RefKind.None && !assignment.isRef) {
                        EmitIndirectStore(dataContainer.type);
                    } else {
                        if (IsStackLocal(dataContainer))
                            break;
                        else
                            _builder.EmitLocalStore(dataContainer);
                    }
                } else if (symbol is ParameterSymbol parameter) {
                    EmitParameterStore(parameter, assignment.isRef);
                    break;
                } else {
                    throw ExceptionUtilities.Unreachable();
                }

                break;
            case BoundKind.ArrayAccessExpression:
                var array = ((BoundArrayAccessExpression)expression).receiver;
                var arrayType = (ArrayTypeSymbol)array.StrippedType();
                EmitArrayElementStore(arrayType);
                break;
            case BoundKind.FunctionPointerCallExpression:
                EmitIndirectStore(expression.type);
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
            case BoundKind.PointerIndirectionOperator:
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
        _builder.EmitWithSymbolToken(OpCode.Stobj, thisRef.type);
    }

    private void EmitArrayElementStore(ArrayTypeSymbol arrayType) {
        if (arrayType.isSZArray)
            EmitVectorElementStore(arrayType);
        else
            EmitArrayElementStoreInternal(arrayType);
    }

    private void EmitVectorElementStore(ArrayTypeSymbol arrayType) {
        var elementType = arrayType.elementType;

        if (elementType.IsEnumType())
            elementType = ((NamedTypeSymbol)elementType).enumUnderlyingType;

        switch (elementType.specialType) {
            case SpecialType.Bool:
                _builder.Emit(OpCode.Stelem_I1);
                break;
            case SpecialType.Int:
                _builder.Emit(OpCode.Stelem_I8);
                break;
            case SpecialType.Decimal:
                _builder.Emit(OpCode.Stelem_R8);
                break;
            case SpecialType.None when elementType is PointerTypeSymbol:
                _builder.Emit(OpCode.Stelem_I);
                break;
            default:
                if (elementType.IsVerifierReference()) {
                    _builder.Emit(OpCode.Stelem_Ref);
                } else {
                    _builder.EmitWithSymbolToken(OpCode.Stelem, elementType);
                }

                break;
        }
    }

    private void EmitFieldStore(BoundFieldAccessExpression fieldAccess, bool refAssign) {
        EmitFieldStore(fieldAccess.field, refAssign);
    }

    private void EmitFieldStore(FieldSymbol field, bool refAssign) {
        if (field.refKind != RefKind.None && !refAssign) {
            EmitIndirectStore(field.type);
        } else {
            _builder.EmitWithSymbolToken(field.isStatic ? OpCode.Stsfld : OpCode.Stfld, field);
        }
    }

    private void EmitParameterStore(BoundParameterExpression parameter, bool refAssign) {
        EmitParameterStore(parameter.parameter, refAssign);
    }

    private void EmitParameterStore(ParameterSymbol parameter, bool refAssign) {
        if (parameter.refKind != RefKind.None && !refAssign) {
            EmitIndirectStore(parameter.type);
        } else {
            var slot = ParameterSlot(parameter);
            _builder.EmitStoreArgument(slot);
        }
    }

    private void EmitIndirectStore(TypeSymbol type) {
        if (type.IsEnumType())
            type = ((NamedTypeSymbol)type).enumUnderlyingType;

        switch (type.specialType) {
            case SpecialType.Bool:
            case SpecialType.Int8:
            case SpecialType.UInt8:
                _builder.Emit(OpCode.Stind_I1);
                break;
            case SpecialType.Char:
            case SpecialType.Int16:
            case SpecialType.UInt16:
                _builder.Emit(OpCode.Stind_I2);
                break;
            case SpecialType.Int32:
            case SpecialType.UInt32:
                _builder.Emit(OpCode.Stind_I4);
                break;
            case SpecialType.Int:
            case SpecialType.Int64:
            case SpecialType.UInt64:
                _builder.Emit(OpCode.Stind_I8);
                break;
            case SpecialType.IntPtr:
            case SpecialType.UIntPtr:
            case SpecialType.Pointer:
            case SpecialType.FunctionPointer:
                _builder.Emit(OpCode.Stind_I);
                break;
            case SpecialType.Float32:
                _builder.Emit(OpCode.Stind_R4);
                break;
            case SpecialType.Decimal:
            case SpecialType.Float64:
                _builder.Emit(OpCode.Stind_R8);
                break;
            default:
                if (type.IsVerifierReference()) {
                    _builder.Emit(OpCode.Stind_Ref);
                } else {
                    _builder.EmitWithSymbolToken(OpCode.Stobj, type);
                }

                break;
        }
    }

    private void EmitEnumConversion(BoundCastExpression conversion) {
        var fromType = conversion.operand.type;

        if (fromType.IsEnumType())
            fromType = ((NamedTypeSymbol)fromType).enumUnderlyingType;

        var fromPredefTypeKind = fromType.specialType;
        var toType = conversion.type;

        if (toType.IsEnumType())
            toType = ((NamedTypeSymbol)toType).enumUnderlyingType;

        var toPredefTypeKind = toType.specialType;

        EmitNumericConversion(fromPredefTypeKind, toPredefTypeKind);
    }

    private VariableDefinition EmitAssignmentDuplication(
        BoundAssignmentOperator assignmentOperator,
        UseKind useKind,
        bool lhsUsesStack) {
        VariableDefinition temp = null;

        if (useKind != UseKind.Unused) {
            _builder.Emit(OpCode.Dup);

            if (lhsUsesStack) {
                temp = AllocateTemp(
                    assignmentOperator.left.type,
                    assignmentOperator.isRef ? LocalSlotConstraints.ByRef : LocalSlotConstraints.None
                );

                _builder.EmitLocalStore(temp);
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
            case BoundKind.FieldSlotExpression: {
                    var left = (BoundFieldSlotExpression)assignmentTarget;

                    if (left.field.refKind != RefKind.None && !assignmentOperator.isRef) {
                        EmitFieldLoadNoIndirection(left.field, left.receiver, used: true);
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
                        _builder.EmitLoadArgument(ParameterSlot(left.parameter));
                        lhsUsesStack = true;
                    }
                }

                break;
            case BoundKind.StackSlotExpression: {
                    var left = (BoundStackSlotExpression)assignmentTarget;

                    if (left.symbol is DataContainerSymbol dataContainer) {
                        if (dataContainer.refKind != RefKind.None && !assignmentOperator.isRef) {
                            if (!IsStackLocal(dataContainer))
                                _builder.EmitLocalLoad(dataContainer);

                            lhsUsesStack = true;
                        }
                    } else if (left.symbol is ParameterSymbol parameter) {
                        if (parameter.refKind != RefKind.None && !assignmentOperator.isRef) {
                            _builder.EmitLoadArgument(ParameterSlot(parameter));
                            lhsUsesStack = true;
                        }
                    } else {
                        throw ExceptionUtilities.Unreachable();
                    }
                }

                break;
            case BoundKind.FunctionPointerCallExpression: {
                    var left = (BoundFunctionPointerCallExpression)assignmentTarget;
                    EmitCalli(left, UseKind.UsedAsAddress);
                    lhsUsesStack = true;
                }

                break;
            case BoundKind.DataContainerExpression: {
                    var left = (BoundDataContainerExpression)assignmentTarget;

                    if (left.dataContainer.refKind != RefKind.None && !assignmentOperator.isRef) {
                        if (!IsStackLocal(left.dataContainer))
                            _builder.EmitLocalLoad(left.dataContainer);

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
            case BoundKind.PointerIndirectionOperator: {
                    var left = (BoundPointerIndirectionOperator)assignmentTarget;
                    EmitExpression(left.operand, used: true);
                    lhsUsesStack = true;
                }

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

        if (used && !TargetIsNotOnHeap(left))
            return false;

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
            if (_builder.tryNestingLevel != 0)
                return false;

            return true;
        }

        return false;
    }

    private bool TryInPlaceCtorCall(BoundExpression target, BoundObjectCreationExpression objCreation, bool used) {
        var temp = EmitAddress(target, AddressKind.Writeable);

        var constructor = objCreation.constructor;
        EmitArguments(objCreation.arguments, constructor.parameters, objCreation.argumentRefKinds);

        _builder.EmitWithSymbolToken(OpCode.Call, constructor);

        if (used)
            EmitExpression(target, used: true);

        return true;
    }

    private bool SafeToGetWriteableReference(BoundExpression left) {
        if (!HasHome(left, AddressKind.Writeable))
            return false;

        if (left.kind == BoundKind.ArrayAccessExpression &&
            left.StrippedType().typeKind == TypeKind.TemplateParameter &&
            !IsValueType(left.type)) {
            return false;
        }

        return true;
    }

    internal static bool TargetIsNotOnHeap(BoundExpression left) {
        switch (left.kind) {
            case BoundKind.ParameterExpression:
                return ((BoundParameterExpression)left).parameter.refKind == RefKind.None;
            case BoundKind.DataContainerExpression:
                return ((BoundDataContainerExpression)left).dataContainer.refKind == RefKind.None;
        }

        return false;
    }

    private void EmitFieldSlotExpression(BoundFieldSlotExpression expression, bool used) {
        EmitFieldLoad(expression.field, expression.receiver, used);
    }

    private void EmitFieldLoad(BoundFieldAccessExpression expression, bool used) {
        EmitFieldLoad(expression.field, expression.receiver, used);
    }

    private void EmitFieldLoad(FieldSymbol field, BoundExpression receiver, bool used) {
        if (!used) {
            if (field.isCapturedFrame)
                return;

            if (!field.isStatic && receiver.type.IsVerifierValue() && field.refKind == RefKind.None) {
                EmitExpression(receiver, used: false);
                return;
            }
        }

        EmitFieldLoadNoIndirection(field, receiver, used);

        if (field.refKind != RefKind.None)
            EmitLoadIndirect(field.type);

        EmitPopIfUnused(used);
    }

    private void EmitFieldLoadNoIndirection(BoundFieldAccessExpression fieldAccess, bool used) {
        EmitFieldLoadNoIndirection(fieldAccess.field, fieldAccess.receiver, used);
    }

    private void EmitFieldLoadNoIndirection(FieldSymbol field, BoundExpression receiver, bool used) {
        if (field.isStatic) {
            _builder.EmitWithSymbolToken(OpCode.Ldsfld, field);
        } else {
            var fieldType = field.type;

            if (IsValueType(fieldType) && (object)fieldType == (object)receiver.type) {
                EmitExpression(receiver, used);
            } else {
                var temp = EmitFieldLoadReceiver(receiver);

                if (temp is not null)
                    _builder.FreeTemp(temp);

                _builder.EmitWithSymbolToken(OpCode.Ldfld, field);
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
                _builder.EmitWithSymbolToken(OpCode.Unbox, receiver.type);
                return true;
            }
        } else if (receiver.kind == BoundKind.FieldAccessExpression) {
            var fieldAccess = (BoundFieldAccessExpression)receiver;
            var field = fieldAccess.field;

            if (!field.isStatic && EmitFieldLoadReceiverAddress(fieldAccess.receiver)) {
                _builder.EmitWithSymbolToken(OpCode.Ldflda, field);
                return true;
            }
        }

        return false;
    }

    internal static bool FieldLoadMustUseRef(BoundExpression expr) {
        var type = expr.type;

        if (type.IsTemplateParameter())
            return true;

        switch (type.specialType) {
            case SpecialType.Int:
            case SpecialType.Int8:
            case SpecialType.Int16:
            case SpecialType.Int32:
            case SpecialType.Int64:
            case SpecialType.UInt8:
            case SpecialType.UInt16:
            case SpecialType.UInt32:
            case SpecialType.UInt64:
            case SpecialType.Float32:
            case SpecialType.Float64:
            case SpecialType.IntPtr:
            case SpecialType.UIntPtr:
            case SpecialType.Char:
            case SpecialType.Decimal:
            case SpecialType.Bool:
                return true;
        }

        return type.IsEnumType();
    }

    private void EmitParameterLoad(BoundParameterExpression expression) {
        EmitParameterLoad(expression.parameter);
    }

    private void EmitParameterLoad(ParameterSymbol parameter) {
        var slot = ParameterSlot(parameter);
        _builder.EmitLoadArgument(slot);

        if (parameter.refKind != RefKind.None) {
            var parameterType = parameter.type;
            EmitLoadIndirect(parameterType);
        }
    }

    private void EmitStackSlotExpression(BoundStackSlotExpression expression, bool used) {
        var symbol = expression.symbol;

        switch (symbol.kind) {
            case SymbolKind.Local:
                EmitLocalLoad((DataContainerSymbol)symbol, used);
                break;
            case SymbolKind.Parameter:
                if (used)
                    EmitParameterLoad((ParameterSymbol)symbol);

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(symbol.kind);

        }
    }

    private void EmitLocalLoad(BoundDataContainerExpression expression, bool used) {
        EmitLocalLoad(expression.dataContainer, used);
    }

    private void EmitLocalLoad(DataContainerSymbol dataContainer, bool used) {
        var local = dataContainer;
        var isRefLocal = local.refKind != RefKind.None;

        if (IsStackLocal(local)) {
            EmitPopIfUnused(used || isRefLocal);
        } else {
            if (used || isRefLocal) {
                var definition = _builder.GetLocal(local);
                _builder.EmitLocalLoad(definition);
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
        switch (expression.conversion.kind) {
            case ConversionKind.MethodGroup:
                throw ExceptionUtilities.UnexpectedValue(expression.conversion.kind);
            case ConversionKind.ImplicitNullToPointer:
                EmitIntConstant(0);
                _builder.Emit(OpCode.Conv_U);
                EmitPopIfUnused(used);
                return;
        }

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
        if (IsReferenceType(cast.operand.type)) {
            if (cast.type.specialType == SpecialType.Nullable) {
                return;
            } else if (cast.type.specialType == SpecialType.String) {
                _builder.EmitToString(OpCode.Call);
                return;
            }
        }

        if (cast.operand.StrippedType().IsEnumType()) {
            if (cast.type.specialType == SpecialType.String) {
                var type = cast.operand.StrippedType();
                var value = AllocateTemp(type);
                _builder.EmitLocalStore(value);
                _builder.EmitLocalAddress(value);
                _builder.EmitWithSymbolToken(OpCode.Constrained, type);
                _builder.EmitToString(OpCode.Callvirt);
                return;
            }
        }

        var isCastable = cast.operand.type.specialType == SpecialType.String && cast.type.IsPrimitiveType() ||
            cast.type.specialType == SpecialType.String && cast.operand.type.IsPrimitiveType();

        var involvesRefTypes = !isCastable && (cast.operand.type.IsVerifierReference() ||
            (cast.type.IsVerifierReference() && cast.type.specialType != SpecialType.String));

        switch (cast.conversion.kind) {
            case ConversionKind.MethodGroup:
                throw ExceptionUtilities.UnexpectedValue(cast.conversion.kind);
            case ConversionKind.Identity:
                break;
            case ConversionKind.Implicit when involvesRefTypes:
            case ConversionKind.ImplicitReference:
            case ConversionKind.AnyBoxing:
                EmitImplicitReferenceConversion(cast);
                break;
            case ConversionKind.Explicit when involvesRefTypes:
            case ConversionKind.ExplicitReference:
            case ConversionKind.AnyUnboxing:
                EmitExplicitReferenceConversion(cast);
                break;
            case ConversionKind.ImplicitEnum:
            case ConversionKind.ExplicitEnum:
                EmitEnumConversion(cast);
                break;
            case ConversionKind.Implicit:
            case ConversionKind.Explicit:
            case ConversionKind.ImplicitNumeric:
            case ConversionKind.ExplicitNumeric:
                EmitConvertCallOrNumericConversion(cast);
                break;
            case ConversionKind.ImplicitPointerToVoid:
            case ConversionKind.ExplicitPointerToPointer:
                return;
            case ConversionKind.ExplicitPointerToInteger:
            case ConversionKind.ExplicitIntegerToPointer:
                var fromType = cast.operand.type;
                var fromPredefTypeKind = fromType.specialType;

                var toType = cast.type;
                var toPredefTypeKind = toType.specialType;

                EmitNumericConversion(fromPredefTypeKind, toPredefTypeKind);
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

        if (fromPredefTypeKind.IsNumeric() && toPredefTypeKind.IsNumeric())
            EmitNumericConversion(fromPredefTypeKind, toPredefTypeKind);
        else
            _builder.EmitConvertCall(fromPredefTypeKind, toPredefTypeKind);
    }

    internal void EmitLoad(LocalOrParameter localOrParameter) {
        if (localOrParameter.local is { } local)
            _builder.EmitLocalLoad(local);
        else
            _builder.EmitLoadArgument(localOrParameter.parameterIndex);
    }

    internal void EmitNumericConversion(SpecialType from, SpecialType to) {
        // TODO Handle as if checked?
        from = NormalizeNumericType(from);
        to = NormalizeNumericType(to);

        switch (to) {
            case SpecialType.Int8:
                switch (from) {
                    case SpecialType.Int8:
                        break;
                    default:
                        _builder.Emit(OpCode.Conv_I1);
                        break;
                }

                break;
            case SpecialType.UInt8:
                switch (from) {
                    case SpecialType.UInt8:
                        break;
                    default:
                        _builder.Emit(OpCode.Conv_U1);
                        break;
                }

                break;
            case SpecialType.Int16:
                switch (from) {
                    case SpecialType.Int8:
                    case SpecialType.UInt8:
                    case SpecialType.Int16:
                        break;
                    default:
                        _builder.Emit(OpCode.Conv_I2);
                        break;
                }

                break;
            case SpecialType.Char:
            case SpecialType.UInt16:
                switch (from) {
                    case SpecialType.UInt8:
                    case SpecialType.UInt16:
                    case SpecialType.Char:
                        break;
                    default:
                        _builder.Emit(OpCode.Conv_U2);
                        break;
                }

                break;
            case SpecialType.Int32:
                switch (from) {
                    case SpecialType.Int8:
                    case SpecialType.UInt8:
                    case SpecialType.Int16:
                    case SpecialType.UInt16:
                    case SpecialType.Int32:
                    case SpecialType.Char:
                    case SpecialType.UInt32:
                        break;
                    default:
                        _builder.Emit(OpCode.Conv_I4);
                        break;
                }

                break;
            case SpecialType.UInt32:
                switch (from) {
                    case SpecialType.UInt8:
                    case SpecialType.UInt16:
                    case SpecialType.UInt32:
                    case SpecialType.Char:
                    case SpecialType.Int8:
                    case SpecialType.Int16:
                    case SpecialType.Int32:
                        break;
                    default:
                        _builder.Emit(OpCode.Conv_U4);
                        break;
                }

                break;
            case SpecialType.IntPtr:
                switch (from) {
                    case SpecialType.IntPtr:
                    case SpecialType.UIntPtr:
                    case SpecialType.Pointer:
                    case SpecialType.FunctionPointer:
                        break;
                    case SpecialType.Int8:
                    case SpecialType.Int16:
                    case SpecialType.Int32:
                        _builder.Emit(OpCode.Conv_I);
                        break;
                    case SpecialType.UInt8:
                    case SpecialType.UInt16:
                    case SpecialType.Char:
                        _builder.Emit(OpCode.Conv_U);
                        break;
                    case SpecialType.UInt32:
                        _builder.Emit(OpCode.Conv_U);
                        break;
                    default:
                        _builder.Emit(OpCode.Conv_I);
                        break;
                }

                break;
            case SpecialType.UIntPtr:
                switch (from) {
                    case SpecialType.UIntPtr:
                    case SpecialType.IntPtr:
                    case SpecialType.Pointer:
                    case SpecialType.FunctionPointer:
                        break;
                    case SpecialType.UInt8:
                    case SpecialType.UInt16:
                    case SpecialType.UInt32:
                    case SpecialType.Char:
                        _builder.Emit(OpCode.Conv_U);
                        break;
                    case SpecialType.Int8:
                    case SpecialType.Int16:
                    case SpecialType.Int32:
                        _builder.Emit(OpCode.Conv_I);
                        break;
                    default:
                        _builder.Emit(OpCode.Conv_U);
                        break;
                }

                break;
            case SpecialType.Int64:
                switch (from) {
                    case SpecialType.Int64:
                    case SpecialType.UInt64:
                        break;
                    case SpecialType.Int8:
                    case SpecialType.Int16:
                    case SpecialType.Int32:
                    case SpecialType.IntPtr:
                        _builder.Emit(OpCode.Conv_I8);
                        break;
                    case SpecialType.UInt8:
                    case SpecialType.UInt16:
                    case SpecialType.UInt32:
                    case SpecialType.Char:
                        _builder.Emit(OpCode.Conv_U8);
                        break;
                    case SpecialType.Pointer:
                    case SpecialType.FunctionPointer:
                    case SpecialType.UIntPtr:
                        _builder.Emit(OpCode.Conv_U8);
                        break;
                    default:
                        _builder.Emit(OpCode.Conv_I8);
                        break;
                }

                break;
            case SpecialType.UInt64:
                switch (from) {
                    case SpecialType.UInt64:
                    case SpecialType.Int64:
                        break;
                    case SpecialType.UInt8:
                    case SpecialType.UInt16:
                    case SpecialType.UInt32:
                    case SpecialType.Pointer:
                    case SpecialType.FunctionPointer:
                    case SpecialType.UIntPtr:
                    case SpecialType.Char:
                        _builder.Emit(OpCode.Conv_U8);
                        break;
                    case SpecialType.Int8:
                    case SpecialType.Int16:
                    case SpecialType.Int32:
                    case SpecialType.IntPtr:
                        _builder.Emit(OpCode.Conv_I8);
                        break;
                    default:
                        _builder.Emit(OpCode.Conv_U8);
                        break;
                }

                break;
            case SpecialType.Float32:
                switch (from) {
                    case SpecialType.UInt32:
                    case SpecialType.UInt64:
                    case SpecialType.UIntPtr:
                        _builder.Emit(OpCode.Conv_R_Un);
                        break;
                }

                _builder.Emit(OpCode.Conv_R4);
                break;
            case SpecialType.Float64:
                switch (from) {
                    case SpecialType.UInt32:
                    case SpecialType.UInt64:
                    case SpecialType.UIntPtr:
                        _builder.Emit(OpCode.Conv_R_Un);
                        break;
                }

                _builder.Emit(OpCode.Conv_R8);
                break;
            case SpecialType.Pointer:
            case SpecialType.FunctionPointer:
                switch (from) {
                    case SpecialType.UInt8:
                    case SpecialType.UInt16:
                    case SpecialType.UInt32:
                    case SpecialType.UInt64:
                    case SpecialType.Int64:
                        _builder.Emit(OpCode.Conv_U);
                        break;
                    case SpecialType.Int8:
                    case SpecialType.Int16:
                    case SpecialType.Int32:
                        _builder.Emit(OpCode.Conv_I);
                        break;
                    case SpecialType.IntPtr:
                    case SpecialType.UIntPtr:
                        break;
                    default:
                        throw ExceptionUtilities.UnexpectedValue(from);
                }

                break;
            default:
                throw ExceptionUtilities.UnexpectedValue(to);
        }
    }

    internal static SpecialType NormalizeNumericType(SpecialType specialType) {
        if (specialType == SpecialType.Int)
            return SpecialType.Int64;

        if (specialType == SpecialType.Decimal)
            return SpecialType.Float64;

        return specialType;
    }

    private void EmitImplicitReferenceConversion(BoundCastExpression conversion) {
        if (!conversion.operand.type.IsVerifierReference())
            EmitBox(conversion.operand.type);

        var resultType = conversion.type;

        if (!resultType.IsVerifierReference())
            _builder.EmitWithSymbolToken(OpCode.Unbox_Any, resultType);
        else if (resultType.IsArray())
            EmitStaticCast(resultType);
    }

    private void EmitExplicitReferenceConversion(BoundCastExpression conversion) {
        if (!conversion.operand.type.IsVerifierReference())
            EmitBox(conversion.operand.type);

        var resultType = conversion.type;

        if (resultType.IsVerifierReference())
            _builder.EmitWithSymbolToken(OpCode.Castclass, resultType);
        else
            _builder.EmitWithSymbolToken(OpCode.Unbox_Any, resultType);
    }

    private void EmitStaticCast(TypeSymbol to) {
        var temp = AllocateTemp(to);
        _builder.EmitLocalStore(temp);
        _builder.EmitLocalLoad(temp);
        _builder.FreeTemp(temp);
    }

    private void EmitBaseExpression(BoundBaseExpression expression) {
        var thisType = _method.containingType;
        _builder.Emit(OpCode.Ldarg_0);

        if (IsValueType(thisType)) {
            EmitLoadIndirect(thisType);
            EmitBox(thisType);
        }
    }

    private void EmitThisExpression(BoundThisExpression expression) {
        var thisType = expression.type;
        _builder.Emit(OpCode.Ldarg_0);

        if (IsValueType(thisType))
            EmitLoadIndirect(thisType);
    }

    private void EmitLoadIndirect(TypeSymbol type) {
        if (type.IsEnumType())
            type = ((NamedTypeSymbol)type).enumUnderlyingType;

        switch (type.specialType) {
            case SpecialType.Int:
            case SpecialType.Int64:
            case SpecialType.UInt64:
                _builder.Emit(OpCode.Ldind_I8);
                break;
            case SpecialType.Bool:
            case SpecialType.UInt8:
                _builder.Emit(OpCode.Ldind_U1);
                break;
            case SpecialType.Int8:
                _builder.Emit(OpCode.Ldind_I1);
                break;
            case SpecialType.Int16:
                _builder.Emit(OpCode.Ldind_I2);
                break;
            case SpecialType.UInt16:
            case SpecialType.Char:
                _builder.Emit(OpCode.Ldind_U2);
                break;
            case SpecialType.Int32:
                _builder.Emit(OpCode.Ldind_I4);
                break;
            case SpecialType.UInt32:
                _builder.Emit(OpCode.Ldind_U4);
                break;
            case SpecialType.IntPtr:
            case SpecialType.UIntPtr:
            case SpecialType.Pointer:
            case SpecialType.FunctionPointer:
                _builder.Emit(OpCode.Ldind_I);
                break;
            case SpecialType.Decimal:
            case SpecialType.Float64:
                _builder.Emit(OpCode.Ldind_R8);
                break;
            case SpecialType.Float32:
                _builder.Emit(OpCode.Ldind_R4);
                break;
            case SpecialType.None when type is PointerTypeSymbol:
                _builder.Emit(OpCode.Ldind_I);
                break;
            default:
                if (type.IsVerifierReference())
                    _builder.Emit(OpCode.Ldind_Ref);
                else
                    _builder.EmitWithSymbolToken(OpCode.Ldobj, type);

                break;
        }
    }

    private void EmitBox(TypeSymbol type) {
        _builder.EmitWithSymbolToken(OpCode.Box, type);
    }

    private void EmitPopIfUnused(bool used) {
        if (!used)
            _builder.Emit(OpCode.Pop);
    }

    #endregion
}
