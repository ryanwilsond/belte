using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CecilILBuilder : ILBuilder {
    private readonly List<(int instructionIndex, object target)> _unhandledGotos;
    private readonly Dictionary<DataContainerSymbol, CecilVariableDefinition> _locals;
    private readonly ILProcessor _iLProcessor;
    private readonly ILEmitter _module;
    private readonly MethodSymbol _method;
    private readonly MethodDefinition _definition;

    internal CecilILBuilder(MethodSymbol method, ILEmitter module, MethodDefinition definition) {
        _method = method;
        _module = module;
        _definition = definition;
        _iLProcessor = definition.Body.GetILProcessor();
        _unhandledGotos = [];
        _locals = [];
    }

    private int _count => _iLProcessor.Body.Instructions.Count;

    internal override void Finish() {
        foreach (var (instructionIndex, target) in _unhandledGotos) {
            var targetLabel = target;
            var targetInstructionIndex = ((CecilLabelInfo)_labels[targetLabel]).targetInstructionIndex;
            var targetInstruction = _iLProcessor.Body.Instructions[targetInstructionIndex];
            var instructionFix = _iLProcessor.Body.Instructions[instructionIndex];
            instructionFix.Operand = targetInstruction;
        }
    }

    internal override void FreeTemp(CodeGeneration.VariableDefinition temp) {
        // TODO Mono does not handle slot freeing, we would need to do this manually by keeping a stack
        // var cLocal = ((CecilVariableDefinition)temp).variableDefinition;
        // _iLProcessor.Body.Variables.Remove(cLocal);
    }

    internal override void Emit(CodeGeneration.OpCode opCode) {
        _iLProcessor.Emit(ConvertToCil(opCode));
    }

    internal override void Emit(CodeGeneration.OpCode opCode, sbyte value) {
        _iLProcessor.Emit(ConvertToCil(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, int value) {
        _iLProcessor.Emit(ConvertToCil(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, long value) {
        _iLProcessor.Emit(ConvertToCil(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, double value) {
        _iLProcessor.Emit(ConvertToCil(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, string value) {
        _iLProcessor.Emit(ConvertToCil(opCode), value);
    }

    internal override void EmitLoadArgument(int slot) {
        _iLProcessor.Emit(OpCodes.Ldarg, _definition.Parameters[slot]);
    }

    internal override void EmitLoadArgumentAddr(int slot) {
        _iLProcessor.Emit(OpCodes.Ldarga, _definition.Parameters[slot]);
    }

    internal override void EmitSymbolToken(TypeSymbol type) {
        _iLProcessor.Emit(OpCodes.Ldtoken, _module.GetType(type));
    }

    internal override void EmitSymbolToken(FieldSymbol field) {
        _iLProcessor.Emit(OpCodes.Ldtoken, _module.GetField(field));
    }

    internal override void EmitLocalAddress(DataContainerSymbol local) {
        if (local.isRef)
            EmitLocalLoad(local);
        else
            _iLProcessor.Emit(OpCodes.Ldloca, _locals[local].variableDefinition);
    }

    internal override void EmitLocalAddress(CodeGeneration.VariableDefinition local) {
        if (local.isRef) {
            EmitLocalLoad(local);
        } else {
            var cLocal = ((CecilVariableDefinition)local).variableDefinition;
            _iLProcessor.Emit(OpCodes.Ldloca, cLocal);
        }
    }

    internal override void EmitLocalStore(CodeGeneration.VariableDefinition local) {
        var cLocal = ((CecilVariableDefinition)local).variableDefinition;
        _iLProcessor.Emit(OpCodes.Stloc, cLocal);
    }

    internal override void EmitLocalLoad(DataContainerSymbol local) {
        _iLProcessor.Emit(OpCodes.Ldloc, _locals[local].variableDefinition);
    }

    internal override void EmitLocalLoad(CodeGeneration.VariableDefinition local) {
        var cLocal = ((CecilVariableDefinition)local).variableDefinition;
        _iLProcessor.Emit(OpCodes.Ldloc, cLocal);
    }

    internal override CodeGeneration.VariableDefinition GetLocal(DataContainerSymbol local) {
        return _locals[local];
    }

    internal override CodeGeneration.ParameterDefinition GetParameter(ParameterSymbol parameter) {
        var slot = parameter.ordinal;

        if (!_method.isStatic)
            slot++;

        return new CecilParameterDefinition(_definition.Parameters[slot]);
    }

    internal override CodeGeneration.VariableDefinition AllocateTemp(TypeSymbol type) {
        var typeReference = _module.GetType(type);
        var variableDefinition = new Mono.Cecil.Cil.VariableDefinition(typeReference);
        _iLProcessor.Body.Variables.Add(variableDefinition);
        return new CecilVariableDefinition(variableDefinition, false);
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
        _iLProcessor.Emit(cOpCode, Instruction.Create(OpCodes.Nop));
    }

    private static Mono.Cecil.Cil.OpCode ConvertToCil(CodeGeneration.OpCode opCode) {
        return opCode switch {
            CodeGeneration.OpCode.Nop => OpCodes.Nop,
            CodeGeneration.OpCode.Br => OpCodes.Br,
            CodeGeneration.OpCode.Clt => OpCodes.Clt,
            CodeGeneration.OpCode.Clt_Un => OpCodes.Clt_Un,
            CodeGeneration.OpCode.Cgt => OpCodes.Cgt,
            CodeGeneration.OpCode.Cgt_Un => OpCodes.Cgt_Un,
            CodeGeneration.OpCode.Blt => OpCodes.Blt,
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
            CodeGeneration.OpCode.Brfalse => OpCodes.Brfalse,
            CodeGeneration.OpCode.Ldelema => OpCodes.Ldelema,
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
            _ => throw new NotImplementedException()
        };
    }
}
