
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Type of symbol.
/// </summary>
public enum SymbolKind {
    Global,
    Local,
    NamedType,
    ArrayType,
    ErrorType,
    Method,
    Parameter,
    TemplateParameter,
    Field,
    Label,
    Namespace,
}
