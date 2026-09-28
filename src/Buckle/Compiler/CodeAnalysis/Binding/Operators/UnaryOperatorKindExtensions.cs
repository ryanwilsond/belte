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

    internal static bool IsChecked(this UnaryOperatorKind kind) {
        return 0 != (kind & UnaryOperatorKind.Checked);
    }

    internal static UnaryOperatorKind WithOverflowChecksIfApplicable(this UnaryOperatorKind kind, bool enabled) {
        if (enabled) {
            if (kind.IsIntegral()) {
                switch (kind.Operator()) {
                    case UnaryOperatorKind.PrefixIncrement:
                    case UnaryOperatorKind.PostfixIncrement:
                    case UnaryOperatorKind.PrefixDecrement:
                    case UnaryOperatorKind.PostfixDecrement:
                    case UnaryOperatorKind.UnaryMinus:
                        return kind | UnaryOperatorKind.Checked;
                }
            }

            return kind;
        } else {
            return kind & ~UnaryOperatorKind.Checked;
        }
    }

    internal static bool IsIntegral(this UnaryOperatorKind kind) {
        switch (kind.OperandTypes()) {
            case UnaryOperatorKind.Int8:
            case UnaryOperatorKind.Int16:
            case UnaryOperatorKind.Int32:
            case UnaryOperatorKind.Int64:
            case UnaryOperatorKind.UInt8:
            case UnaryOperatorKind.UInt16:
            case UnaryOperatorKind.UInt32:
            case UnaryOperatorKind.UInt64:
            case UnaryOperatorKind.Char:
            case UnaryOperatorKind.Enum:
            case UnaryOperatorKind.Pointer:
                return true;
        }

        return false;
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
