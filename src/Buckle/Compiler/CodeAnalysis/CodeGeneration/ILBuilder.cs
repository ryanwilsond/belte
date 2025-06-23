using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal abstract class ILBuilder {
    private protected readonly Dictionary<object, LabelInfo> _labels = [];

    internal abstract void Finish();

    internal abstract void MarkLabel(object label);

    internal abstract void FreeTemp(VariableDefinition temp);

    internal abstract void Emit(OpCode opCode);

    internal abstract void Emit(OpCode opCode, sbyte value);

    internal abstract void Emit(OpCode opCode, int value);

    internal abstract void Emit(OpCode opCode, long value);

    internal abstract void Emit(OpCode opCode, double value);

    internal abstract void Emit(OpCode opCode, string value);

    internal abstract void EmitWithSymbolToken(OpCode opCode, TypeSymbol type);

    internal abstract void EmitWithSymbolToken(OpCode opCode, FieldSymbol type);

    internal abstract void EmitWithSymbolToken(OpCode opCode, MethodSymbol type);

    internal abstract void EmitLocalAddress(DataContainerSymbol local);

    internal abstract void EmitLocalAddress(VariableDefinition local);

    internal abstract void EmitLocalLoad(DataContainerSymbol local);

    internal abstract void EmitLocalLoad(VariableDefinition local);

    internal abstract void EmitLocalStore(DataContainerSymbol local);

    internal abstract void EmitLocalStore(VariableDefinition local);

    internal abstract void EmitBranch(OpCode opCode, object label, OpCode revOpCode = OpCode.Nop);

    internal abstract void EmitLoadArgument(int slot);

    internal abstract void EmitLoadArgumentAddr(int slot);

    internal abstract void EmitStoreArgument(int slot);

    internal abstract void EmitGetTypeFromHandle(TypeSymbol type);

    internal abstract void EmitNullAssert(TypeSymbol type);

    internal abstract void EmitStringConcat2();

    internal abstract void EmitStringEquality();

    internal abstract void EmitConvertCall(SpecialType from, SpecialType to);

    internal abstract void EmitRandomNext();

    internal abstract void EmitNullValue(TypeSymbol generic);

    internal abstract void EmitLdsfldRandom();

    internal abstract void EmitNewobjNullable(TypeSymbol generic);

    internal abstract void EmitThrowNullCondition();

    internal abstract void EmitArrayAddress(ArrayTypeSymbol type);

    internal abstract void EmitArraySet(ArrayTypeSymbol type);

    internal abstract void EmitArrayGet(ArrayTypeSymbol type);

    internal abstract void EmitArrayCreate(ArrayTypeSymbol type);

    internal abstract void EmitToString();

    internal abstract VariableDefinition AllocateTemp(TypeSymbol type, bool isRef);

    internal abstract VariableDefinition GetLocal(DataContainerSymbol local);

    internal abstract void DeclareLocal(DataContainerSymbol local);

    internal abstract ParameterDefinition GetParameter(ParameterSymbol parameter);
}
