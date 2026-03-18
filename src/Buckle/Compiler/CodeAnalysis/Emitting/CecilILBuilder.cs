using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CecilILBuilder : ILBuilder {
    private readonly List<(int instructionIndex, object target)> _unhandledGotos;
    private readonly ILEmitter _module;
    private readonly MethodSymbol _method;
    private readonly MethodDefinition _definition;
    internal readonly ILProcessor iLProcessor;

    internal CecilILBuilder(MethodSymbol method, ILEmitter module, MethodDefinition definition) {
        _method = method;
        _module = module;
        _definition = definition;
        definition.Body.InitLocals = true;
        iLProcessor = definition.Body.GetILProcessor();
        _unhandledGotos = [];
        _localSlotManager = new CecilLocalSlotManager();
    }

    private int _count => iLProcessor.Body.Instructions.Count;

    private CecilLocalSlotManager _localSlotManager { get; }

    internal override LocalSlotManager localSlotManager => _localSlotManager;

    internal override int tryNestingLevel => throw new NotImplementedException();

    internal override void Finish() {
        foreach (var (instructionIndex, target) in _unhandledGotos) {
            var targetLabel = target;
            var targetInstructionIndex = ((CecilLabelInfo)_labels[targetLabel]).targetInstructionIndex;
            var targetInstruction = iLProcessor.Body.Instructions[targetInstructionIndex];
            var instructionFix = iLProcessor.Body.Instructions[instructionIndex];
            instructionFix.Operand = targetInstruction;
        }
    }

    internal override void FreeTemp(CodeGeneration.VariableDefinition temp) {
        // TODO Mono does not handle slot freeing, we would need to do this manually by keeping a stack
        // var cLocal = ((CecilVariableDefinition)temp).variableDefinition;
        // _iLProcessor.Body.Variables.Remove(cLocal);
    }

    internal override void Emit(CodeGeneration.OpCode opCode) {
        iLProcessor.Emit(ConvertToCil(opCode));
    }

    internal override void Emit(CodeGeneration.OpCode opCode, sbyte value) {
        iLProcessor.Emit(ConvertToCil(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, int value) {
        iLProcessor.Emit(ConvertToCil(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, long value) {
        iLProcessor.Emit(ConvertToCil(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, double value) {
        iLProcessor.Emit(ConvertToCil(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, string value) {
        iLProcessor.Emit(ConvertToCil(opCode), value);
    }

    internal override void EmitLoadArgument(int slot) {
        slot = _definition.HasThis && !_definition.ExplicitThis ? slot - 1 : slot;
        iLProcessor.Emit(OpCodes.Ldarg, _definition.Parameters[slot]);
    }

    internal override void EmitLoadArgumentAddr(int slot) {
        slot = _definition.HasThis && !_definition.ExplicitThis ? slot - 1 : slot;
        iLProcessor.Emit(OpCodes.Ldarga, _definition.Parameters[slot]);
    }

    internal override void EmitWithSymbolToken(CodeGeneration.OpCode opCode, TypeSymbol type) {
        iLProcessor.Emit(ConvertToCil(opCode), _module.GetType(type));
    }

    internal override void EmitWithSymbolToken(CodeGeneration.OpCode opCode, FieldSymbol field) {
        iLProcessor.Emit(ConvertToCil(opCode), _module.GetField(field));
    }

    internal override void EmitWithSymbolToken(CodeGeneration.OpCode opCode, MethodSymbol method) {
        iLProcessor.Emit(ConvertToCil(opCode), _module.GetMethod(method));
    }

    internal override void BeginTry() {
        throw new NotImplementedException();
    }

    internal override void BeginCatch() {
        throw new NotImplementedException();
    }

    internal override void BeginFinally() {
        throw new NotImplementedException();
    }

    internal override void EndTry() {
        throw new NotImplementedException();
    }

    internal override void EmitReturn() {
        throw new NotImplementedException();
    }

    internal override void EmitLocalAddress(DataContainerSymbol local) {
        if (local.isRef)
            EmitLocalLoad(local);
        else
            iLProcessor.Emit(OpCodes.Ldloca, _localSlotManager.GetCecilLocal(local).variableDefinition);
    }

    internal override void EmitLocalAddress(CodeGeneration.VariableDefinition local) {
        if (local.isRef) {
            EmitLocalLoad(local);
        } else {
            var cLocal = ((CecilVariableDefinition)local).variableDefinition;
            iLProcessor.Emit(OpCodes.Ldloca, cLocal);
        }
    }

    internal override void EmitStoreArgument(int slot) {
        slot = _definition.HasThis && !_definition.ExplicitThis ? slot - 1 : slot;
        iLProcessor.Emit(OpCodes.Starg, _definition.Parameters[slot]);
    }

    internal override void EmitLocalStore(DataContainerSymbol local) {
        EmitLocalStore(_localSlotManager.GetLocal(local));
    }

    internal override void EmitLocalStore(CodeGeneration.VariableDefinition local) {
        var cLocal = ((CecilVariableDefinition)local).variableDefinition;
        iLProcessor.Emit(OpCodes.Stloc, cLocal);
    }

    internal override void EmitLocalLoad(DataContainerSymbol local) {
        iLProcessor.Emit(OpCodes.Ldloc, _localSlotManager.GetCecilLocal(local).variableDefinition);
    }

    internal override void EmitLocalLoad(CodeGeneration.VariableDefinition local) {
        var cLocal = ((CecilVariableDefinition)local).variableDefinition;
        iLProcessor.Emit(OpCodes.Ldloc, cLocal);
    }

    internal override void EmitGetTypeFromHandle(TypeSymbol _) {
        iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Type_GetTypeFromHandle);
    }

    internal override void EmitNullAssert(TypeSymbol type) {
        iLProcessor.Emit(OpCodes.Call, _module.GetNullAssert(type));
    }

    internal override void EmitNullValue(TypeSymbol type) {
        iLProcessor.Emit(OpCodes.Call, _module.GetNullableValue(type));
    }

    internal override void EmitSort(TypeSymbol elementType) {
        iLProcessor.Emit(OpCodes.Call, _module.GetSort(elementType));
    }

    internal override void EmitStringConcat2() {
        iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.String_Concat_SS);
    }

    internal override void EmitStringEquality() {
        iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.String_Equality_SS);
    }

    internal override void EmitStringChars() {
        iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.String_get_Chars_I);
    }

    internal override void EmitConvertCall(SpecialType from, SpecialType to) {
        switch (from, to) {
            case (SpecialType.String, SpecialType.Bool):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToBoolean_S);
                break;
            case (SpecialType.String, SpecialType.Int):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToInt64_S);
                break;
            case (SpecialType.Decimal, SpecialType.Int):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToInt64_D);
                break;
            case (SpecialType.String, SpecialType.Decimal):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToDouble_S);
                break;
            case (SpecialType.Int, SpecialType.Decimal):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToDouble_I);
                break;
            case (SpecialType.Int, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_I);
                break;
            case (SpecialType.Decimal, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_D);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue((from, to));
        }
    }

    internal override void EmitNewobjNullable(TypeSymbol generic) {
        iLProcessor.Emit(OpCodes.Newobj, _module.GetNullableCtor(generic));
    }

    internal override void EmitRandomNextInt64() {
        iLProcessor.Emit(OpCodes.Callvirt, ILEmitter.NetMethodReference.Random_NextInt64_I);
    }

    internal override void EmitRandomNextDouble() {
        iLProcessor.Emit(OpCodes.Callvirt, ILEmitter.NetMethodReference.Random_NextDouble);
    }

    internal override void EmitLdsfldRandom() {
        iLProcessor.Emit(OpCodes.Ldsfld, _module.randomField);
    }

    internal override void EmitThrowNullCondition() {
        iLProcessor.Emit(OpCodes.Newobj, ILEmitter.NetMethodReference.NullConditionException_ctor);
        iLProcessor.Emit(OpCodes.Throw);
    }

    internal override void EmitArrayAddress(ArrayTypeSymbol type) {
        throw new NotImplementedException();
    }

    internal override void EmitArrayCreate(ArrayTypeSymbol type) {
        throw new NotImplementedException();
    }

    internal override void EmitArrayGet(ArrayTypeSymbol type) {
        throw new NotImplementedException();
    }

    internal override void EmitArraySet(ArrayTypeSymbol type) {
        throw new NotImplementedException();
    }

    internal override void EmitToString() {
        iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Object_ToString);
    }

    internal override CodeGeneration.VariableDefinition GetLocal(DataContainerSymbol local) {
        return _localSlotManager.GetLocal(local);
    }

    internal override CodeGeneration.ParameterDefinition GetParameter(ParameterSymbol parameter) {
        // TODO ? Cecil adjusts for this parameter behind the scenes?
        // var slot = parameter.ordinal;

        // if (!_method.isStatic)
        //     slot++;

        return new CecilParameterDefinition(_definition.Parameters[parameter.ordinal]);
    }

    internal override CodeGeneration.VariableDefinition DeclareLocal(
        TypeSymbol type,
        DataContainerSymbol symbol,
        string name,
        SynthesizedLocalKind kind,
        LocalSlotConstraints constraints,
        bool isSlotReusable) {
        var typeReference = _module.GetType(type, (constraints & LocalSlotConstraints.ByRef) != 0);
        var variableDefinition = new Mono.Cecil.Cil.VariableDefinition(typeReference);
        iLProcessor.Body.Variables.Add(variableDefinition);

        return _localSlotManager.DeclareLocal(
            variableDefinition,
            type,
            symbol,
            name,
            kind,
            constraints,
            isSlotReusable
        );
    }

    internal override CodeGeneration.VariableDefinition AllocateSlot(
        TypeSymbol type,
        LocalSlotConstraints constraints) {
        var typeReference = _module.GetType(type);
        var variableDefinition = new Mono.Cecil.Cil.VariableDefinition(typeReference);
        iLProcessor.Body.Variables.Add(variableDefinition);
        return _localSlotManager.AllocateSlot(variableDefinition, type, constraints);
    }

    internal CodeGeneration.VariableDefinition AllocateSlot(
        TypeReference type,
        LocalSlotConstraints constraints) {
        var variableDefinition = new Mono.Cecil.Cil.VariableDefinition(type);
        iLProcessor.Body.Variables.Add(variableDefinition);
        return _localSlotManager.AllocateSlot(variableDefinition, null, constraints);
    }

    internal override void MarkLabel(object label) {
        _labels.Add(label, new CecilLabelInfo(_count));
    }

    internal override void EmitBranch(
        CodeGeneration.OpCode opCode,
        object label,
        CodeGeneration.OpCode revOpCode = CodeGeneration.OpCode.Nop) {
        // var cRevOpCode = ConvertToCil(revOpCode);
        var cOpCode = ConvertToCil(opCode);
        _unhandledGotos.Add((_count, label));
        iLProcessor.Emit(cOpCode, Instruction.Create(OpCodes.Nop));
    }

    private static Mono.Cecil.Cil.OpCode ConvertToCil(CodeGeneration.OpCode opCode) {
        return opCode switch {
            CodeGeneration.OpCode.Nop => OpCodes.Nop,
            CodeGeneration.OpCode.Br => OpCodes.Br,
            CodeGeneration.OpCode.Br_S => OpCodes.Br_S,
            CodeGeneration.OpCode.Clt => OpCodes.Clt,
            CodeGeneration.OpCode.Clt_Un => OpCodes.Clt_Un,
            CodeGeneration.OpCode.Cgt => OpCodes.Cgt,
            CodeGeneration.OpCode.Cgt_Un => OpCodes.Cgt_Un,
            CodeGeneration.OpCode.Blt => OpCodes.Blt,
            CodeGeneration.OpCode.Blt_S => OpCodes.Blt_S,
            CodeGeneration.OpCode.Bge => OpCodes.Bge,
            CodeGeneration.OpCode.Blt_Un => OpCodes.Blt_Un,
            CodeGeneration.OpCode.Bge_Un => OpCodes.Bge_Un,
            CodeGeneration.OpCode.Ble_Un => OpCodes.Ble_Un,
            CodeGeneration.OpCode.Bgt => OpCodes.Bgt,
            CodeGeneration.OpCode.Ble => OpCodes.Ble,
            CodeGeneration.OpCode.Bgt_Un => OpCodes.Bgt_Un,
            CodeGeneration.OpCode.Readonly => OpCodes.Readonly,
            CodeGeneration.OpCode.Isinst => OpCodes.Isinst,
            CodeGeneration.OpCode.Brtrue => OpCodes.Brtrue,
            CodeGeneration.OpCode.Brtrue_S => OpCodes.Brtrue_S,
            CodeGeneration.OpCode.Brfalse => OpCodes.Brfalse,
            CodeGeneration.OpCode.Brfalse_S => OpCodes.Brfalse_S,
            CodeGeneration.OpCode.Ldelema => OpCodes.Ldelema,
            CodeGeneration.OpCode.Leave => OpCodes.Leave,
            CodeGeneration.OpCode.Leave_S => OpCodes.Leave_S,
            CodeGeneration.OpCode.Ldc_I4 => OpCodes.Ldc_I4,
            CodeGeneration.OpCode.Conv_Ovf_I => OpCodes.Conv_Ovf_I,
            CodeGeneration.OpCode.Ldsflda => OpCodes.Ldsflda,
            CodeGeneration.OpCode.Ldflda => OpCodes.Ldflda,
            CodeGeneration.OpCode.Ldfld => OpCodes.Ldfld,
            CodeGeneration.OpCode.Initobj => OpCodes.Initobj,
            CodeGeneration.OpCode.Ldnull => OpCodes.Ldnull,
            CodeGeneration.OpCode.Conv_I8 => OpCodes.Conv_I8,
            CodeGeneration.OpCode.Conv_U8 => OpCodes.Conv_U8,
            CodeGeneration.OpCode.Ldc_I8 => OpCodes.Ldc_I8,
            CodeGeneration.OpCode.Ldc_I4_S => OpCodes.Ldc_I4_S,
            CodeGeneration.OpCode.Ldc_I4_M1 => OpCodes.Ldc_I4_M1,
            CodeGeneration.OpCode.Ldc_I4_0 => OpCodes.Ldc_I4_0,
            CodeGeneration.OpCode.Ldc_I4_1 => OpCodes.Ldc_I4_1,
            CodeGeneration.OpCode.Ldc_I4_2 => OpCodes.Ldc_I4_2,
            CodeGeneration.OpCode.Ldc_I4_3 => OpCodes.Ldc_I4_3,
            CodeGeneration.OpCode.Ldc_I4_4 => OpCodes.Ldc_I4_4,
            CodeGeneration.OpCode.Ldc_I4_5 => OpCodes.Ldc_I4_5,
            CodeGeneration.OpCode.Ldc_I4_6 => OpCodes.Ldc_I4_6,
            CodeGeneration.OpCode.Ldc_I4_7 => OpCodes.Ldc_I4_7,
            CodeGeneration.OpCode.Ldc_I4_8 => OpCodes.Ldc_I4_8,
            CodeGeneration.OpCode.Ldc_R8 => OpCodes.Ldc_R8,
            CodeGeneration.OpCode.Ldstr => OpCodes.Ldstr,
            CodeGeneration.OpCode.Beq => OpCodes.Beq,
            CodeGeneration.OpCode.Bne_Un => OpCodes.Bne_Un,
            CodeGeneration.OpCode.Ret => OpCodes.Ret,
            CodeGeneration.OpCode.Ldarg_0 => OpCodes.Ldarg_0,
            CodeGeneration.OpCode.Ldelem_I8 => OpCodes.Ldelem_I8,
            CodeGeneration.OpCode.Ldelem_U1 => OpCodes.Ldelem_U1,
            CodeGeneration.OpCode.Ldelem_R8 => OpCodes.Ldelem_R8,
            CodeGeneration.OpCode.Ldelem_Ref => OpCodes.Ldelem_Ref,
            CodeGeneration.OpCode.Ldelem => OpCodes.Ldelem_Any,
            CodeGeneration.OpCode.Newarr => OpCodes.Newarr,
            CodeGeneration.OpCode.Newobj => OpCodes.Newobj,
            CodeGeneration.OpCode.Call => OpCodes.Call,
            CodeGeneration.OpCode.Constrained => OpCodes.Constrained,
            CodeGeneration.OpCode.Callvirt => OpCodes.Callvirt,
            CodeGeneration.OpCode.Ceq => OpCodes.Ceq,
            CodeGeneration.OpCode.Unbox_Any => OpCodes.Unbox_Any,
            CodeGeneration.OpCode.Mul => OpCodes.Mul,
            CodeGeneration.OpCode.Add => OpCodes.Add,
            CodeGeneration.OpCode.Sub => OpCodes.Sub,
            CodeGeneration.OpCode.Div => OpCodes.Div,
            CodeGeneration.OpCode.Rem => OpCodes.Rem,
            CodeGeneration.OpCode.Shl => OpCodes.Shl,
            CodeGeneration.OpCode.Shr => OpCodes.Shr,
            CodeGeneration.OpCode.Shr_Un => OpCodes.Shr_Un,
            CodeGeneration.OpCode.And => OpCodes.And,
            CodeGeneration.OpCode.Xor => OpCodes.Xor,
            CodeGeneration.OpCode.Or => OpCodes.Or,
            CodeGeneration.OpCode.Neg => OpCodes.Neg,
            CodeGeneration.OpCode.Not => OpCodes.Not,
            CodeGeneration.OpCode.Stobj => OpCodes.Stobj,
            CodeGeneration.OpCode.Stelem_I1 => OpCodes.Stelem_I1,
            CodeGeneration.OpCode.Stelem_I8 => OpCodes.Stelem_I8,
            CodeGeneration.OpCode.Stelem_R8 => OpCodes.Stelem_R8,
            CodeGeneration.OpCode.Stelem_Ref => OpCodes.Stelem_Ref,
            CodeGeneration.OpCode.Stelem => OpCodes.Stelem_Any,
            CodeGeneration.OpCode.Stsfld => OpCodes.Stsfld,
            CodeGeneration.OpCode.Stfld => OpCodes.Stfld,
            CodeGeneration.OpCode.Stind_I1 => OpCodes.Stind_I1,
            CodeGeneration.OpCode.Stind_I8 => OpCodes.Stind_I8,
            CodeGeneration.OpCode.Stind_R8 => OpCodes.Stind_R8,
            CodeGeneration.OpCode.Stind_Ref => OpCodes.Stind_Ref,
            CodeGeneration.OpCode.Dup => OpCodes.Dup,
            CodeGeneration.OpCode.Ldsfld => OpCodes.Ldsfld,
            CodeGeneration.OpCode.Unbox => OpCodes.Unbox,
            CodeGeneration.OpCode.Conv_R8 => OpCodes.Conv_R8,
            CodeGeneration.OpCode.Conv_U2 => OpCodes.Conv_U2,
            CodeGeneration.OpCode.Castclass => OpCodes.Castclass,
            CodeGeneration.OpCode.Ldind_I8 => OpCodes.Ldind_I8,
            CodeGeneration.OpCode.Ldind_I1 => OpCodes.Ldind_I1,
            CodeGeneration.OpCode.Ldind_R8 => OpCodes.Ldind_R8,
            CodeGeneration.OpCode.Ldind_Ref => OpCodes.Ldind_Ref,
            CodeGeneration.OpCode.Ldobj => OpCodes.Ldobj,
            CodeGeneration.OpCode.Box => OpCodes.Box,
            CodeGeneration.OpCode.Pop => OpCodes.Pop,
            CodeGeneration.OpCode.Ldtoken => OpCodes.Ldtoken,
            CodeGeneration.OpCode.Conv_I4 => OpCodes.Conv_I4,
            CodeGeneration.OpCode.Throw => OpCodes.Throw,
            CodeGeneration.OpCode.Rethrow => OpCodes.Rethrow,
            _ => throw new NotImplementedException()
        };
    }
}
