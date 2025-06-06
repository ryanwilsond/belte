using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.CodeGeneration;

internal abstract class ILBuilder {
    private protected readonly Dictionary<object, LabelInfo> _labels = [];

    internal abstract void Finish();

    internal abstract void MarkLabel(object label);

    internal abstract void Emit(OpCode opCode);

    internal abstract void Emit(OpCode opCode, int value);

    internal abstract void EmitSymbolToken(TypeSymbol type);

    internal abstract void EmitLocalAddress(DataContainerSymbol local);

    internal abstract void EmitLocalLoad(DataContainerSymbol local);

    internal abstract void EmitBranch(OpCode opCode, object label, OpCode revOpCode = OpCode.Nop);
}
