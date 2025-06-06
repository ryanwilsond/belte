using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using Buckle.CodeAnalysis.CodeGeneration;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Evaluating;

internal sealed class RefILBuilder : ILBuilder {
    private readonly Dictionary<DataContainerSymbol, LocalBuilder> _locals;
    private readonly ILGenerator _iLGenerator;
    private readonly Executor _module;
    private readonly MethodSymbol _method;

    private ArrayBuilder<LocalBuilder> _expressionTemps;
    private LocalBuilder _returnTemp;
    private int _tryNestingLevel;

    internal RefILBuilder(MethodSymbol method, Executor module, ILGenerator iLGenerator) {
        _method = method;
        _iLGenerator = iLGenerator;
        _module = module;
        _locals = [];
    }

    private LocalBuilder _lazyReturnTemp {
        get {
            _returnTemp ??= AllocateTemp(_method.returnType);
            return _returnTemp;
        }
    }

    internal override void Finish() {
        _expressionTemps?.Free();
    }

    internal override void Emit(CodeGeneration.OpCode opCode) {
        _iLGenerator.Emit(ConvertToRef(opCode));
    }

    internal override void Emit(CodeGeneration.OpCode opCode, int value) {
        _iLGenerator.Emit(ConvertToRef(opCode), value);
    }

    internal override void EmitSymbolToken(TypeSymbol type) {
        _iLGenerator.Emit(OpCodes.Ldtoken, _module.GetType(type));
    }

    internal override void EmitLocalAddress(DataContainerSymbol local) {
        if (local.isRef)
            EmitLocalLoad(local);
        else
            _iLGenerator.Emit(OpCodes.Ldloca, _locals[local]);
    }

    internal override void EmitLocalLoad(DataContainerSymbol local) {
        _iLGenerator.Emit(OpCodes.Ldloc, _locals[local]);
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

    private LocalBuilder AllocateTemp(TypeSymbol type) {
        var typeBuilder = _module.GetType(type);
        return _iLGenerator.DeclareLocal(typeBuilder);
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
            _ => throw new NotImplementedException()
        };
    }
}
