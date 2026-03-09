
namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class CodeGenerator {
    internal enum CallKind : byte {
        Call,
        CallVirt,
        ConstrainedCallVirt,
    }
}
