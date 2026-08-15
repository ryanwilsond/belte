using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal static class OperatorFacts {
    internal static bool NoUserDefinedOperators(TypeSymbol type) {
        switch (type.typeKind) {
            case TypeKind.Class:
            case TypeKind.Struct:
            case TypeKind.Enum:
            case TypeKind.TemplateParameter:
            case TypeKind.Interface:
                return false;
            default:
                return true;
        }
    }

    internal static bool IsValidObjectEquality(
        Conversions conversions,
        TypeSymbol leftType,
        bool leftIsNull,
        TypeSymbol rightType,
        bool rightIsNull) {
        if ((leftType is not null) && leftType.IsTemplateParameter()) {
            if (leftType.isValueType || (!leftType.isReferenceType && !rightIsNull))
                return false;

            leftType = ((TemplateParameterSymbol)leftType).effectiveBaseClass;
        }

        if ((rightType is not null) && rightType.IsTemplateParameter()) {
            if (rightType.isValueType || (!rightType.isReferenceType && !leftIsNull))
                return false;

            rightType = ((TemplateParameterSymbol)rightType).effectiveBaseClass;
        }

        var leftIsObjectType = (leftType is not null) && leftType.isReferenceType;

        if (!leftIsObjectType && !leftIsNull)
            return false;

        var rightIsObjectType = (rightType is not null) && rightType.isReferenceType;

        if (!rightIsObjectType && !rightIsNull)
            return false;

        if (leftIsNull || rightIsNull)
            return true;

        var leftConversion = conversions.ClassifyConversionFromType(leftType, rightType);

        if (leftConversion.isIdentity || leftConversion.isReference)
            return true;

        var rightConversion = conversions.ClassifyConversionFromType(rightType, leftType);

        if (rightConversion.isIdentity || rightConversion.isReference)
            return true;

        return false;
    }

    internal static string GetBinaryOperatorNameFromKind(BinaryOperatorKind kind) {
        return (kind & BinaryOperatorKind.OpMask) switch {
            BinaryOperatorKind.Addition => WellKnownMemberNames.AdditionOperatorName,
            BinaryOperatorKind.And => WellKnownMemberNames.BitwiseAndOperatorName,
            BinaryOperatorKind.Division => WellKnownMemberNames.DivideOperatorName,
            BinaryOperatorKind.Equal => WellKnownMemberNames.EqualityOperatorName,
            BinaryOperatorKind.GreaterThan => WellKnownMemberNames.GreaterThanOperatorName,
            BinaryOperatorKind.GreaterThanOrEqual => WellKnownMemberNames.GreaterThanOrEqualOperatorName,
            BinaryOperatorKind.LeftShift => WellKnownMemberNames.LeftShiftOperatorName,
            BinaryOperatorKind.LessThan => WellKnownMemberNames.LessThanOperatorName,
            BinaryOperatorKind.LessThanOrEqual => WellKnownMemberNames.LessThanOrEqualOperatorName,
            BinaryOperatorKind.Multiplication => WellKnownMemberNames.MultiplyOperatorName,
            BinaryOperatorKind.Or => WellKnownMemberNames.BitwiseOrOperatorName,
            BinaryOperatorKind.NotEqual => WellKnownMemberNames.InequalityOperatorName,
            BinaryOperatorKind.Modulo => WellKnownMemberNames.ModulusOperatorName,
            BinaryOperatorKind.RightShift => WellKnownMemberNames.RightShiftOperatorName,
            BinaryOperatorKind.UnsignedRightShift => WellKnownMemberNames.UnsignedRightShiftOperatorName,
            BinaryOperatorKind.Subtraction => WellKnownMemberNames.SubtractionOperatorName,
            BinaryOperatorKind.Xor => WellKnownMemberNames.BitwiseExclusiveOrOperatorName,
            BinaryOperatorKind.Power => WellKnownMemberNames.PowerOperatorName,
            BinaryOperatorKind.Min => WellKnownMemberNames.SlashBackslashOperatorName,
            BinaryOperatorKind.Max => WellKnownMemberNames.BackslashSlashOperatorName,
            _ => throw ExceptionUtilities.UnexpectedValue(kind & BinaryOperatorKind.OpMask),
        };
    }

    internal static string GetUnaryOperatorNameFromKind(UnaryOperatorKind kind) {
        return (kind & UnaryOperatorKind.OpMask) switch {
            UnaryOperatorKind.UnaryPlus => WellKnownMemberNames.UnaryPlusOperatorName,
            UnaryOperatorKind.UnaryMinus => WellKnownMemberNames.UnaryNegationOperatorName,
            UnaryOperatorKind.BitwiseComplement => WellKnownMemberNames.BitwiseNotOperatorName,
            UnaryOperatorKind.LogicalNegation => WellKnownMemberNames.LogicalNotOperatorName,
            UnaryOperatorKind.PostfixIncrement or UnaryOperatorKind.PrefixIncrement
                => WellKnownMemberNames.IncrementOperatorName,
            UnaryOperatorKind.PostfixDecrement or UnaryOperatorKind.PrefixDecrement
                => WellKnownMemberNames.DecrementOperatorName,
            _ => throw ExceptionUtilities.UnexpectedValue(kind & UnaryOperatorKind.OpMask),
        };
    }

    internal static string GetCompoundOperatorNameFromKind(SyntaxKind kind) {
        switch (kind) {
            case SyntaxKind.SlashBackslashEqualsToken: return WellKnownMemberNames.SlashBackslashAssignmentOperatorName;
            case SyntaxKind.BackslashSlashEqualsToken: return WellKnownMemberNames.BackslashSlashAssignmentOperatorName;
            case SyntaxKind.AsteriskAsteriskEqualsToken: return WellKnownMemberNames.PowerAssignmentOperatorName;
            case SyntaxKind.PlusEqualsToken: return WellKnownMemberNames.AdditionAssignmentOperatorName;
            case SyntaxKind.MinusEqualsToken: return WellKnownMemberNames.SubtractionAssignmentOperatorName;
            case SyntaxKind.AsteriskEqualsToken: return WellKnownMemberNames.MultiplicationAssignmentOperatorName;
            case SyntaxKind.SlashEqualsToken: return WellKnownMemberNames.DivisionAssignmentOperatorName;
            case SyntaxKind.PercentEqualsToken: return WellKnownMemberNames.ModulusAssignmentOperatorName;
            case SyntaxKind.CaretEqualsToken: return WellKnownMemberNames.ExclusiveOrAssignmentOperatorName;
            case SyntaxKind.AmpersandEqualsToken: return WellKnownMemberNames.BitwiseAndAssignmentOperatorName;
            case SyntaxKind.PipeEqualsToken: return WellKnownMemberNames.BitwiseOrAssignmentOperatorName;
            case SyntaxKind.LessThanLessThanEqualsToken: return WellKnownMemberNames.LeftShiftAssignmentOperatorName;
            case SyntaxKind.GreaterThanGreaterThanEqualsToken: return WellKnownMemberNames.RightShiftAssignmentOperatorName;
            case SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken: return WellKnownMemberNames.UnsignedRightShiftAssignmentOperatorName;
            case SyntaxKind.PlusPlusToken: return WellKnownMemberNames.IncrementAssignmentOperatorName;
            case SyntaxKind.MinusMinusToken: return WellKnownMemberNames.DecrementAssignmentOperatorName;
            default:
                throw ExceptionUtilities.UnexpectedValue(kind);
        }
    }

    internal static bool OperatorAllowsTemplate(string name) {
        switch (name) {
            case WellKnownMemberNames.IndexOperatorName:
                return false;
            case WellKnownMemberNames.PowerOperatorName:
            case WellKnownMemberNames.MultiplyOperatorName:
            case WellKnownMemberNames.DivideOperatorName:
            case WellKnownMemberNames.ModulusOperatorName:
            case WellKnownMemberNames.AdditionOperatorName:
            case WellKnownMemberNames.UnaryPlusOperatorName:
            case WellKnownMemberNames.SubtractionOperatorName:
            case WellKnownMemberNames.UnaryNegationOperatorName:
            case WellKnownMemberNames.LeftShiftOperatorName:
            case WellKnownMemberNames.RightShiftOperatorName:
            case WellKnownMemberNames.UnsignedRightShiftOperatorName:
            case WellKnownMemberNames.BitwiseAndOperatorName:
            case WellKnownMemberNames.BitwiseExclusiveOrOperatorName:
            case WellKnownMemberNames.BitwiseOrOperatorName:
            case WellKnownMemberNames.IncrementOperatorName:
            case WellKnownMemberNames.DecrementOperatorName:
            case WellKnownMemberNames.LogicalNotOperatorName:
            case WellKnownMemberNames.BitwiseNotOperatorName:
            case WellKnownMemberNames.EqualityOperatorName:
            case WellKnownMemberNames.InequalityOperatorName:
            case WellKnownMemberNames.LessThanOperatorName:
            case WellKnownMemberNames.GreaterThanOperatorName:
            case WellKnownMemberNames.LessThanOrEqualOperatorName:
            case WellKnownMemberNames.GreaterThanOrEqualOperatorName:
            case WellKnownMemberNames.SlashBackslashOperatorName:
            case WellKnownMemberNames.BackslashSlashOperatorName:
                return true;
            case WellKnownMemberNames.ImplicitConversionName:
            case WellKnownMemberNames.ExplicitConversionName:
                return true;
            case WellKnownMemberNames.LengthOperatorName:
            case WellKnownMemberNames.IterOperatorName:
                return false;
            case WellKnownMemberNames.PowerAssignmentOperatorName:
            case WellKnownMemberNames.AdditionAssignmentOperatorName:
            case WellKnownMemberNames.SubtractionAssignmentOperatorName:
            case WellKnownMemberNames.MultiplicationAssignmentOperatorName:
            case WellKnownMemberNames.DivisionAssignmentOperatorName:
            case WellKnownMemberNames.ModulusAssignmentOperatorName:
            case WellKnownMemberNames.BitwiseAndAssignmentOperatorName:
            case WellKnownMemberNames.BitwiseOrAssignmentOperatorName:
            case WellKnownMemberNames.ExclusiveOrAssignmentOperatorName:
            case WellKnownMemberNames.LeftShiftAssignmentOperatorName:
            case WellKnownMemberNames.RightShiftAssignmentOperatorName:
            case WellKnownMemberNames.UnsignedRightShiftAssignmentOperatorName:
            case WellKnownMemberNames.SlashBackslashAssignmentOperatorName:
            case WellKnownMemberNames.BackslashSlashAssignmentOperatorName:
            case WellKnownMemberNames.IncrementAssignmentOperatorName:
            case WellKnownMemberNames.DecrementAssignmentOperatorName:
                return false;
            default:
                return false;
        }
    }

    internal static bool IsCompoundAssignmentOperatorName(string operatorMetadataName) {
        switch (operatorMetadataName) {
            case WellKnownMemberNames.DecrementAssignmentOperatorName:
            case WellKnownMemberNames.IncrementAssignmentOperatorName:
            case WellKnownMemberNames.SlashBackslashAssignmentOperatorName:
            case WellKnownMemberNames.BackslashSlashAssignmentOperatorName:
            case WellKnownMemberNames.PowerAssignmentOperatorName:
            case WellKnownMemberNames.AdditionAssignmentOperatorName:
            case WellKnownMemberNames.SubtractionAssignmentOperatorName:
            case WellKnownMemberNames.MultiplicationAssignmentOperatorName:
            case WellKnownMemberNames.DivisionAssignmentOperatorName:
            case WellKnownMemberNames.ModulusAssignmentOperatorName:
            case WellKnownMemberNames.BitwiseAndAssignmentOperatorName:
            case WellKnownMemberNames.BitwiseOrAssignmentOperatorName:
            case WellKnownMemberNames.ExclusiveOrAssignmentOperatorName:
            case WellKnownMemberNames.LeftShiftAssignmentOperatorName:
            case WellKnownMemberNames.RightShiftAssignmentOperatorName:
            case WellKnownMemberNames.UnsignedRightShiftAssignmentOperatorName:
                return true;
            default:
                return false;
        }
    }
}
