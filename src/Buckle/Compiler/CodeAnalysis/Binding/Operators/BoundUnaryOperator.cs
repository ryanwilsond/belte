using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound unary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundUnaryOperator {
    private BoundUnaryOperator(
        SyntaxKind kind,
        BoundUnaryOperatorKind opKind,
        TypeSymbol operandType,
        TypeSymbol resultType) {
        this.kind = kind;
        this.opKind = opKind;
        this.operandType = operandType;
        type = resultType;
    }

    private BoundUnaryOperator(SyntaxKind kind, BoundUnaryOperatorKind opKind, TypeSymbol operandType)
        : this(kind, opKind, operandType, operandType) { }

    /// <summary>
    /// All defined possible operators, and their operand type.
    /// </summary>
    internal static BoundUnaryOperator[] Operators = {
        // integer
        new BoundUnaryOperator(SyntaxKind.PlusToken, BoundUnaryOperatorKind.NumericalIdentity,
            TypeSymbol.Int),
        new BoundUnaryOperator(SyntaxKind.MinusToken, BoundUnaryOperatorKind.NumericalNegation,
            TypeSymbol.Int),
        new BoundUnaryOperator(SyntaxKind.TildeToken, BoundUnaryOperatorKind.BitwiseCompliment,
            TypeSymbol.Int),

        // boolean
        new BoundUnaryOperator(SyntaxKind.ExclamationToken, BoundUnaryOperatorKind.BooleanNegation,
            TypeSymbol.Bool),

        // decimal
        new BoundUnaryOperator(SyntaxKind.PlusToken, BoundUnaryOperatorKind.NumericalIdentity,
            TypeSymbol.Decimal),
        new BoundUnaryOperator(SyntaxKind.MinusToken, BoundUnaryOperatorKind.NumericalNegation,
            TypeSymbol.Decimal),
    };

    /// <summary>
    /// Operator token type.
    /// </summary>
    internal SyntaxKind kind { get; }

    /// <summary>
    /// Bound operator type.
    /// </summary>
    internal BoundUnaryOperatorKind opKind { get; }

    internal TypeSymbol operandType { get; }

    /// <summary>
    /// Result value <see cref="TypeSymbol" />.
    /// </summary>
    internal TypeSymbol type { get; }

    internal static BoundUnaryOperator BindWithOverloading(
        SyntaxToken operatorToken,
        SyntaxKind kind,
        BoundExpression operand,
        OverloadResolution overloadResolution,
        out OverloadResolutionResult<MethodSymbol> result) {
        var name = SyntaxFacts.GetOperatorMemberName(kind, 1);

        if (name is not null) {
            var symbols = ((operand.type is NamedTypeSymbol o)
                ? o.GetMembers(name).Where(m => m is MethodSymbol).Select(m => m as MethodSymbol)
                : []).ToImmutableArray();

            if (symbols.Length > 0) {
                result = overloadResolution.SuppressedMethodOverloadResolution(
                    symbols,
                    [(null, operand)],
                    name,
                    operatorToken,
                    null,
                    operand.type
                );

                if (result.succeeded || result.ambiguous)
                    return null;
            }
        }

        result = OverloadResolutionResult<MethodSymbol>.Failed();

        return Bind(kind, operand.type);
    }

    /// <summary>
    /// Attempts to bind an operator with given operand.
    /// </summary>
    /// <param name="kind">Operator <see cref="BoundType" />.</param>
    /// <param name="operandType">Operand <see cref="BoundType" />.</param>
    /// <returns><see cref="BoundUnaryOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundUnaryOperator Bind(SyntaxKind kind, BoundType operandType) {
        foreach (var op in Operators) {
            var operandIsCorrect = op.operandType is null
                ? true
                : Conversion.Classify(operandType, op.operandType, false).isImplicit;

            if (op.kind == kind && operandIsCorrect) {
                if (op.operandType is null) {
                    return new BoundUnaryOperator(kind, op.opKind, operandType);
                } else if (operandType.isNullable) {
                    return new BoundUnaryOperator(
                        kind,
                        op.opKind,
                        BoundType.CopyWith(op.operandType, isNullable: true),
                        BoundType.CopyWith(op.type, isNullable: true)
                    );
                } else {
                    return op;
                }
            }
        }

        return null;
    }
}
