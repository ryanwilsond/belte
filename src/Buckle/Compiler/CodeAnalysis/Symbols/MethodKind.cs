
namespace Buckle.CodeAnalysis.Symbols;

internal enum MethodKind : byte {
    Constructor,
    Ordinary,
    Builtin,
    LocalFunction,
}
