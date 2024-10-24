using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound field access expression, bound from a <see cref="Syntax.MemberAccessExpressionSyntax" />.
/// </summary>
internal sealed class BoundFieldAccessExpression : BoundExpression {
    internal BoundFieldAccessExpression(
        BoundExpression receiver,
        FieldSymbol field,
        TypeSymbol type,
        ConstantValue constant) {
        this.receiver = receiver;
        this.field = field;
        this.type = type;
        constantValue = constant;
    }

    internal override BoundNodeKind kind => BoundNodeKind.FieldAccessExpression;

    internal override TypeSymbol type { get; }

    internal override ConstantValue constantValue { get; }

    internal BoundExpression receiver { get; }

    internal FieldSymbol field { get; }
}
