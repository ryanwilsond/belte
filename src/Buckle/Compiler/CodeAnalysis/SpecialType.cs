
namespace Buckle.CodeAnalysis;

/// <summary>
/// Special type of symbol, if any.
/// </summary>
public enum SpecialType : byte {
    None,

    // Cor Types
    Object,
    Array,
    Enum,
    Any,
    String,
    Bool,
    WinBool,
    Char,
    Int,
    Decimal,
    Int8,
    UInt8,
    Int16,
    UInt16,
    Int32,
    UInt32,
    Int64,
    UInt64,
    Float32,
    Float64,
    IntPtr,
    UIntPtr,
    Type,
    Nullable,
    Void,
    ValueType,
    TypedReference,

    // Superficial special types
    Pointer,
    FunctionPointer,
}
