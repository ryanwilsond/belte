
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Special type of symbol, if any.
/// </summary>
internal enum SpecialType : byte {
    None,
    Object,
    Array,
    Any,
    String,
    Bool,
    Char,
    Int,
    Decimal,
    Type,
    Nullable,
    Func,
    Void,
    List,
    Dictionary,
}
