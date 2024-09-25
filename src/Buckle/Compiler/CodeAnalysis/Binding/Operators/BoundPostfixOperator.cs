using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound postfix operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundPostfixOperator {
    private BoundPostfixOperator(
        SyntaxKind kind,
        BoundPostfixOperatorKind opKind,
        TypeSymbol operandType,
        TypeSymbol resultType) {
        this.kind = kind;
        this.opKind = opKind;
        this.operandType = operandType;
        type = resultType;
    }

    private BoundPostfixOperator(SyntaxKind kind, BoundPostfixOperatorKind opKind, TypeSymbol operandType)
        : this(kind, opKind, operandType, operandType) { }

    /// <summary>
    /// All defined possible operators, and their operand type.
    /// </summary>
    internal static BoundPostfixOperator[] Operators = {
        // integer
        new BoundPostfixOperator(SyntaxKind.PlusPlusToken, BoundPostfixOperatorKind.Increment, TypeSymbol.Int),
        new BoundPostfixOperator(SyntaxKind.MinusMinusToken, BoundPostfixOperatorKind.Decrement, TypeSymbol.Int),

        // decimal
        new BoundPostfixOperator(SyntaxKind.PlusPlusToken, BoundPostfixOperatorKind.Increment, TypeSymbol.Decimal),
        new BoundPostfixOperator(SyntaxKind.MinusMinusToken, BoundPostfixOperatorKind.Decrement, TypeSymbol.Decimal),

        new BoundPostfixOperator(SyntaxKind.ExclamationToken, BoundPostfixOperatorKind.NullAssert, null),
    };

    /// <summary>
    /// Operator token type.
    /// </summary>
    internal SyntaxKind kind { get; }

    /// <summary>
    /// Bound operator type.
    /// </summary>
    internal BoundPostfixOperatorKind opKind { get; }

    internal TypeSymbol operandType { get; }

    /// <summary>
    /// Result value <see cref="TypeSymbol" />.
    /// </summary>
    internal TypeSymbol type { get; }

    internal static BoundPostfixOperator BindWithOverloading(
        SyntaxToken operatorToken,
        SyntaxKind kind,
        BoundExpression operand,
        OverloadResolution overloadResolution,
        out OverloadResolutionResult<MethodSymbol> result) {
        var name = SyntaxFacts.GetOperatorMemberName(kind, 1);

        if (name is not null) {
            var symbols = ((operand.type.typeSymbol is NamedTypeSymbol o)
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
    /// <returns><see cref="BoundPostfixOperator" /> if an operator exists, otherwise null.</returns>
    internal static BoundPostfixOperator Bind(SyntaxKind kind, BoundType operandType) {
        foreach (var op in Operators) {
            var operandIsCorrect = op.operandType is null
                ? true
                : Conversion.Classify(operandType, op.operandType, false).isImplicit;
            var nonNullable = op.operandType is null;

            if (op.kind == kind && operandIsCorrect) {
                if (op.operandType is null) {
                    return new BoundPostfixOperator(
                        kind,
                        op.opKind,
                        nonNullable ? BoundType.CopyWith(operandType, isNullable: false) : operandType
                    );
                } else if (operandType.isNullable) {
                    return new BoundPostfixOperator(
                        kind,
                        op.opKind,
                        BoundType.CopyWith(op.operandType, isNullable: !nonNullable),
                        BoundType.CopyWith(op.type, isNullable: !nonNullable)
                    );
                } else {
                    return op;
                }
            }
        }

        return null;
    }
}
