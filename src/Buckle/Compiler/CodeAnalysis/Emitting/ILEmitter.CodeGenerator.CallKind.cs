namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    internal sealed partial class CodeGenerator {
        private enum CallKind : byte {
            Call,
            CallVirt,
            ConstrainedCallVirt,
        }
    }
}
