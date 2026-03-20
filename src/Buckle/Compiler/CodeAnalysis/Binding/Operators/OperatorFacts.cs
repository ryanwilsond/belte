using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Libraries;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal static class OperatorFacts {
    internal static bool NoUserDefinedOperators(TypeSymbol type) {
        return type.typeKind switch {
            TypeKind.Class or TypeKind.TemplateParameter => false,
            _ => true,
        };
    }

    internal static bool IsValidObjectEquality(
        Conversions conversions,
        TypeSymbol leftType,
        bool leftIsNull,
        TypeSymbol rightType,
        bool rightIsNull) {
        if ((leftType is not null) && leftType.IsTemplateParameter()) {
            if (leftType.isPrimitiveType || (!leftType.isObjectType && !rightIsNull))
                return false;

            leftType = ((TemplateParameterSymbol)leftType).effectiveBaseClass;
        }

        if ((rightType is not null) && rightType.IsTemplateParameter()) {
            if (rightType.isPrimitiveType || (!rightType.isObjectType && !leftIsNull))
                return false;

            rightType = ((TemplateParameterSymbol)rightType).effectiveBaseClass;
        }

        var leftIsObjectType = (leftType is not null) && leftType.isObjectType;

        if (!leftIsObjectType && !leftIsNull)
            return false;

        var rightIsObjectType = (rightType is not null) && rightType.isObjectType;

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

    internal static string OperatorNameFromDeclaration(ConversionDeclarationSyntax declaration) {
        return declaration.implicitOrExplicitKeyword.kind switch {
            SyntaxKind.ImplicitKeyword => WellKnownMemberNames.ImplicitConversionName,
            _ => WellKnownMemberNames.ExplicitConversionName,
        };
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

    internal static BinaryOperatorSignature GetSignature(BinaryOperatorKind kind) {
        var left = TypeFromKind(kind);

        switch (kind.Operator()) {
            case BinaryOperatorKind.Multiplication:
            case BinaryOperatorKind.Division:
            case BinaryOperatorKind.Subtraction:
            case BinaryOperatorKind.Modulo:
            case BinaryOperatorKind.And:
            case BinaryOperatorKind.Or:
            case BinaryOperatorKind.Xor:
                return new BinaryOperatorSignature(kind, left, left, left);
            case BinaryOperatorKind.Addition:
                return new BinaryOperatorSignature(kind, left, TypeFromKind(kind), TypeFromKind(kind));
            case BinaryOperatorKind.LeftShift:
            case BinaryOperatorKind.RightShift:
            case BinaryOperatorKind.UnsignedRightShift:
                var rightType = CorLibrary.GetSpecialType(SpecialType.Int);

                if (kind.IsLifted())
                    rightType = CorLibrary.GetOrCreateNullableType(rightType);

                return new BinaryOperatorSignature(kind, left, rightType, left);
            case BinaryOperatorKind.Equal:
            case BinaryOperatorKind.NotEqual:
            case BinaryOperatorKind.GreaterThan:
            case BinaryOperatorKind.LessThan:
            case BinaryOperatorKind.GreaterThanOrEqual:
            case BinaryOperatorKind.LessThanOrEqual:
                return new BinaryOperatorSignature(
                    kind,
                    left,
                    left,
                    kind.IsLifted()
                        ? CorLibrary.GetNullableType(SpecialType.Bool)
                        : CorLibrary.GetSpecialType(SpecialType.Bool)
                );
        }

        return new BinaryOperatorSignature(kind, left, TypeFromKind(kind), TypeFromKind(kind));
    }

    internal static UnaryOperatorSignature GetSignature(UnaryOperatorKind kind) {
        var opType = kind.OperandTypes() switch {
            UnaryOperatorKind.Int => CorLibrary.GetSpecialType(SpecialType.Int),
            UnaryOperatorKind.Char => CorLibrary.GetSpecialType(SpecialType.Char),
            UnaryOperatorKind.Decimal => CorLibrary.GetSpecialType(SpecialType.Decimal),
            UnaryOperatorKind.Bool => CorLibrary.GetSpecialType(SpecialType.Bool),
            _ => throw ExceptionUtilities.UnexpectedValue(kind.OperandTypes()),
        };

        if (kind.IsLifted())
            opType = CorLibrary.GetOrCreateNullableType(opType);

        return new UnaryOperatorSignature(kind, opType, opType);
    }

    private static TypeSymbol TypeFromKind(BinaryOperatorKind kind) {
        var type = kind.OperandTypes() switch {
            BinaryOperatorKind.Int => CorLibrary.GetSpecialType(SpecialType.Int),
            BinaryOperatorKind.Decimal => CorLibrary.GetSpecialType(SpecialType.Decimal),
            BinaryOperatorKind.Bool => CorLibrary.GetSpecialType(SpecialType.Bool),
            BinaryOperatorKind.Object => CorLibrary.GetSpecialType(SpecialType.Object),
            BinaryOperatorKind.String => CorLibrary.GetSpecialType(SpecialType.String),
            BinaryOperatorKind.Char => CorLibrary.GetSpecialType(SpecialType.Char),
            BinaryOperatorKind.Type => CorLibrary.GetSpecialType(SpecialType.Type),
            BinaryOperatorKind.Any => CorLibrary.GetSpecialType(SpecialType.Any),
            _ => throw ExceptionUtilities.UnexpectedValue(kind.OperandTypes()),
        };

        if (kind.IsLifted())
            type = CorLibrary.GetOrCreateNullableType(type);

        return type;
    }
}
