using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class RefILBuilder : ILBuilder {
    private readonly Dictionary<DataContainerSymbol, RefVariableDefinition> _locals;
    private readonly ILGenerator _iLGenerator;
    private readonly Executor _module;
    private readonly MethodSymbol _method;

    internal RefILBuilder(MethodSymbol method, Executor module, ILGenerator iLGenerator) {
        _method = method;
        _iLGenerator = iLGenerator;
        _module = module;
        _locals = [];
    }

    internal override void Finish() { }

    internal override void FreeTemp(VariableDefinition temp) {
        // TODO Reflection does not handle slot freeing, we would need to do this manually by keeping a stack
        // var cLocal = ((RefVariableDefinition)temp).localBuilder;
    }

    internal override void Emit(CodeGeneration.OpCode opCode) {
        _iLGenerator.Emit(ConvertToRef(opCode));
    }

    internal override void Emit(CodeGeneration.OpCode opCode, int value) {
        _iLGenerator.Emit(ConvertToRef(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, sbyte value) {
        _iLGenerator.Emit(ConvertToRef(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, long value) {
        _iLGenerator.Emit(ConvertToRef(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, double value) {
        _iLGenerator.Emit(ConvertToRef(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, string value) {
        _iLGenerator.Emit(ConvertToRef(opCode), value);
    }

    internal override void EmitLoadArgument(int slot) {
        switch (slot) {
            case 0: _iLGenerator.Emit(OpCodes.Ldarg_0); break;
            case 1: _iLGenerator.Emit(OpCodes.Ldarg_1); break;
            case 2: _iLGenerator.Emit(OpCodes.Ldarg_2); break;
            case 3: _iLGenerator.Emit(OpCodes.Ldarg_3); break;
            default:
                if (slot < 0xFF)
                    _iLGenerator.Emit(OpCodes.Ldarg_S, unchecked((sbyte)slot));
                else
                    _iLGenerator.Emit(OpCodes.Ldarg, slot);

                break;
        }
    }

    internal override void EmitLoadArgumentAddr(int slot) {
        if (slot < 0xFF)
            _iLGenerator.Emit(OpCodes.Ldarga_S, unchecked((sbyte)slot));
        else
            _iLGenerator.Emit(OpCodes.Ldarga, slot);
    }

    internal override void EmitSymbolToken(TypeSymbol type) {
        _iLGenerator.Emit(OpCodes.Ldtoken, _module.GetType(type));
    }

    internal override void EmitSymbolToken(FieldSymbol field) {
        _iLGenerator.Emit(OpCodes.Ldtoken, _module.GetField(field));
    }

    internal override void EmitLocalAddress(DataContainerSymbol local) {
        if (local.isRef)
            EmitLocalLoad(local);
        else
            _iLGenerator.Emit(OpCodes.Ldloca, _locals[local].localBuilder);
    }

    internal override void EmitLocalAddress(VariableDefinition local) {
        if (local.isRef) {
            EmitLocalLoad(local);
        } else {
            var cLocal = ((RefVariableDefinition)local).localBuilder;
            _iLGenerator.Emit(OpCodes.Ldloca, cLocal);
        }
    }

    internal override void EmitLocalStore(VariableDefinition local) {
        var cLocal = ((RefVariableDefinition)local).localBuilder;
        _iLGenerator.Emit(OpCodes.Stloc, cLocal);
    }

    internal override void EmitLocalLoad(DataContainerSymbol local) {
        _iLGenerator.Emit(OpCodes.Ldloc, _locals[local].localBuilder);
    }

    internal override void EmitLocalLoad(VariableDefinition local) {
        var cLocal = ((RefVariableDefinition)local).localBuilder;
        _iLGenerator.Emit(OpCodes.Ldloc, cLocal);
    }

    internal override VariableDefinition GetLocal(DataContainerSymbol local) {
        return _locals[local];
    }

    internal override void DeclareLocal(DataContainerSymbol local) {
        var typeBuilder = _module.GetType(local.type);
        var localBuilder = _iLGenerator.DeclareLocal(typeBuilder);
        var mapLocal = new RefVariableDefinition(localBuilder, local.isRef);

        _locals.Add(local, mapLocal);
    }

    internal override ParameterDefinition GetParameter(ParameterSymbol parameter) {
        var slot = parameter.ordinal;

        if (!_method.isStatic)
            slot++;

        return new RefParameterDefinition(slot);
    }

    internal override void MarkLabel(object label) {
        _iLGenerator.MarkLabel(((RefLabelInfo)_labels[label]).label);
    }

    internal override void EmitBranch(
        CodeGeneration.OpCode opCode,
        object label,
        CodeGeneration.OpCode revOpCode = CodeGeneration.OpCode.Nop) {
        // var cRevOpCode = ConvertToRef(revOpCode);
        var cOpCode = ConvertToRef(opCode);

        if (!_labels.TryGetValue(label, out var labelInfo))
            _labels.Add(label, new RefLabelInfo(_iLGenerator));

        _iLGenerator.Emit(cOpCode, ((RefLabelInfo)labelInfo).label);
    }

    internal override VariableDefinition AllocateTemp(TypeSymbol type) {
        var typeBuilder = _module.GetType(type);
        var localBuilder = _iLGenerator.DeclareLocal(typeBuilder);
        return new RefVariableDefinition(localBuilder, false);
    }

    private static System.Reflection.Emit.OpCode ConvertToRef(CodeGeneration.OpCode opCode) {
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
            CodeGeneration.OpCode.Beq => OpCodes.Beq,
            CodeGeneration.OpCode.Bne_Un => OpCodes.Bne_Un,
            CodeGeneration.OpCode.Ret => OpCodes.Ret,
            _ => throw new NotImplementedException()
        };
    }
}
