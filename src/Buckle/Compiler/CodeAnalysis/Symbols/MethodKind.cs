
namespace Buckle.CodeAnalysis.Symbols;

public enum MethodKind : byte {
    Constructor,
    StaticConstructor,
    Ordinary,
    LocalFunction,
    Operator,
    Conversion,
    FunctionPointerSignature,
    FunctionSignature,
    AnonymousFunction,
}
