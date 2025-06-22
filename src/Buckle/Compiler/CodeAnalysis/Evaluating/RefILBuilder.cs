using System;
using System.Collections.Generic;
using System.Linq;
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
    private readonly bool _log;
    private readonly List<Label> _labelCounts;

    private int _localCount;

    internal RefILBuilder(MethodSymbol method, Executor module, ILGenerator iLGenerator, bool log) {
        _method = method;
        _iLGenerator = iLGenerator;
        _module = module;
        _locals = [];
        _labelCounts = [];
        _log = log;
    }

    internal override void Finish() { }

    internal override void FreeTemp(VariableDefinition temp) {
        // TODO Reflection does not handle slot freeing, we would need to do this manually by keeping a stack
        // var cLocal = ((RefVariableDefinition)temp).localBuilder;
    }

    internal override void Emit(CodeGeneration.OpCode opCode) {
        Emit(ConvertToRef(opCode));
    }

    internal override void Emit(CodeGeneration.OpCode opCode, int value) {
        Emit(ConvertToRef(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, sbyte value) {
        Emit(ConvertToRef(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, long value) {
        Emit(ConvertToRef(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, double value) {
        Emit(ConvertToRef(opCode), value);
    }

    internal override void Emit(CodeGeneration.OpCode opCode, string value) {
        Emit(ConvertToRef(opCode), value);
    }

    internal override void EmitLoadArgument(int slot) {
        switch (slot) {
            case 0: Emit(OpCodes.Ldarg_0); break;
            case 1: Emit(OpCodes.Ldarg_1); break;
            case 2: Emit(OpCodes.Ldarg_2); break;
            case 3: Emit(OpCodes.Ldarg_3); break;
            default:
                if (slot < 0xFF)
                    Emit(OpCodes.Ldarg_S, unchecked((sbyte)slot));
                else
                    Emit(OpCodes.Ldarg, slot);

                break;
        }
    }

    internal override void EmitLoadArgumentAddr(int slot) {
        if (slot < 0xFF)
            Emit(OpCodes.Ldarga_S, unchecked((sbyte)slot));
        else
            Emit(OpCodes.Ldarga, slot);
    }

    internal override void EmitWithSymbolToken(CodeGeneration.OpCode opCode, TypeSymbol type) {
        EmitWithSymbolToken(ConvertToRef(opCode), _module.GetType(type));
    }

    internal override void EmitWithSymbolToken(CodeGeneration.OpCode opCode, FieldSymbol field) {
        EmitWithSymbolToken(ConvertToRef(opCode), _module.GetField(field));
    }

    internal override void EmitWithSymbolToken(CodeGeneration.OpCode opCode, MethodSymbol method) {
        if (method.methodKind == MethodKind.Constructor)
            EmitWithSymbolToken(ConvertToRef(opCode), _module.GetConstructor(method));
        else
            EmitWithSymbolToken(ConvertToRef(opCode), _module.GetMethod(method));
    }

    internal override void EmitLocalAddress(DataContainerSymbol local) {
        if (local.isRef)
            EmitLocalLoad(local);
        else
            Emit(OpCodes.Ldloca, _locals[local].localBuilder);
    }

    internal override void EmitLocalAddress(VariableDefinition local) {
        if (local.isRef) {
            EmitLocalLoad(local);
        } else {
            var cLocal = ((RefVariableDefinition)local).localBuilder;
            Emit(OpCodes.Ldloca, cLocal);
        }
    }

    internal override void EmitLocalStore(DataContainerSymbol local) {
        EmitLocalStore(_locals[local]);
    }

    internal override void EmitLocalStore(VariableDefinition local) {
        var cLocal = ((RefVariableDefinition)local).localBuilder;
        Emit(OpCodes.Stloc, cLocal);
    }

    internal override void EmitStoreArgument(int slot) {
        Emit(OpCodes.Starg, slot);
    }

    internal override void EmitLocalLoad(DataContainerSymbol local) {
        Emit(OpCodes.Ldloc, _locals[local].localBuilder);
    }

    internal override void EmitLocalLoad(VariableDefinition local) {
        var cLocal = ((RefVariableDefinition)local).localBuilder;
        Emit(OpCodes.Ldloc, cLocal);
    }

    internal override void EmitGetTypeFromHandle(TypeSymbol type) {
        EmitWithSymbolToken(OpCodes.Call, Executor.MethodInfoCache.Type_GetTypeFromHandle);
    }

    internal override void EmitNullAssert(TypeSymbol type) {
        EmitWithSymbolToken(OpCodes.Call, _module.GetNullAssert(type));
    }

    internal override void EmitNullValue(TypeSymbol type) {
        EmitWithSymbolToken(OpCodes.Call, _module.GetNullableValue(type));
    }

    internal override void EmitStringConcat2() {
        EmitWithSymbolToken(OpCodes.Call, Executor.MethodInfoCache.String_Concat_SS);
    }

    internal override void EmitStringEquality() {
        EmitWithSymbolToken(OpCodes.Call, Executor.MethodInfoCache.String_Equality_SS);
    }

    internal override void EmitConvertCall(SpecialType from, SpecialType to) {
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;

        switch (from, to) {
            case (SpecialType.String, SpecialType.Bool):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToBoolean", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.Int):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToInt64", flags, [typeof(string)]));
                break;
            case (SpecialType.Decimal, SpecialType.Int):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToInt64", flags, [typeof(double)]));
                break;
            case (SpecialType.String, SpecialType.Decimal):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToDouble", flags, [typeof(string)]));
                break;
            case (SpecialType.Int, SpecialType.Decimal):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToDouble", flags, [typeof(long)]));
                break;
            case (SpecialType.Int, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(long)]));
                break;
            case (SpecialType.Decimal, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(double)]));
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue((from, to));
        }
    }

    internal override void EmitNewobjNullable(TypeSymbol generic) {
        EmitWithSymbolToken(OpCodes.Newobj, _module.GetNullableCtor(generic));
    }

    internal override void EmitRandomNext() {
        EmitWithSymbolToken(
            OpCodes.Callvirt,
            typeof(Random).GetMethod(
                "Next",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                [typeof(int)]
            )
        );
    }

    internal override void EmitLdsfldRandom() {
        EmitWithSymbolToken(OpCodes.Ldsfld, _module.randomField);
    }

    internal override void EmitThrowNullCondition() {
        _iLGenerator.Emit(OpCodes.Newobj, Executor.MethodInfoCache.NullConditionException_ctor);
        _iLGenerator.Emit(OpCodes.Throw);
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

    internal override VariableDefinition GetLocal(DataContainerSymbol local) {
        return _locals[local];
    }

    internal override void DeclareLocal(DataContainerSymbol local) {
        var typeBuilder = _module.GetType(local.type, local.isRef);
        LogLocal(typeBuilder);
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
            _labelCounts.Add(((RefLabelInfo)value).label);
            _labels.Add(label, value);
        }

        var cLabel = ((RefLabelInfo)value).label;
        LogMark(cLabel);
        _iLGenerator.MarkLabel(cLabel);
    }

    internal override void EmitBranch(
        CodeGeneration.OpCode opCode,
        object label,
        CodeGeneration.OpCode revOpCode = CodeGeneration.OpCode.Nop) {
        // var cRevOpCode = ConvertToRef(revOpCode);
        var cOpCode = ConvertToRef(opCode);

        if (!_labels.TryGetValue(label, out var labelInfo)) {
            labelInfo = new RefLabelInfo(_iLGenerator);
            _labelCounts.Add(((RefLabelInfo)labelInfo).label);
            _labels.Add(label, labelInfo);
        }

        EmitWithSymbolToken(cOpCode, ((RefLabelInfo)labelInfo).label);
    }

    internal override VariableDefinition AllocateTemp(TypeSymbol type, bool isRef) {
        var typeBuilder = _module.GetType(type, isRef);
        LogLocal(typeBuilder);
        var localBuilder = _iLGenerator.DeclareLocal(typeBuilder);
        return new RefVariableDefinition(localBuilder, isRef);
    }

    private void Log(System.Reflection.Emit.OpCode opCode) {
        if (_log)
            Console.WriteLine($"\t\tIL{_iLGenerator.ILOffset:X4}: {opCode}");
    }

    private void Log(System.Reflection.Emit.OpCode opCode, object value) {
        if (_log)
            Console.WriteLine($"\t\tIL{_iLGenerator.ILOffset:X4}: {opCode} {value}");
    }

    private void LogLocal(Type type) {
        if (_log)
            Console.WriteLine($"\tlocal [{_localCount++}]{type}");
    }

    private void LogMark(Label label) {
        if (_log)
            Console.WriteLine($"\tlabel {_labelCounts.FindIndex(l => l == label)}: IL{_iLGenerator.ILOffset:X4}");
    }

    private void Emit(System.Reflection.Emit.OpCode opCode, int value) {
        Log(opCode, value);
        _iLGenerator.Emit(opCode, value);
    }

    private void Emit(System.Reflection.Emit.OpCode opCode, sbyte value) {
        Log(opCode, value);
        _iLGenerator.Emit(opCode, value);
    }

    private void Emit(System.Reflection.Emit.OpCode opCode, long value) {
        Log(opCode, value);
        _iLGenerator.Emit(opCode, value);
    }

    private void Emit(System.Reflection.Emit.OpCode opCode, double value) {
        Log(opCode, value);
        _iLGenerator.Emit(opCode, value);
    }

    private void Emit(System.Reflection.Emit.OpCode opCode, string value) {
        Log(opCode, value);
        _iLGenerator.Emit(opCode, value);
    }

    private void Emit(System.Reflection.Emit.OpCode opCode, LocalBuilder builder) {
        Log(opCode, builder);
        _iLGenerator.Emit(opCode, builder);
    }

    private void Emit(System.Reflection.Emit.OpCode opCode) {
        Log(opCode);
        _iLGenerator.Emit(opCode);
    }

    private void EmitWithSymbolToken(System.Reflection.Emit.OpCode opCode, Type type) {
        Log(opCode, type);
        _iLGenerator.Emit(opCode, type);
    }

    private void EmitWithSymbolToken(System.Reflection.Emit.OpCode opCode, System.Reflection.FieldInfo field) {
        Log(opCode, $"{field.DeclaringType.Name}.{field.Name}");
        _iLGenerator.Emit(opCode, field);
    }

    private void EmitWithSymbolToken(System.Reflection.Emit.OpCode opCode, System.Reflection.MethodInfo method) {
        Log(opCode, PrettyPrint(method));
        _iLGenerator.Emit(opCode, method);
    }

    private void EmitWithSymbolToken(System.Reflection.Emit.OpCode opCode, System.Reflection.ConstructorInfo ctor) {
        Log(opCode, PrettyPrint(ctor));
        _iLGenerator.Emit(opCode, ctor);
    }

    private void EmitWithSymbolToken(System.Reflection.Emit.OpCode opCode, Label label) {
        Log(opCode, $"label [{_labelCounts.FindIndex(l => l == label)}]");
        _iLGenerator.Emit(opCode, label);
    }

    private string PrettyPrint(System.Reflection.MethodInfo method) {
        if (!_log)
            return "";

        var preamble = $"{method.ReturnType.Name} {method.DeclaringType.Name}.{method.Name}";

        try {
            if (method.GetParameters().Length == 0)
                return preamble + "()";

            return preamble + $"({string.Join(", ", method.GetParameters().Select(p => p.ParameterType))})";
        } catch (NotSupportedException) {
            return preamble + "(<unresolved-type>)";
        }
    }

    private string PrettyPrint(System.Reflection.ConstructorInfo ctor) {
        if (!_log)
            return "";

        var preamble = $"instance {ctor.DeclaringType.Name}.{ctor.Name}";

        try {
            if (ctor.GetParameters().Length == 0)
                return preamble + "()";

            return preamble + $"({string.Join(", ", ctor.GetParameters().Select(p => p.ParameterType))})";
        } catch (NotSupportedException) {
            return preamble + "(<unresolved-type>)";
        }
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
            CodeGeneration.OpCode.Throw => OpCodes.Throw,
            _ => throw new NotImplementedException()
        };
    }
}
