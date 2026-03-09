
namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class CodeGenerator {
    internal enum UseKind : byte {
        Unused,
        UsedAsValue,
        UsedAsAddress,
    }
}
