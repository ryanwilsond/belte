
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Type of type symbol.
/// </summary>
public enum TypeKind : byte {
    Unknown = 0,
    Array,
    Class,
    Struct,
    Enum,
    Primitive,
    TemplateParameter,
    Error,
    Pointer,
    FunctionPointer,
}
