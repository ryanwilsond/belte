using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal static class BinaryOperatorKindExtensions {
    internal static int OperatorIndex(this BinaryOperatorKind kind) {
        return ((int)kind.Operator() >> 8) - 16;
    }

    internal static BinaryOperatorKind Operator(this BinaryOperatorKind kind) {
        return kind & BinaryOperatorKind.OpMask;
    }

    internal static BinaryOperatorKind OperatorWithConditional(this BinaryOperatorKind kind) {
        return kind & (BinaryOperatorKind.OpMask | BinaryOperatorKind.Conditional);
    }

    internal static BinaryOperatorKind OperandTypes(this BinaryOperatorKind kind) {
        return kind & BinaryOperatorKind.TypeMask;
    }

    internal static bool IsLifted(this BinaryOperatorKind kind) {
        return 0 != (kind & BinaryOperatorKind.Lifted);
    }

    internal static bool IsUserDefined(this BinaryOperatorKind kind) {
        return (kind & BinaryOperatorKind.TypeMask) == BinaryOperatorKind.UserDefined;
    }

    internal static bool IsChecked(this BinaryOperatorKind kind) {
        return 0 != (kind & BinaryOperatorKind.Checked);
    }

    internal static BinaryOperatorKind WithOverflowChecksIfApplicable(this BinaryOperatorKind kind, bool enabled) {
        if (enabled) {
            if (kind.IsIntegral()) {
                switch (kind.Operator()) {
                    case BinaryOperatorKind.Addition:
                    case BinaryOperatorKind.Subtraction:
                    case BinaryOperatorKind.Multiplication:
                    case BinaryOperatorKind.Division:
                        return kind | BinaryOperatorKind.Checked;
                }
            }

            return kind;
        } else {
            return kind & ~BinaryOperatorKind.Checked;
        }
    }

    internal static bool IsIntegral(this BinaryOperatorKind kind) {
        switch (kind.OperandTypes()) {
            case BinaryOperatorKind.Int8:
            case BinaryOperatorKind.Int16:
            case BinaryOperatorKind.UInt8:
            case BinaryOperatorKind.UInt16:
                throw ExceptionUtilities.UnexpectedValue(kind);
            case BinaryOperatorKind.Int32:
            case BinaryOperatorKind.Int64:
            case BinaryOperatorKind.UInt32:
            case BinaryOperatorKind.UInt64:
            case BinaryOperatorKind.Char:
            case BinaryOperatorKind.Enum:
            case BinaryOperatorKind.EnumAndUnderlying:
            case BinaryOperatorKind.UnderlyingAndEnum:
            case BinaryOperatorKind.Pointer:
                return true;
        }

        return false;
    }

    internal static bool EmitsAsCheckedInstruction(this BinaryOperatorKind kind) {
        if (!kind.IsChecked())
            return false;

        switch (kind.Operator()) {
            case BinaryOperatorKind.Addition:
            case BinaryOperatorKind.Subtraction:
            case BinaryOperatorKind.Multiplication:
                return true;
        }

        return false;
    }

    internal static bool IsShift(this BinaryOperatorKind kind) {
        var type = kind.Operator();
        return type == BinaryOperatorKind.LeftShift ||
            type == BinaryOperatorKind.RightShift ||
            type == BinaryOperatorKind.UnsignedRightShift;
    }

    internal static bool IsConditional(this BinaryOperatorKind kind) {
        return 0 != (kind & BinaryOperatorKind.Conditional);
    }

    internal static bool IsComparison(this BinaryOperatorKind kind) {
        switch (kind.Operator()) {
            case BinaryOperatorKind.Equal:
            case BinaryOperatorKind.NotEqual:
            case BinaryOperatorKind.GreaterThan:
            case BinaryOperatorKind.GreaterThanOrEqual:
            case BinaryOperatorKind.LessThan:
            case BinaryOperatorKind.LessThanOrEqual:
                return true;
        }

        return false;
    }

    internal static SyntaxKind ToSyntaxKind(this BinaryOperatorKind kind) {
        var isConditional = kind.IsConditional();

        return (kind & BinaryOperatorKind.OpMask) switch {
            BinaryOperatorKind.Multiplication => SyntaxKind.AsteriskToken,
            BinaryOperatorKind.Addition => SyntaxKind.PlusToken,
            BinaryOperatorKind.Subtraction => SyntaxKind.MinusToken,
            BinaryOperatorKind.Division => SyntaxKind.SlashToken,
            BinaryOperatorKind.Modulo => SyntaxKind.PercentToken,
            BinaryOperatorKind.LeftShift => SyntaxKind.LessThanLessThanToken,
            BinaryOperatorKind.RightShift => SyntaxKind.GreaterThanGreaterThanToken,
            BinaryOperatorKind.Equal => SyntaxKind.EqualsEqualsToken,
            BinaryOperatorKind.NotEqual => SyntaxKind.ExclamationEqualsToken,
            BinaryOperatorKind.GreaterThan => SyntaxKind.GreaterThanToken,
            BinaryOperatorKind.LessThan => SyntaxKind.LessThanToken,
            BinaryOperatorKind.GreaterThanOrEqual => SyntaxKind.GreaterThanEqualsToken,
            BinaryOperatorKind.LessThanOrEqual => SyntaxKind.LessThanEqualsToken,
            BinaryOperatorKind.UnsignedRightShift => SyntaxKind.GreaterThanGreaterThanGreaterThanToken,
            BinaryOperatorKind.And when isConditional => SyntaxKind.AmpersandAmpersandToken,
            BinaryOperatorKind.And when !isConditional => SyntaxKind.AmpersandToken,
            BinaryOperatorKind.Or when isConditional => SyntaxKind.PipePipeToken,
            BinaryOperatorKind.Or when !isConditional => SyntaxKind.PipeToken,
            BinaryOperatorKind.Xor => SyntaxKind.CaretToken,
            BinaryOperatorKind.Power => SyntaxKind.AsteriskAsteriskToken,
            _ => throw ExceptionUtilities.UnexpectedValue(kind),
        };
    }
}
