
namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class BoundTreeExpander {
    private protected enum UseKind : byte {
        Value,
        StableValue,
        Writable
    }
}
