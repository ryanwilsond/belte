namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    internal enum AddressKind : byte {
        Writeable,
        Constrained,
        ReadOnly,
        ReadOnlyStrict,
    }
}
