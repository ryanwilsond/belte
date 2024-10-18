
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Type of symbol.
/// </summary>
public enum SymbolKind : byte {
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
