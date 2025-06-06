
namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class CodeGenerator {
    private enum CallKind : byte {
        Call,
        CallVirt,
        ConstrainedCallVirt,
    }
}
