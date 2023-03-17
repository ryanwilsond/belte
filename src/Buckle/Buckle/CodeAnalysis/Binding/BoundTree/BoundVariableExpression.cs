using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound variable expression, bound from a <see cref="Syntax.NameExpressionSyntax" />.
/// </summary>
internal sealed class BoundVariableExpression : BoundExpression {
    internal BoundVariableExpression(VariableSymbol variable) {
        this.variable = variable;
    }

    internal VariableSymbol variable { get; }

    internal override BoundType type => variable.type;

    internal override BoundNodeKind kind => BoundNodeKind.VariableExpression;

    internal override BoundConstant constantValue => variable.constantValue;
}
