
namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Type of symbol.
/// </summary>
internal enum SymbolKind {
    GlobalVariable,
    LocalVariable,
    Type,
    Function,
    Parameter,
    Field,
}