
namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class ILOptimizer {
    internal enum ExprContext {
        None,
        Sideeffects,
        Value,
        Address,
        AssignmentTarget,
        Box
    }
}
