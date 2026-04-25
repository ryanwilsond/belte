
namespace Buckle.CodeAnalysis.Symbols;

public enum RefKind : byte {
    None,
    Ref,
    Out,
    RefConst,
    RefConstParameter,
}
