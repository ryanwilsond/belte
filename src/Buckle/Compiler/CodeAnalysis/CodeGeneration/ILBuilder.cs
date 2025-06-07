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

    internal abstract void EmitSymbolToken(TypeSymbol type);

    internal abstract void EmitSymbolToken(FieldSymbol type);

    internal abstract void EmitLocalAddress(DataContainerSymbol local);

    internal abstract void EmitLocalAddress(VariableDefinition local);

    internal abstract void EmitLocalLoad(DataContainerSymbol local);

    internal abstract void EmitLocalLoad(VariableDefinition local);

    internal abstract void EmitLocalStore(VariableDefinition local);

    internal abstract void EmitBranch(OpCode opCode, object label, OpCode revOpCode = OpCode.Nop);

    internal abstract void EmitLoadArgument(int slot);

    internal abstract void EmitLoadArgumentAddr(int slot);

    internal abstract VariableDefinition AllocateTemp(TypeSymbol type);

    internal abstract VariableDefinition GetLocal(DataContainerSymbol local);

    internal abstract ParameterDefinition GetParameter(ParameterSymbol parameter);
}
