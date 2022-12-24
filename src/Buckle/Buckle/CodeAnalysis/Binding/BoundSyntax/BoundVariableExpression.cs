using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound variable expression, bound from a <see cref="VariableExpresionSyntax" />.
/// </summary>
internal sealed class BoundVariableExpression : BoundExpression {
    internal BoundVariableExpression(VariableSymbol variable) {
        this.variable = variable;
    }

    internal VariableSymbol variable { get; }

    internal override BoundTypeClause typeClause => variable.typeClause;

    internal override BoundNodeKind kind => BoundNodeKind.VariableExpression;

    internal override BoundConstant constantValue => variable.constantValue;
}
