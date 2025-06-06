using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;
using Mono.Cecil.Cil;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CecilILBuilder : ILBuilder {
    private readonly List<(int instructionIndex, object target)> _unhandledGotos;
    private readonly Dictionary<DataContainerSymbol, VariableDefinition> _locals;
    private readonly ILProcessor _iLProcessor;
    private readonly ILEmitter _module;
    private readonly MethodSymbol _method;

    private ArrayBuilder<VariableDefinition> _expressionTemps;
    private VariableDefinition _returnTemp;
    private int _tryNestingLevel;

    internal CecilILBuilder(MethodSymbol method, ILEmitter module, ILProcessor iLProcessor) {
        _method = method;
        _module = module;
        _iLProcessor = iLProcessor;
        _unhandledGotos = [];
        _locals = [];
    }

    private VariableDefinition _lazyReturnTemp {
        get {
            _returnTemp ??= AllocateTemp(_method.returnType);
            return _returnTemp;
        }
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

        _expressionTemps?.Free();
    }

    internal override void Emit(CodeGeneration.OpCode opCode) {
        _iLProcessor.Emit(ConvertToCil(opCode));
    }

    internal override void Emit(CodeGeneration.OpCode opCode, int value) {
        _iLProcessor.Emit(ConvertToCil(opCode), value);
    }

    internal override void EmitSymbolToken(TypeSymbol type) {
        _iLProcessor.Emit(OpCodes.Ldtoken, _module.GetType(type));
    }

    internal override void EmitLocalAddress(DataContainerSymbol local) {
        if (local.isRef)
            EmitLocalLoad(local);
        else
            _iLProcessor.Emit(OpCodes.Ldloca, _locals[local]);
    }

    internal override void EmitLocalLoad(DataContainerSymbol local) {
        _iLProcessor.Emit(OpCodes.Ldloc, _locals[local]);
    }

    private VariableDefinition AllocateTemp(TypeSymbol type) {
        var typeReference = _module.GetType(type);
        var variableDefinition = new VariableDefinition(typeReference);
        _iLProcessor.Body.Variables.Add(variableDefinition);
        return variableDefinition;
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
            _ => throw new NotImplementedException()
        };
    }
}
