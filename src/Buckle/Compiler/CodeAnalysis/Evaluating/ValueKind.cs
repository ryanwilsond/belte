
namespace Buckle.CodeAnalysis.Evaluating;

public enum ValueKind : byte {
    Null = 0,
    Int8,
    Int16,
    Int32,
    Int64,
    UInt8,
    UInt16,
    UInt32,
    UInt64,
    Float32,
    Float64,
    Bool,
    Char,
    String,
    Type,
    HeapPtr,
    Ref,
    Struct,
    MethodGroup,
}
