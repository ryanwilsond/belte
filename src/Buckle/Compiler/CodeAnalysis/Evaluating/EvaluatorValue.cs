using System.Runtime.InteropServices;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Evaluating;

[StructLayout(LayoutKind.Explicit)]
internal struct EvaluatorValue {
    [FieldOffset(0)]
    internal ValueKind kind;

    [FieldOffset(8)]
    internal long int64;

    [FieldOffset(8)]
    internal double @double;

    [FieldOffset(8)]
    internal bool @bool;

    [FieldOffset(8)]
    internal string @string;

    [FieldOffset(8)]
    internal HeapObject ptr;

    [FieldOffset(8)]
    internal StructInstance @struct;
}

internal sealed class StructInstance {
    internal FieldSymbol[] symbols;
    internal EvaluatorValue[] values;
}

internal sealed class HeapObject {
    internal NamedTypeSymbol type;
    internal EvaluatorValue[] fields;
}

internal enum ValueKind : byte {
    Null,
    Int64,
    Bool,
    Double,
    Reference,
    Struct
}
