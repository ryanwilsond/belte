
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Type of type symbol.
/// </summary>
internal enum TypeKind : byte {
    Array,
    Class,
    Struct,
    Primitive,
    TemplateParameter,
    Error,
}
