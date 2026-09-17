
namespace Buckle.CodeAnalysis.Symbols;

internal enum TypedConstantKind : byte {
    Error = 0,
    Primitive = 1,
    Enum = 2,
    Type = 3,
    Array = 4,
}
