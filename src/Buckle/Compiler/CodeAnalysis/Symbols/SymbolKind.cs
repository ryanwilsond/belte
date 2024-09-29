
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Type of symbol.
/// </summary>
public enum SymbolKind {
    GlobalVariable,
    LocalVariable,
    NamedType,
    ArrayType,
    Method,
    Parameter,
    Field,
    Label,
    TemplateParameter,
}
