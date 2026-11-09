
namespace Buckle.CodeAnalysis.CodeGeneration;

internal sealed partial class ILOptimizer {
    internal enum ExprContext {
        None,
        SideEffects,
        Value,
        Address,
        AssignmentTarget,
        Box
    }
}
