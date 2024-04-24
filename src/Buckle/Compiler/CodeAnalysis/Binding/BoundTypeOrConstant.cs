
namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Template argument value.
/// </summary>
internal sealed class BoundTypeOrConstant {
    internal BoundTypeOrConstant(BoundConstant constant, BoundType type, BoundExpression expression) {
        this.constant = constant;
        this.type = type;
        this.expression = expression;
        isConstant = true;
    }

    internal BoundTypeOrConstant(BoundType type) {
        constant = null;
        expression = null;
        isConstant = false;
        this.type = type;
    }

    internal bool isConstant { get; }

    internal BoundConstant constant { get; }

    internal BoundType type { get; }

    internal BoundExpression expression { get; }

    internal bool Equals(BoundTypeOrConstant typeOrConstant) {
        if (isConstant)
            return constant?.value == typeOrConstant.constant?.value;
        else
            return type.Equals(typeOrConstant.type);
    }
}
