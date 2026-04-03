
namespace Buckle.CodeAnalysis.CodeGeneration;

internal enum OperandKind : byte {
    None = 0,
    Token,
    TypeTok,
    ValueType,
    Class,
    Method,
    Callsitedescr,
    Ctor,
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
