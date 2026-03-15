
namespace Buckle.CodeAnalysis.Symbols;

internal enum NullableContextKind : byte {
    Unknown = 0,
    None,
    Oblivious,
    NotAnnotated,
    Annotated,
}
