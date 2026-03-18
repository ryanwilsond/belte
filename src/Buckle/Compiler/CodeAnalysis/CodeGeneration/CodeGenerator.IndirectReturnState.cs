namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class CodeGenerator {
    private enum IndirectReturnState : byte {
        NotNeeded = 0,
        Needed = 1,
        Emitted = 2,
    }
}
