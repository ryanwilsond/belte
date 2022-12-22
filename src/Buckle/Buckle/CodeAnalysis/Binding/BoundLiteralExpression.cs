using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound literal expression, bound from a <see cref="LiteralExpression" />.
/// </summary>
internal sealed class BoundLiteralExpression : BoundExpression {
    internal BoundLiteralExpression(object value) {
        if (value is bool)
            typeClause = new BoundTypeClause(TypeSymbol.Bool, isLiteral: true);
        else if (value is int)
            typeClause = new BoundTypeClause(TypeSymbol.Int, isLiteral: true);
        else if (value is string)
            typeClause = new BoundTypeClause(TypeSymbol.String, isLiteral: true);
        else if (value is double)
            typeClause = new BoundTypeClause(TypeSymbol.Decimal, isLiteral: true);
        else if (value == null)
            typeClause = new BoundTypeClause(null, isLiteral: true);
        else
            throw new BelteInternalException(
                $"BoundLiteralExpression: unexpected literal '{value}' of type '{value.GetType()}'");

        constantValue = new BoundConstant(value);
    }

    /// <param name="override">Forces a <see cref="BoundTypeClause" /> on a value instead of implying.</param>
    internal BoundLiteralExpression(object value, BoundTypeClause @override) {
        typeClause = new BoundTypeClause(
            @override.lType, @override.isImplicit, @override.isConstantReference, @override.isReference,
            @override.isConstant, @override.isNullable, true, @override.dimensions);

        constantValue = new BoundConstant(value);
    }

    internal override BoundNodeType type => BoundNodeType.LiteralExpression;

    internal override BoundTypeClause typeClause { get; }

    internal override BoundConstant constantValue { get; }

    internal object value => constantValue.value;
}
