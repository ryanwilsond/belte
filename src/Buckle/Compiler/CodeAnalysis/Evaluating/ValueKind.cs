
namespace Buckle.CodeAnalysis.Evaluating;

public enum ValueKind : byte {
    Null = 0,
    Int64,
    Bool,
    Double,
    Char,
    String,
    Type,
    HeapPtr,
    Ref,
    Struct,
    MethodGroup,
}
