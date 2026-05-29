
namespace Buckle.CodeAnalysis.Symbols;

public enum MethodKind : byte {
    Constructor,
    StaticConstructor,
    Destructor,
    Finalizer,
    Ordinary,
    LocalFunction,
    Operator,
    Conversion,
    FunctionPointerSignature,
    FunctionSignature,
    AnonymousFunction,
    Lambda,
    Literal,
}
