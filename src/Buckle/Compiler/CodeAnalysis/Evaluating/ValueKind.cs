
namespace Buckle.CodeAnalysis.Evaluating;

public enum ValueKind : byte {
    Null,
    Int64,
    Bool,
    Double,
    String,
    HeapPtr,
    Ref,
    Struct
}
