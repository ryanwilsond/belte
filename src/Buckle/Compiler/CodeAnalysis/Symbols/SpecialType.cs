
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Special type of symbol, if any.
/// </summary>
internal enum SpecialType {
    None,
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
}
