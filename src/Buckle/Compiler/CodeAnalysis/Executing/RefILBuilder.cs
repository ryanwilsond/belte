using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

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

    internal override void EmitWithSymbolToken(CodeGeneration.OpCode opCode, TypeSymbol type) {
        _iLGenerator.Emit(ConvertToRef(opCode), _module.GetType(type));
    }

    internal override void EmitWithSymbolToken(CodeGeneration.OpCode opCode, FieldSymbol field) {
        _iLGenerator.Emit(ConvertToRef(opCode), _module.GetField(field));
    }

    internal override void EmitWithSymbolToken(CodeGeneration.OpCode opCode, MethodSymbol method) {
        if (method.methodKind == MethodKind.Constructor)
            _iLGenerator.Emit(ConvertToRef(opCode), _module.GetConstructor(method));
        else
            _iLGenerator.Emit(ConvertToRef(opCode), _module.GetMethod(method));
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

    internal override void EmitLocalStore(DataContainerSymbol local) {
        EmitLocalStore(_locals[local]);
    }

    internal override void EmitLocalStore(VariableDefinition local) {
        var cLocal = ((RefVariableDefinition)local).localBuilder;
        _iLGenerator.Emit(OpCodes.Stloc, cLocal);
    }

    internal override void EmitStoreArgument(int slot) {
        _iLGenerator.Emit(OpCodes.Starg, slot);
    }

    internal override void EmitLocalLoad(DataContainerSymbol local) {
        _iLGenerator.Emit(OpCodes.Ldloc, _locals[local].localBuilder);
    }

    internal override void EmitLocalLoad(VariableDefinition local) {
        var cLocal = ((RefVariableDefinition)local).localBuilder;
        _iLGenerator.Emit(OpCodes.Ldloc, cLocal);
    }

    internal override void EmitGetTypeFromHandle(TypeSymbol type) {
        var getTypeFromHandle = _module.GetType(type).GetMethod(
            "GetTypeFromHandle",
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
            null,
            [typeof(RuntimeTypeHandle)],
            null
        );

        _iLGenerator.Emit(OpCodes.Call, getTypeFromHandle);
    }

    internal override void EmitNullAssert(TypeSymbol type) {
        _iLGenerator.Emit(OpCodes.Call, _module.GetNullAssert(type));
    }

    internal override void EmitStringConcat2() {
        _iLGenerator.Emit(OpCodes.Call, Executor.NetMethodInfo.String_Concat_SS);
    }

    internal override void EmitStringEquality() {
        _iLGenerator.Emit(OpCodes.Call, Executor.NetMethodInfo.String_Equality_SS);
    }

    internal override void EmitConvertCall(SpecialType from, SpecialType to) {
        switch (from, to) {
            case (SpecialType.String, SpecialType.Bool):
                _iLGenerator.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToBoolean", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.Int):
                _iLGenerator.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToInt64", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, [typeof(string)]));
                break;
            case (SpecialType.Decimal, SpecialType.Int):
                _iLGenerator.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToInt64", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, [typeof(double)]));
                break;
            case (SpecialType.String, SpecialType.Decimal):
                _iLGenerator.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToDouble", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, [typeof(string)]));
                break;
            case (SpecialType.Int, SpecialType.Decimal):
                _iLGenerator.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToDouble", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, [typeof(long)]));
                break;
            case (SpecialType.Int, SpecialType.String):
                _iLGenerator.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, [typeof(long)]));
                break;
            case (SpecialType.Decimal, SpecialType.String):
                _iLGenerator.Emit(OpCodes.Call, typeof(Convert).GetMethod("ToString", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static, [typeof(double)]));
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue((from, to));
        }
    }

    internal override void EmitNewobjNullable(TypeSymbol generic) {
        _iLGenerator.Emit(OpCodes.Newobj, _module.GetNullableCtor(generic));
    }

    internal override void EmitRandomNext() {
        _iLGenerator.Emit(OpCodes.Callvirt, typeof(Random).GetMethod("Next", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance, [typeof(int)]));
    }

    internal override void EmitLdsfldRandom() {
        _iLGenerator.Emit(OpCodes.Ldsfld, _module.randomField);
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
        if (!_labels.TryGetValue(label, out var value)) {
            value = new RefLabelInfo(_iLGenerator);
            _labels.Add(label, value);
        }

        _iLGenerator.MarkLabel(((RefLabelInfo)value).label);
    }

    internal override void EmitBranch(
        CodeGeneration.OpCode opCode,
        object label,
        CodeGeneration.OpCode revOpCode = CodeGeneration.OpCode.Nop) {
        // var cRevOpCode = ConvertToRef(revOpCode);
        var cOpCode = ConvertToRef(opCode);

        if (!_labels.TryGetValue(label, out var labelInfo)) {
            labelInfo = new RefLabelInfo(_iLGenerator);
            _labels.Add(label, labelInfo);
        }

        _iLGenerator.Emit(cOpCode, ((RefLabelInfo)labelInfo).label);
    }

    internal override VariableDefinition AllocateTemp(TypeSymbol type, bool isRef) {
        var typeBuilder = _module.GetType(type);
        var localBuilder = _iLGenerator.DeclareLocal(typeBuilder);
        return new RefVariableDefinition(localBuilder, isRef);
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
            CodeGeneration.OpCode.Ldarg_0 => OpCodes.Ldarg_0,
            CodeGeneration.OpCode.Ldelem_I8 => OpCodes.Ldelem_I8,
            CodeGeneration.OpCode.Ldelem_U1 => OpCodes.Ldelem_U1,
            CodeGeneration.OpCode.Ldelem_R8 => OpCodes.Ldelem_R8,
            CodeGeneration.OpCode.Ldelem_Ref => OpCodes.Ldelem_Ref,
            CodeGeneration.OpCode.Ldelem => OpCodes.Ldelem,
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
            CodeGeneration.OpCode.Stelem => OpCodes.Stelem,
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
            _ => throw new NotImplementedException()
        };
    }
}
