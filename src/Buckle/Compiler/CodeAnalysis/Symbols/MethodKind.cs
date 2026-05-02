
namespace Buckle.CodeAnalysis.Symbols;

public enum MethodKind : byte {
    Constructor,
    StaticConstructor,
    Destructor,
    Ordinary,
    LocalFunction,
    Operator,
    Conversion,
    FunctionPointerSignature,
    FunctionSignature,
    AnonymousFunction,
}
