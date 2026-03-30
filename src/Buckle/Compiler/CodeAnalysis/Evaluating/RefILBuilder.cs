using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Emit;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class RefILBuilder : ILBuilder {
    private readonly ILGenerator _iLGenerator;
    private readonly Executor _module;
    private readonly MethodSymbol _method;
    private readonly StringWriter _logger;
    private readonly List<Label> _labelCounts;
    private readonly Stack<object> _tryStack;

    private object _epilogue;
    private LocalBuilder _returnLocal;
    private bool _needsEpilogue;

    private int _localCount;

    internal RefILBuilder(MethodSymbol method, Executor module, ILGenerator iLGenerator, StringWriter logger) {
        _method = method;
        _iLGenerator = iLGenerator;
        _module = module;
        _labelCounts = [];
        _tryStack = new Stack<object>();
        _logger = logger;
        _localSlotManager = new RefLocalSlotManager();
    }

    private RefLocalSlotManager _localSlotManager { get; }

    internal override LocalSlotManager localSlotManager => _localSlotManager;

    internal override int tryNestingLevel => _tryStack.Count;

    // ? This doesn't actually need to be accurate because this builder doesn't emit sequence points
    internal override int instructionsEmitted => _iLGenerator.ILOffset;

    internal override void Finish() {
        if (_needsEpilogue) {
            MarkLabel(_epilogue);

            if (!_method.returnsVoid) {
                Log(OpCodes.Ldloc, _returnLocal);
                _iLGenerator.Emit(OpCodes.Ldloc, _returnLocal);
            }

            Emit(CodeGeneration.OpCode.Ret);
        }
    }

    internal override void DefineHiddenSequencePoint(int instructionIndex) { }

    internal override void DefineSequencePoint(SyntaxTree syntaxTree, TextLocation location, int instructionIndex) { }

    internal override void DefineInitialHiddenSequencePoint() { }

    internal override void BeginTry() {
        if (!_needsEpilogue) {
            _needsEpilogue = true;
            _epilogue = new object();
            _returnLocal = ((RefVariableDefinition)AllocateSlot(
                _method.returnType,
                _method.returnsByRef ? LocalSlotConstraints.ByRef : LocalSlotConstraints.None
            )).localBuilder;
        }

        _iLGenerator.BeginExceptionBlock();
        _logger.WriteLine("Try {");
        _tryStack.Push(new object());
    }

    internal override void BeginCatch() {
        EmitBranch(CodeGeneration.OpCode.Leave, _tryStack.Peek());
        _iLGenerator.BeginCatchBlock(typeof(Exception));
        _logger.WriteLine("} Catch {");
    }

    internal override void BeginFinally() {
        EmitBranch(CodeGeneration.OpCode.Leave, _tryStack.Peek());
        _iLGenerator.BeginFinallyBlock();
        _logger.WriteLine("} Finally {");
    }

    internal override void EndTry(bool emitEndFinally) {
        if (emitEndFinally) {
            Log(OpCodes.Endfinally);
            _iLGenerator.Emit(OpCodes.Endfinally);
        }

        _iLGenerator.EndExceptionBlock();
        _logger.WriteLine("} // Try end");
        MarkLabel(_tryStack.Pop());

        if (_tryStack.Count > 0)
            EmitBranch(CodeGeneration.OpCode.Leave, _tryStack.Peek());
        else
            Emit(CodeGeneration.OpCode.Nop);
    }

    internal override void EmitReturn() {
        if (_tryStack.Count == 0) {
            Emit(CodeGeneration.OpCode.Ret);
        } else {
            if (!_method.returnsVoid)
                Emit(OpCodes.Stloc, _returnLocal);

            EmitBranch(CodeGeneration.OpCode.Leave, _epilogue);
        }
    }

    internal override void FreeTemp(VariableDefinition temp) {
        // TODO Reflection does not handle slot freeing, we would need to do this manually by keeping a stack
        // var cLocal = ((RefVariableDefinition)temp).localBuilder;
    }

    internal override void EmitCalli(FunctionPointerTypeSymbol type) {
        var managed = type.signature.isManaged;
        var returnType = _module.GetType(type.signature.returnType);
        var paramTypes = type.signature.GetParameterTypes().Select(p => _module.GetType(p.type)).ToArray();

        if (managed) {
            Log(OpCodes.Calli, type.signature);
            _iLGenerator.EmitCalli(
                OpCodes.Calli,
                System.Reflection.CallingConventions.VarArgs,
                returnType,
                paramTypes,
                Type.EmptyTypes
            );
        } else {
            Log(OpCodes.Calli, type.signature);
            _iLGenerator.EmitCalli(
                OpCodes.Calli,
                System.Runtime.InteropServices.CallingConvention.Winapi,
                returnType,
                paramTypes
            );
        }
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

    internal override void Emit(CodeGeneration.OpCode opCode, float value) {
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
            Emit(OpCodes.Ldloca, _localSlotManager.GetRefLocal(local).localBuilder);
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
        EmitLocalStore(_localSlotManager.GetLocal(local));
    }

    internal override void EmitLocalStore(VariableDefinition local) {
        var cLocal = ((RefVariableDefinition)local).localBuilder;
        Emit(OpCodes.Stloc, cLocal);
    }

    internal override void EmitStoreArgument(int slot) {
        Emit(OpCodes.Starg, slot);
    }

    internal override void EmitLocalLoad(DataContainerSymbol local) {
        Emit(OpCodes.Ldloc, _localSlotManager.GetRefLocal(local).localBuilder);
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

    internal override void EmitSort(TypeSymbol elementType) {
        EmitWithSymbolToken(OpCodes.Call, _module.GetSort(elementType));
    }

    internal override void EmitLength(TypeSymbol elementType) {
        EmitWithSymbolToken(OpCodes.Call, _module.GetLength(elementType));
    }

    internal override void EmitSizeOf(TypeSymbol elementType) {
        EmitWithSymbolToken(OpCodes.Call, _module.GetSizeOf(elementType));
    }

    internal override void EmitStringConcat2() {
        EmitWithSymbolToken(OpCodes.Call, Executor.MethodInfoCache.String_Concat_SS);
    }

    internal override void EmitStringEquality() {
        EmitWithSymbolToken(OpCodes.Call, Executor.MethodInfoCache.String_Equality_SS);
    }

    internal override void EmitStringChars() {
        EmitWithSymbolToken(OpCodes.Call, Executor.MethodInfoCache.String_get_Chars_I);
    }

    internal override void EmitConvertCall(SpecialType from, SpecialType to) {
        var flags = System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static;

        if (from != SpecialType.String && to != SpecialType.String)
            throw ExceptionUtilities.UnexpectedValue((from, to));

        switch (from, to) {
            case (SpecialType.String, SpecialType.Bool):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToBoolean", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.Int):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToInt64", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.Decimal):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToDouble", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.Char):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToChar", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.UInt8):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToByte", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.UInt16):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToUInt16", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.UInt32):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToUInt32", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.UInt64):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToUInt64", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.Int8):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToSByte", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.Int16):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToInt16", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.Int32):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToInt32", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.Int64):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToInt64", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.Float32):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToSingle", flags, [typeof(string)]));
                break;
            case (SpecialType.String, SpecialType.Float64):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToDouble", flags, [typeof(string)]));
                break;
            case (SpecialType.Bool, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(bool)]));
                break;
            case (SpecialType.Int, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(long)]));
                break;
            case (SpecialType.Decimal, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(double)]));
                break;
            case (SpecialType.Char, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(char)]));
                break;
            case (SpecialType.UInt8, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(byte)]));
                break;
            case (SpecialType.UInt16, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(ushort)]));
                break;
            case (SpecialType.UInt32, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(uint)]));
                break;
            case (SpecialType.UInt64, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(ulong)]));
                break;
            case (SpecialType.Int8, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(sbyte)]));
                break;
            case (SpecialType.Int16, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(short)]));
                break;
            case (SpecialType.Int32, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(int)]));
                break;
            case (SpecialType.Int64, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(long)]));
                break;
            case (SpecialType.Float32, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(float)]));
                break;
            case (SpecialType.Float64, SpecialType.String):
                EmitWithSymbolToken(OpCodes.Call, typeof(Convert).GetMethod("ToString", flags, [typeof(double)]));
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue((from, to));
        }
    }

    internal override void EmitNewobjNullable(TypeSymbol generic) {
        EmitWithSymbolToken(OpCodes.Newobj, _module.GetNullableCtor(generic));
    }

    internal override void EmitRandomNextInt64() {
        EmitWithSymbolToken(
            OpCodes.Callvirt,
            typeof(Random).GetMethod(
                "NextInt64",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                [typeof(long)]
            )
        );
    }

    internal override void EmitRandomNextDouble() {
        EmitWithSymbolToken(
            OpCodes.Callvirt,
            typeof(Random).GetMethod(
                "NextDouble",
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance,
                Type.EmptyTypes
            )
        );
    }

    internal override void EmitLdsfldRandom() {
        EmitWithSymbolToken(OpCodes.Ldsfld, _module.randomField);
    }

    internal override void EmitThrowNullCondition() {
        Log(OpCodes.Newobj, Executor.MethodInfoCache.NullConditionException_ctor);
        _iLGenerator.Emit(OpCodes.Newobj, Executor.MethodInfoCache.NullConditionException_ctor);
        Log(OpCodes.Throw);
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

    internal override void EmitToString() {
        Log(OpCodes.Call, Executor.MethodInfoCache.Object_ToString);
        _iLGenerator.Emit(OpCodes.Call, Executor.MethodInfoCache.Object_ToString);
    }

    internal override VariableDefinition GetLocal(DataContainerSymbol local) {
        return _localSlotManager.GetLocal(local);
    }

    internal override VariableDefinition DeclareLocal(
        TypeSymbol type,
        DataContainerSymbol symbol,
        string name,
        SynthesizedLocalKind kind,
        LocalSlotConstraints constraints,
        bool isSlotReusable) {
        var typeBuilder = (type.typeKind == TypeKind.FunctionPointer)
            ? typeof(IntPtr)
            : _module.GetType(type, (constraints & LocalSlotConstraints.ByRef) != 0);

        LogLocal(typeBuilder);
        var localBuilder = _iLGenerator.DeclareLocal(typeBuilder, symbol.isPinned);

        return _localSlotManager.DeclareLocal(localBuilder, type, symbol, name, kind, constraints, isSlotReusable);
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

    internal override VariableDefinition AllocateSlot(
        TypeSymbol type,
        LocalSlotConstraints constraints) {
        var typeBuilder = type.typeKind == TypeKind.FunctionPointer
            ? typeof(IntPtr)
            : _module.GetType(type, (constraints & LocalSlotConstraints.ByRef) != 0);

        LogLocal(typeBuilder);
        var localBuilder = _iLGenerator.DeclareLocal(typeBuilder);

        return _localSlotManager.AllocateSlot(localBuilder, type, constraints);
    }

    private void Log(System.Reflection.Emit.OpCode opCode) {
        _logger.WriteLine($"\t\tIL{_iLGenerator.ILOffset:X4}: {opCode}");
    }

    private void Log(System.Reflection.Emit.OpCode opCode, object value) {
        _logger.WriteLine($"\t\tIL{_iLGenerator.ILOffset:X4}: {opCode} {value}");
    }

    private void LogLocal(Type type) {
        _logger.WriteLine($"\tlocal [{_localCount++}]{type}");
    }

    private void LogMark(Label label) {
        _logger.WriteLine($"\tlabel {_labelCounts.FindIndex(l => l == label)}: IL{_iLGenerator.ILOffset:X4}");
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

    private void Emit(System.Reflection.Emit.OpCode opCode, float value) {
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
            CodeGeneration.OpCode.Leave => OpCodes.Leave,
            CodeGeneration.OpCode.Leave_S => OpCodes.Leave_S,
            CodeGeneration.OpCode.Isinst => OpCodes.Isinst,
            CodeGeneration.OpCode.Brtrue => OpCodes.Brtrue,
            CodeGeneration.OpCode.Brfalse => OpCodes.Brfalse,
            CodeGeneration.OpCode.Ldelema => OpCodes.Ldelema,
            CodeGeneration.OpCode.Ldc_I4 => OpCodes.Ldc_I4,
            CodeGeneration.OpCode.Ldsflda => OpCodes.Ldsflda,
            CodeGeneration.OpCode.Ldflda => OpCodes.Ldflda,
            CodeGeneration.OpCode.Ldfld => OpCodes.Ldfld,
            CodeGeneration.OpCode.Initobj => OpCodes.Initobj,
            CodeGeneration.OpCode.Ldnull => OpCodes.Ldnull,
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
            CodeGeneration.OpCode.Ldc_R4 => OpCodes.Ldc_R4,
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
            CodeGeneration.OpCode.Div_Un => OpCodes.Div_Un,
            CodeGeneration.OpCode.Rem => OpCodes.Rem,
            CodeGeneration.OpCode.Rem_Un => OpCodes.Rem_Un,
            CodeGeneration.OpCode.Shl => OpCodes.Shl,
            CodeGeneration.OpCode.Shr => OpCodes.Shr,
            CodeGeneration.OpCode.Shr_Un => OpCodes.Shr_Un,
            CodeGeneration.OpCode.And => OpCodes.And,
            CodeGeneration.OpCode.Xor => OpCodes.Xor,
            CodeGeneration.OpCode.Or => OpCodes.Or,
            CodeGeneration.OpCode.Neg => OpCodes.Neg,
            CodeGeneration.OpCode.Not => OpCodes.Not,
            CodeGeneration.OpCode.Stobj => OpCodes.Stobj,
            CodeGeneration.OpCode.Stelem_I => OpCodes.Stelem_I,
            CodeGeneration.OpCode.Stelem_I1 => OpCodes.Stelem_I1,
            CodeGeneration.OpCode.Stelem_I8 => OpCodes.Stelem_I8,
            CodeGeneration.OpCode.Stelem_R8 => OpCodes.Stelem_R8,
            CodeGeneration.OpCode.Stelem_Ref => OpCodes.Stelem_Ref,
            CodeGeneration.OpCode.Stelem => OpCodes.Stelem,
            CodeGeneration.OpCode.Stsfld => OpCodes.Stsfld,
            CodeGeneration.OpCode.Stfld => OpCodes.Stfld,
            CodeGeneration.OpCode.Stind_I => OpCodes.Stind_I,
            CodeGeneration.OpCode.Stind_I1 => OpCodes.Stind_I1,
            CodeGeneration.OpCode.Stind_I2 => OpCodes.Stind_I2,
            CodeGeneration.OpCode.Stind_I4 => OpCodes.Stind_I4,
            CodeGeneration.OpCode.Stind_I8 => OpCodes.Stind_I8,
            CodeGeneration.OpCode.Stind_R4 => OpCodes.Stind_R4,
            CodeGeneration.OpCode.Stind_R8 => OpCodes.Stind_R8,
            CodeGeneration.OpCode.Stind_Ref => OpCodes.Stind_Ref,
            CodeGeneration.OpCode.Dup => OpCodes.Dup,
            CodeGeneration.OpCode.Ldsfld => OpCodes.Ldsfld,
            CodeGeneration.OpCode.Unbox => OpCodes.Unbox,
            CodeGeneration.OpCode.Conv_I => OpCodes.Conv_I,
            CodeGeneration.OpCode.Conv_I1 => OpCodes.Conv_I1,
            CodeGeneration.OpCode.Conv_I2 => OpCodes.Conv_I2,
            CodeGeneration.OpCode.Conv_I4 => OpCodes.Conv_I4,
            CodeGeneration.OpCode.Conv_I8 => OpCodes.Conv_I8,
            CodeGeneration.OpCode.Conv_U => OpCodes.Conv_U,
            CodeGeneration.OpCode.Conv_U1 => OpCodes.Conv_U1,
            CodeGeneration.OpCode.Conv_U2 => OpCodes.Conv_U2,
            CodeGeneration.OpCode.Conv_U4 => OpCodes.Conv_U4,
            CodeGeneration.OpCode.Conv_U8 => OpCodes.Conv_U8,
            CodeGeneration.OpCode.Conv_R4 => OpCodes.Conv_R4,
            CodeGeneration.OpCode.Conv_R8 => OpCodes.Conv_R8,
            CodeGeneration.OpCode.Conv_Ovf_I => OpCodes.Conv_Ovf_I,
            CodeGeneration.OpCode.Conv_R_Un => OpCodes.Conv_R_Un,
            CodeGeneration.OpCode.Castclass => OpCodes.Castclass,
            CodeGeneration.OpCode.Ldind_I => OpCodes.Ldind_I,
            CodeGeneration.OpCode.Ldind_U1 => OpCodes.Ldind_U1,
            CodeGeneration.OpCode.Ldind_I1 => OpCodes.Ldind_I1,
            CodeGeneration.OpCode.Ldind_U2 => OpCodes.Ldind_U2,
            CodeGeneration.OpCode.Ldind_I2 => OpCodes.Ldind_I2,
            CodeGeneration.OpCode.Ldind_U4 => OpCodes.Ldind_U4,
            CodeGeneration.OpCode.Ldind_I4 => OpCodes.Ldind_I4,
            CodeGeneration.OpCode.Ldind_I8 => OpCodes.Ldind_I8,
            CodeGeneration.OpCode.Ldind_R4 => OpCodes.Ldind_R4,
            CodeGeneration.OpCode.Ldind_R8 => OpCodes.Ldind_R8,
            CodeGeneration.OpCode.Ldind_Ref => OpCodes.Ldind_Ref,
            CodeGeneration.OpCode.Ldobj => OpCodes.Ldobj,
            CodeGeneration.OpCode.Box => OpCodes.Box,
            CodeGeneration.OpCode.Pop => OpCodes.Pop,
            CodeGeneration.OpCode.Ldtoken => OpCodes.Ldtoken,
            CodeGeneration.OpCode.Throw => OpCodes.Throw,
            CodeGeneration.OpCode.Rethrow => OpCodes.Rethrow,
            CodeGeneration.OpCode.Ldftn => OpCodes.Ldftn,
            CodeGeneration.OpCode.Calli => OpCodes.Calli,
            CodeGeneration.OpCode.Ldloc => OpCodes.Ldloc,
            CodeGeneration.OpCode.Sizeof => OpCodes.Sizeof,
            CodeGeneration.OpCode.Localloc => OpCodes.Localloc,
            _ => throw new NotImplementedException()
        };
    }
}
