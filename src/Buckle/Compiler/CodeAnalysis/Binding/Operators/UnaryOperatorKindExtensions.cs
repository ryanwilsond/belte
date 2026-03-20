using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal static class UnaryOperatorKindExtensions {
    internal static int OperatorIndex(this UnaryOperatorKind kind) {
        return ((int)kind.Operator() >> 8) - 16;
    }

    internal static UnaryOperatorKind OperandTypes(this UnaryOperatorKind kind) {
        return kind & UnaryOperatorKind.TypeMask;
    }

    internal static UnaryOperatorKind Operator(this UnaryOperatorKind kind) {
        return kind & UnaryOperatorKind.OpMask;
    }

    internal static bool IsLifted(this UnaryOperatorKind kind) {
        return 0 != (kind & UnaryOperatorKind.Lifted);
    }

    internal static SyntaxKind ToSyntaxKind(this UnaryOperatorKind kind) {
        return (kind & UnaryOperatorKind.OpMask) switch {
            UnaryOperatorKind.PostfixIncrement or UnaryOperatorKind.PrefixIncrement => SyntaxKind.PlusPlusToken,
            UnaryOperatorKind.PostfixDecrement or UnaryOperatorKind.PrefixDecrement => SyntaxKind.MinusMinusToken,
            UnaryOperatorKind.UnaryPlus => SyntaxKind.PlusToken,
            UnaryOperatorKind.UnaryMinus => SyntaxKind.MinusToken,
            UnaryOperatorKind.LogicalNegation => SyntaxKind.ExclamationToken,
            UnaryOperatorKind.BitwiseComplement => SyntaxKind.TildeToken,
            _ => throw ExceptionUtilities.UnexpectedValue(kind)
        };
    }
}
