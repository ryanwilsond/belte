
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Type of symbol.
/// </summary>
public enum SymbolKind : byte {
    Local,
    NamedType,
    ArrayType,
    ErrorType,
    PointerType,
    FunctionPointerType,
    Method,
    Parameter,
    TemplateParameter,
    Field,
    Label,
    Namespace,
    Alias,
    Assembly,
    Module,
}
