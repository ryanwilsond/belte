
namespace Buckle.CodeAnalysis.CodeGeneration;

internal enum OperandKind : byte {
    None = 0,
    Token,
    TypeToken,
    ValueType,
    Class,
    Method,
    FunctionPointer,
    Constructor,
    Field,
    String,
    UInt8,
    UInt16,
    Int8,
    Int32,
    Int64,
    Float32,
    Float64,
}
