
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Type of type symbol.
/// </summary>
internal enum TypeKind {
    Array,
    Class,
    Struct,
    Primitive,
    TemplateParameter,
    Error,
}
