
namespace Buckle.CodeAnalysis.Binding;

internal readonly struct BoundPatternBinding {
    internal readonly BoundExpression variableAccess;

    internal readonly BoundDagTemp tempContainingValue;

    internal BoundPatternBinding(BoundExpression variableAccess, BoundDagTemp tempContainingValue) {
        this.variableAccess = variableAccess;
        this.tempContainingValue = tempContainingValue;
    }
}
