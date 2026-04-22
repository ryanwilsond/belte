using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Libraries;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace Buckle.CodeAnalysis.Emitting;

internal sealed class CecilILBuilder : ILBuilder {
    private const int HiddenLine = 0xFEEFEE;

    private readonly List<(int instructionIndex, object target)> _unhandledGotos;
    private readonly List<(int instructionIndex, object[] targets)> _unhandledSwitches;
    private readonly ILEmitter _module;
    private readonly MethodSymbol _method;
    private readonly MethodDefinition _definition;
    private readonly Stack<object> _tryStack;
    private readonly Dictionary<StringText, Document> _documents;

    private SyntaxTree _lastSeqPointTree;
    private int _initialHiddenSequencePointMarker = -1;

    internal readonly ILProcessor iLProcessor;

    internal CecilILBuilder(MethodSymbol method, ILEmitter module, MethodDefinition definition) {
        _method = method;
        _module = module;
        _definition = definition;
        definition.Body.InitLocals = true;
        iLProcessor = definition.Body.GetILProcessor();
        _unhandledGotos = [];
        _unhandledSwitches = [];
        _documents = [];
        _localSlotManager = new CecilLocalSlotManager();
        _tryStack = new Stack<object>();
    }

    private int _count => iLProcessor.Body.Instructions.Count;

    private CecilLocalSlotManager _localSlotManager { get; }

    internal override LocalSlotManager localSlotManager => _localSlotManager;

    internal override int tryNestingLevel => _tryStack.Count;

    internal override int instructionsEmitted => iLProcessor.Body.Instructions.Count;

    internal override void Finish() {
        foreach (var (instructionIndex, target) in _unhandledGotos) {
            var targetLabel = target;
            var targetInstructionIndex = ((CecilLabelInfo)_labels[targetLabel]).targetInstructionIndex;
            var targetInstruction = iLProcessor.Body.Instructions[targetInstructionIndex];
            var instructionFix = iLProcessor.Body.Instructions[instructionIndex];
            instructionFix.Operand = targetInstruction;
        }

        foreach (var (instructionIndex, targets) in _unhandledSwitches) {
            var builder = ArrayBuilder<Instruction>.GetInstance();

            foreach (var target in targets) {
                var targetLabel = target;
                var targetInstructionIndex = ((CecilLabelInfo)_labels[targetLabel]).targetInstructionIndex;
                var targetInstruction = iLProcessor.Body.Instructions[targetInstructionIndex];
                builder.Add(targetInstruction);
            }

            var instructionFix = iLProcessor.Body.Instructions[instructionIndex];
            instructionFix.Operand = builder.ToArrayAndFree();
        }
    }

    internal override void FreeTemp(CodeGeneration.VariableDefinition temp) {
        // TODO Mono does not handle slot freeing, we would need to do this manually by keeping a stack
        // var cLocal = ((CecilVariableDefinition)temp).variableDefinition;
        // _iLProcessor.Body.Variables.Remove(cLocal);
    }

    internal override void DefineHiddenSequencePoint(int instructionIndex) {
        if (_lastSeqPointTree is not null)
            DefineSequencePoint(_lastSeqPointTree, HiddenLine, HiddenLine, 0, 0, instructionIndex);
    }

    internal override void DefineSequencePoint(SyntaxTree syntaxTree, TextLocation location, int instructionIndex) {
        DefineSequencePoint(
            syntaxTree,
            location.startLine,
            location.endline,
            location.startCharacter,
            location.endCharacter,
            instructionIndex
        );
    }

    private void DefineSequencePoint(
        SyntaxTree syntaxTree,
        int startLine,
        int endLine,
        int startChar,
        int endChar,
        int instructionIndex) {
        if (syntaxTree.text is not StringText t)
            return;

        _lastSeqPointTree = syntaxTree;

        var instruction = iLProcessor.Body.Instructions[instructionIndex];

        if (!_documents.TryGetValue(t, out var document)) {
            var fullPath = Path.GetFullPath(t.fileName);
            document = new Document(fullPath);
            _documents.Add(t, document);
        }

        if (_initialHiddenSequencePointMarker >= 0) {
            var hiddenSequencePoint = new SequencePoint(instruction, document) {
                StartLine = HiddenLine,
                EndLine = HiddenLine,
                StartColumn = 0,
                EndColumn = 0,
            };

            iLProcessor.Body.Method.DebugInformation.SequencePoints.Add(hiddenSequencePoint);
            _initialHiddenSequencePointMarker = -1;
        }

        var sequencePoint = new SequencePoint(instruction, document) {
            StartLine = startLine + 1,
            EndLine = endLine + 1,
            StartColumn = startChar + 1,
            EndColumn = endChar + 1,
        };

        iLProcessor.Body.Method.DebugInformation.SequencePoints.Add(sequencePoint);
    }

    internal override void DefineInitialHiddenSequencePoint() {
        _initialHiddenSequencePointMarker = 0;
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

    internal override void Emit(CodeGeneration.OpCode opCode, float value) {
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

    internal override void EndTry(bool emitEndFinally) {
        throw new NotImplementedException();
    }

    internal override void EmitReturn() {
        iLProcessor.Emit(OpCodes.Ret);
    }

    internal override void EmitCalli(FunctionPointerTypeSymbol type) {
        var managed = type.signature.isManaged;
        var returnType = _module.GetType(type.signature.returnType);
        var paramTypes = type.signature.GetParameterTypes().Select(p => _module.GetType(p.type)).ToArray();

        var callSite = new CallSite(returnType) {
            HasThis = false,
            CallingConvention = managed
                ? MethodCallingConvention.Default
                : GetUnmanagedCallingConvention(type.signature.unmanagedCallingConvention)
        };

        foreach (var p in paramTypes)
            callSite.Parameters.Add(new Mono.Cecil.ParameterDefinition(p));

        iLProcessor.Emit(OpCodes.Calli, callSite);

        MethodCallingConvention GetUnmanagedCallingConvention(CallingConvention callingConvention) {
            return callingConvention switch {
                CallingConvention.Unspecified => MethodCallingConvention.StdCall,
                CallingConvention.Winapi => MethodCallingConvention.Default,
                CallingConvention.StdCall => MethodCallingConvention.StdCall,
                CallingConvention.FastCall => MethodCallingConvention.FastCall,
                CallingConvention.ThisCall => MethodCallingConvention.ThisCall,
                CallingConvention.Cdecl => MethodCallingConvention.C,
                _ => throw ExceptionUtilities.UnexpectedValue(callingConvention)
            };
        }
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
        if (!_localSlotManager.TryGetLocal(local, out var value)) {
            if (local.declaringCompilation.options.isScript) {
                DeclareLocal(
                    local.type,
                    local,
                    local.name,
                    local.synthesizedKind,
                    local.isRef ? LocalSlotConstraints.ByRef : LocalSlotConstraints.None,
                    false
                );

                EmitLocalStore(_localSlotManager.GetLocal(local));
                return;
            } else {
                throw new KeyNotFoundException(local.name);
            }
        }

        EmitLocalStore(value);
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

    internal override void EmitLength(TypeSymbol elementType) {
        iLProcessor.Emit(OpCodes.Call, _module.GetLength(elementType));
    }

    internal override void EmitSizeOf(TypeSymbol elementType) {
        throw new NotImplementedException();
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
        if (from != SpecialType.String && to != SpecialType.String)
            throw ExceptionUtilities.UnexpectedValue((from, to));

        switch (from, to) {
            case (SpecialType.String, SpecialType.Bool):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToBoolean_S);
                break;
            case (SpecialType.String, SpecialType.Int):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToInt32_S);
                break;
            case (SpecialType.String, SpecialType.Decimal):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToDouble_S);
                break;
            case (SpecialType.String, SpecialType.Char):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToChar_S);
                break;
            case (SpecialType.String, SpecialType.UInt8):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToByte_S);
                break;
            case (SpecialType.String, SpecialType.UInt16):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToUInt16_S);
                break;
            case (SpecialType.String, SpecialType.UInt32):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToUInt32_S);
                break;
            case (SpecialType.String, SpecialType.UInt64):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToUInt64_S);
                break;
            case (SpecialType.String, SpecialType.Int8):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToSByte_S);
                break;
            case (SpecialType.String, SpecialType.Int16):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToInt16_S);
                break;
            case (SpecialType.String, SpecialType.Int32):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToInt32_S);
                break;
            case (SpecialType.String, SpecialType.Int64):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToInt64_S);
                break;
            case (SpecialType.String, SpecialType.Float32):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToSingle_S);
                break;
            case (SpecialType.String, SpecialType.Float64):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToDouble_S);
                break;
            case (SpecialType.Bool, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_B);
                break;
            case (SpecialType.Int, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_I64);
                break;
            case (SpecialType.Decimal, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_F64);
                break;
            case (SpecialType.Char, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_C);
                break;
            case (SpecialType.UInt8, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_UI8);
                break;
            case (SpecialType.UInt16, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_UI16);
                break;
            case (SpecialType.UInt32, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_UI32);
                break;
            case (SpecialType.UInt64, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_UI64);
                break;
            case (SpecialType.Int8, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_I8);
                break;
            case (SpecialType.Int16, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_I16);
                break;
            case (SpecialType.Int32, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_I32);
                break;
            case (SpecialType.Int64, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_I64);
                break;
            case (SpecialType.Float32, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_F32);
                break;
            case (SpecialType.Float64, SpecialType.String):
                iLProcessor.Emit(OpCodes.Call, ILEmitter.NetMethodReference.Convert_ToString_F64);
                break;
            default:
                throw ExceptionUtilities.UnexpectedValue((from, to));
        }
    }

    internal override void EmitNewobjNullable(TypeSymbol generic) {
        iLProcessor.Emit(OpCodes.Newobj, _module.GetNullableCtor(generic));
    }

    internal override void EmitNewobjFunc(FunctionTypeSymbol type) {
        iLProcessor.Emit(OpCodes.Newobj, _module.GetFuncCtor(type.signature));
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

    internal override void EmitToString(CodeGeneration.OpCode opCode) {
        iLProcessor.Emit(ConvertToCil(opCode), ILEmitter.NetMethodReference.Object_ToString);
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
        var typeReference = (type.typeKind == TypeKind.FunctionPointer)
            ? _module.GetType(CorLibrary.GetSpecialType(SpecialType.IntPtr))
            : _module.GetType(type, (constraints & LocalSlotConstraints.ByRef) != 0);

        if (symbol.isPinned)
            typeReference = new PinnedType(typeReference);

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
        var typeReference = (type.typeKind == TypeKind.FunctionPointer)
            ? _module.GetType(CorLibrary.GetSpecialType(SpecialType.IntPtr))
            : _module.GetType(type);

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

    internal override void EmitSwitch(object[] labels) {
        _unhandledSwitches.Add((_count, labels));
        iLProcessor.Emit(OpCodes.Switch, labels.Select(l => Instruction.Create(OpCodes.Nop)).ToArray());
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
            CodeGeneration.OpCode.Leave => OpCodes.Leave,
            CodeGeneration.OpCode.Leave_S => OpCodes.Leave_S,
            CodeGeneration.OpCode.Isinst => OpCodes.Isinst,
            CodeGeneration.OpCode.Brtrue => OpCodes.Brtrue,
            CodeGeneration.OpCode.Brtrue_S => OpCodes.Brtrue_S,
            CodeGeneration.OpCode.Brfalse => OpCodes.Brfalse,
            CodeGeneration.OpCode.Brfalse_S => OpCodes.Brfalse_S,
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
            CodeGeneration.OpCode.Stelem => OpCodes.Stelem_Any,
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
            CodeGeneration.OpCode.Ldarg => OpCodes.Ldarg,
            CodeGeneration.OpCode.Ldarga => OpCodes.Ldarga,
            CodeGeneration.OpCode.Ldarg_S => OpCodes.Ldarg_S,
            CodeGeneration.OpCode.Ldarga_S => OpCodes.Ldarga_S,
            CodeGeneration.OpCode.Ldloca => OpCodes.Ldloca,
            CodeGeneration.OpCode.Ldloca_S => OpCodes.Ldloca_S,
            CodeGeneration.OpCode.Stloc => OpCodes.Stloc,
            CodeGeneration.OpCode.Stloc_S => OpCodes.Stloc_S,
            CodeGeneration.OpCode.Conv_Ovf_I_Un => OpCodes.Conv_Ovf_I_Un,
            CodeGeneration.OpCode.Conv_Ovf_I1 => OpCodes.Conv_Ovf_I1,
            CodeGeneration.OpCode.Conv_Ovf_I1_Un => OpCodes.Conv_Ovf_I1_Un,
            CodeGeneration.OpCode.Conv_Ovf_I2 => OpCodes.Conv_Ovf_I2,
            CodeGeneration.OpCode.Conv_Ovf_I2_Un => OpCodes.Conv_Ovf_I2_Un,
            CodeGeneration.OpCode.Conv_Ovf_I4 => OpCodes.Conv_Ovf_I4,
            CodeGeneration.OpCode.Conv_Ovf_I4_Un => OpCodes.Conv_Ovf_I4_Un,
            CodeGeneration.OpCode.Conv_Ovf_I8 => OpCodes.Conv_Ovf_I8,
            CodeGeneration.OpCode.Conv_Ovf_I8_Un => OpCodes.Conv_Ovf_I8_Un,
            CodeGeneration.OpCode.Conv_Ovf_U => OpCodes.Conv_Ovf_U,
            CodeGeneration.OpCode.Conv_Ovf_U_Un => OpCodes.Conv_Ovf_U_Un,
            CodeGeneration.OpCode.Conv_Ovf_U1 => OpCodes.Conv_Ovf_U1,
            CodeGeneration.OpCode.Conv_Ovf_U1_Un => OpCodes.Conv_Ovf_U1_Un,
            CodeGeneration.OpCode.Conv_Ovf_U2 => OpCodes.Conv_Ovf_U2,
            CodeGeneration.OpCode.Conv_Ovf_U2_Un => OpCodes.Conv_Ovf_U2_Un,
            CodeGeneration.OpCode.Conv_Ovf_U4 => OpCodes.Conv_Ovf_U4,
            CodeGeneration.OpCode.Conv_Ovf_U4_Un => OpCodes.Conv_Ovf_U4_Un,
            CodeGeneration.OpCode.Conv_Ovf_U8 => OpCodes.Conv_Ovf_U8,
            CodeGeneration.OpCode.Conv_Ovf_U8_Un => OpCodes.Conv_Ovf_U8_Un,
            CodeGeneration.OpCode.Add_Ovf => OpCodes.Add_Ovf,
            CodeGeneration.OpCode.Add_Ovf_Un => OpCodes.Add_Ovf_Un,
            CodeGeneration.OpCode.Arglist => OpCodes.Arglist,
            CodeGeneration.OpCode.Ckfinite => OpCodes.Ckfinite,
            CodeGeneration.OpCode.Cpblk => OpCodes.Cpblk,
            CodeGeneration.OpCode.Cpobj => OpCodes.Cpobj,
            CodeGeneration.OpCode.Initblk => OpCodes.Initblk,
            CodeGeneration.OpCode.Ldarg_1 => OpCodes.Ldarg_1,
            CodeGeneration.OpCode.Ldarg_2 => OpCodes.Ldarg_2,
            CodeGeneration.OpCode.Ldarg_3 => OpCodes.Ldarg_3,
            CodeGeneration.OpCode.Ldelem_I => OpCodes.Ldelem_I,
            CodeGeneration.OpCode.Ldelem_I1 => OpCodes.Ldelem_I1,
            CodeGeneration.OpCode.Ldelem_I2 => OpCodes.Ldelem_I2,
            CodeGeneration.OpCode.Ldelem_I4 => OpCodes.Ldelem_I4,
            CodeGeneration.OpCode.Ldelem_R4 => OpCodes.Ldelem_R4,
            CodeGeneration.OpCode.Ldelem_U2 => OpCodes.Ldelem_U2,
            CodeGeneration.OpCode.Ldelem_U4 => OpCodes.Ldelem_U4,
            CodeGeneration.OpCode.Ldlen => OpCodes.Ldlen,
            CodeGeneration.OpCode.Ldloc_0 => OpCodes.Ldloc_0,
            CodeGeneration.OpCode.Ldloc_1 => OpCodes.Ldloc_1,
            CodeGeneration.OpCode.Ldloc_2 => OpCodes.Ldloc_2,
            CodeGeneration.OpCode.Ldloc_3 => OpCodes.Ldloc_3,
            CodeGeneration.OpCode.Ldloc_S => OpCodes.Ldloc_S,
            CodeGeneration.OpCode.Ldvirtftn => OpCodes.Ldvirtftn,
            CodeGeneration.OpCode.Mkrefany => OpCodes.Mkrefany,
            CodeGeneration.OpCode.Mul_Ovf => OpCodes.Mul_Ovf,
            CodeGeneration.OpCode.Mul_Ovf_Un => OpCodes.Mul_Ovf_Un,
            CodeGeneration.OpCode.Refanytype => OpCodes.Refanytype,
            CodeGeneration.OpCode.Refanyval => OpCodes.Refanyval,
            CodeGeneration.OpCode.Stelem_I2 => OpCodes.Stelem_I2,
            CodeGeneration.OpCode.Stelem_I4 => OpCodes.Stelem_I4,
            CodeGeneration.OpCode.Stelem_R4 => OpCodes.Stelem_R4,
            CodeGeneration.OpCode.Stloc_0 => OpCodes.Stloc_0,
            CodeGeneration.OpCode.Stloc_1 => OpCodes.Stloc_1,
            CodeGeneration.OpCode.Stloc_2 => OpCodes.Stloc_2,
            CodeGeneration.OpCode.Stloc_3 => OpCodes.Stloc_3,
            CodeGeneration.OpCode.Sub_Ovf => OpCodes.Sub_Ovf,
            CodeGeneration.OpCode.Sub_Ovf_Un => OpCodes.Sub_Ovf_Un,
            CodeGeneration.OpCode.Tail => OpCodes.Tail,
            CodeGeneration.OpCode.Unaligned => OpCodes.Unaligned,
            CodeGeneration.OpCode.Volatile => OpCodes.Volatile,
            CodeGeneration.OpCode.Starg => OpCodes.Starg,
            CodeGeneration.OpCode.Starg_S => OpCodes.Starg_S,
            _ => throw new NotImplementedException()
        };
    }
}
