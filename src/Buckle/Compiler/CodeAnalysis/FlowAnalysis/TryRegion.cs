
namespace Buckle.CodeAnalysis.FlowAnalysis;

internal sealed class TryRegion {
    internal BasicBlock tryStart { get; set; }

    internal BasicBlock tryEnd { get; set; }

    internal BasicBlock catchBlock { get; set; }

    internal BasicBlock finallyBlock { get; set; }
}
