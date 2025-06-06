
namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class CodeGenerator {
    private enum UseKind : byte {
        Unused,
        UsedAsValue,
        UsedAsAddress,
    }
}
