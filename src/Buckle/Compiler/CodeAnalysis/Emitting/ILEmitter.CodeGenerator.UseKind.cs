namespace Buckle.CodeAnalysis.Emitting;

internal sealed partial class ILEmitter {
    internal sealed partial class CodeGenerator {
        private enum UseKind : byte {
            Unused,
            UsedAsValue,
            UsedAsAddress,
        }
    }
}
