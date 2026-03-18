
namespace Buckle.CodeAnalysis.Symbols;

public enum MethodKind : byte {
    Constructor,
    Ordinary,
    Builtin,
    LocalFunction,
    Operator,
    StaticConstructor,
}
