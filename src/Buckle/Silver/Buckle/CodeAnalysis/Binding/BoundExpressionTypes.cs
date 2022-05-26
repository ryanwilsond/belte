using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal enum BoundBinaryOperatorType {
    Invalid,
    Addition,
    Subtraction,
    Multiplication,
    Division,
    Power,
    LogicalAnd,
    LogicalOr,
    LogicalXor,
    LeftShift,
    RightShift,
    ConditionalAnd,
    ConditionalOr,
    EqualityEquals,
    EqualityNotEquals,
    LessThan,
    GreaterThan,
    LessOrEqual,
    GreatOrEqual,
}

internal sealed class BoundBinaryOperator {
    public SyntaxType type { get; }
    public BoundBinaryOperatorType opType { get; }
    public BoundTypeClause leftType { get; }
    public BoundTypeClause rightType { get; }
    public BoundTypeClause resultType { get; }

    private BoundBinaryOperator(
        SyntaxType type_, BoundBinaryOperatorType opType_,
        BoundTypeClause leftType_, BoundTypeClause rightType_, BoundTypeClause resultType_) {
        type = type_;
        opType = opType_;
        leftType = leftType_;
        rightType = rightType_;
        resultType = resultType_;
    }

    private BoundBinaryOperator(
        SyntaxType type, BoundBinaryOperatorType opType, BoundTypeClause operandType, BoundTypeClause resultType)
        : this(type, opType, operandType, operandType, resultType) { }

    private BoundBinaryOperator(SyntaxType type, BoundBinaryOperatorType opType, BoundTypeClause typeClause)
        : this(type, opType, typeClause, typeClause, typeClause) { }

    internal static BoundBinaryOperator[] operators_ = {
        // integers
        new BoundBinaryOperator(
            SyntaxType.PLUS_TOKEN, BoundBinaryOperatorType.Addition, new BoundTypeClause(TypeSymbol.Int)),
        new BoundBinaryOperator(
            SyntaxType.MINUS_TOKEN, BoundBinaryOperatorType.Subtraction, new BoundTypeClause(TypeSymbol.Int)),
        new BoundBinaryOperator(
            SyntaxType.ASTERISK_TOKEN, BoundBinaryOperatorType.Multiplication, new BoundTypeClause(TypeSymbol.Int)),
        new BoundBinaryOperator(
            SyntaxType.SLASH_TOKEN, BoundBinaryOperatorType.Division, new BoundTypeClause(TypeSymbol.Int)),
        new BoundBinaryOperator(
            SyntaxType.ASTERISK_ASTERISK_TOKEN, BoundBinaryOperatorType.Power, new BoundTypeClause(TypeSymbol.Int)),
        new BoundBinaryOperator(
            SyntaxType.AMPERSAND_TOKEN, BoundBinaryOperatorType.LogicalAnd, new BoundTypeClause(TypeSymbol.Int)),
        new BoundBinaryOperator(
            SyntaxType.PIPE_TOKEN, BoundBinaryOperatorType.LogicalOr, new BoundTypeClause(TypeSymbol.Int)),
        new BoundBinaryOperator(
            SyntaxType.CARET_TOKEN, BoundBinaryOperatorType.LogicalXor, new BoundTypeClause(TypeSymbol.Int)),
        new BoundBinaryOperator(SyntaxType.LESS_THAN_LESS_THAN_TOKEN, BoundBinaryOperatorType.LeftShift,
            new BoundTypeClause(TypeSymbol.Int)),
        new BoundBinaryOperator(SyntaxType.GREATER_THAN_GREATER_THAN_TOKEN, BoundBinaryOperatorType.RightShift,
            new BoundTypeClause(TypeSymbol.Int)),
        new BoundBinaryOperator(SyntaxType.EQUALS_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityEquals,
            new BoundTypeClause(TypeSymbol.Int), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.EXCLAMATION_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityNotEquals,
            new BoundTypeClause(TypeSymbol.Int), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.LESS_THAN_TOKEN, BoundBinaryOperatorType.LessThan,
            new BoundTypeClause(TypeSymbol.Int), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.GREATER_THAN_TOKEN, BoundBinaryOperatorType.GreaterThan,
            new BoundTypeClause(TypeSymbol.Int), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.LESS_THAN_EQUALS_TOKEN, BoundBinaryOperatorType.LessOrEqual,
            new BoundTypeClause(TypeSymbol.Int), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.GREATER_THAN_EQUALS_TOKEN, BoundBinaryOperatorType.GreatOrEqual,
            new BoundTypeClause(TypeSymbol.Int), new BoundTypeClause(TypeSymbol.Bool)),

        // boolean
        new BoundBinaryOperator(SyntaxType.AMPERSAND_AMPERSAND_TOKEN, BoundBinaryOperatorType.ConditionalAnd,
            new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.PIPE_PIPE_TOKEN, BoundBinaryOperatorType.ConditionalOr,
            new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.AMPERSAND_TOKEN, BoundBinaryOperatorType.LogicalAnd,
            new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.PIPE_TOKEN, BoundBinaryOperatorType.LogicalOr,
            new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.CARET_TOKEN, BoundBinaryOperatorType.LogicalXor,
            new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.EQUALS_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityEquals,
            new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.EXCLAMATION_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityNotEquals,
            new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.EXCLAMATION_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityNotEquals,
            new BoundTypeClause(TypeSymbol.Any), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.EQUALS_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityEquals,
            new BoundTypeClause(TypeSymbol.Any), new BoundTypeClause(TypeSymbol.Bool)),

        // string
        new BoundBinaryOperator(SyntaxType.PLUS_TOKEN, BoundBinaryOperatorType.Addition,
            new BoundTypeClause(TypeSymbol.String)),
        new BoundBinaryOperator(SyntaxType.EQUALS_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityEquals,
            new BoundTypeClause(TypeSymbol.String), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.EXCLAMATION_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityNotEquals,
            new BoundTypeClause(TypeSymbol.String), new BoundTypeClause(TypeSymbol.Bool)),

        // decimal
        new BoundBinaryOperator(SyntaxType.PLUS_TOKEN, BoundBinaryOperatorType.Addition,
            new BoundTypeClause(TypeSymbol.Decimal)),
        new BoundBinaryOperator(SyntaxType.MINUS_TOKEN, BoundBinaryOperatorType.Subtraction,
            new BoundTypeClause(TypeSymbol.Decimal)),
        new BoundBinaryOperator(SyntaxType.ASTERISK_TOKEN, BoundBinaryOperatorType.Multiplication,
            new BoundTypeClause(TypeSymbol.Decimal)),
        new BoundBinaryOperator(SyntaxType.SLASH_TOKEN, BoundBinaryOperatorType.Division,
            new BoundTypeClause(TypeSymbol.Decimal)),
        new BoundBinaryOperator(SyntaxType.ASTERISK_ASTERISK_TOKEN, BoundBinaryOperatorType.Power,
            new BoundTypeClause(TypeSymbol.Decimal)),
        new BoundBinaryOperator(SyntaxType.EQUALS_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityEquals,
            new BoundTypeClause(TypeSymbol.Decimal), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.EXCLAMATION_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityNotEquals,
            new BoundTypeClause(TypeSymbol.Decimal), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.LESS_THAN_TOKEN, BoundBinaryOperatorType.LessThan,
            new BoundTypeClause(TypeSymbol.Decimal), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.GREATER_THAN_TOKEN, BoundBinaryOperatorType.GreaterThan,
            new BoundTypeClause(TypeSymbol.Decimal), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.LESS_THAN_EQUALS_TOKEN, BoundBinaryOperatorType.LessOrEqual,
            new BoundTypeClause(TypeSymbol.Decimal), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.GREATER_THAN_EQUALS_TOKEN, BoundBinaryOperatorType.GreatOrEqual,
            new BoundTypeClause(TypeSymbol.Decimal), new BoundTypeClause(TypeSymbol.Bool)),

        // any
        new BoundBinaryOperator(SyntaxType.EXCLAMATION_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityNotEquals,
            new BoundTypeClause(TypeSymbol.Any), new BoundTypeClause(TypeSymbol.Bool)),
        new BoundBinaryOperator(SyntaxType.EQUALS_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityEquals,
            new BoundTypeClause(TypeSymbol.Any), new BoundTypeClause(TypeSymbol.Bool)),
    };

    public static BoundBinaryOperator Bind(SyntaxType type, BoundTypeClause leftType, BoundTypeClause rightType) {
        foreach (var op in operators_) {
            var leftIsCorrect = Cast.Classify(leftType, op.leftType).isImplicit;
            var rightIsCorrect = Cast.Classify(rightType, op.rightType).isImplicit;

            if (op.type == type && leftIsCorrect && rightIsCorrect)
                return op;
        }

        return null;
    }
}

internal sealed class BoundBinaryExpression : BoundExpression {
    public override BoundNodeType type => BoundNodeType.BinaryExpression;
    public override BoundTypeClause typeClause => op.resultType;
    public override BoundConstant constantValue { get; }
    public BoundExpression left { get; }
    public BoundBinaryOperator op { get; }
    public BoundExpression right { get; }

    public BoundBinaryExpression(BoundExpression left_, BoundBinaryOperator op_, BoundExpression right_) {
        left = left_;
        op = op_;
        right = right_;
        constantValue = ConstantFolding.Fold(left, op, right);
    }
}

internal enum BoundUnaryOperatorType {
    Invalid,
    NumericalIdentity,
    NumericalNegation,
    BooleanNegation,
    BitwiseCompliment,
}

internal sealed class BoundUnaryOperator {
    public SyntaxType type { get; }
    public BoundUnaryOperatorType opType { get; }
    public BoundTypeClause operandType { get; }
    public BoundTypeClause resultType { get; }

    private BoundUnaryOperator(
        SyntaxType type_, BoundUnaryOperatorType opType_, BoundTypeClause operandType_, BoundTypeClause resultType_) {
        type = type_;
        opType = opType_;
        operandType = operandType_;
        resultType = resultType_;
    }

    private BoundUnaryOperator(SyntaxType type, BoundUnaryOperatorType opType, BoundTypeClause operandType)
        : this(type, opType, operandType, operandType) { }

    internal static BoundUnaryOperator[] operators_ = {
        // integer
        new BoundUnaryOperator(SyntaxType.PLUS_TOKEN, BoundUnaryOperatorType.NumericalIdentity,
            new BoundTypeClause(TypeSymbol.Int)),
        new BoundUnaryOperator(SyntaxType.MINUS_TOKEN, BoundUnaryOperatorType.NumericalNegation,
            new BoundTypeClause(TypeSymbol.Int)),
        new BoundUnaryOperator(SyntaxType.TILDE_TOKEN, BoundUnaryOperatorType.BitwiseCompliment,
            new BoundTypeClause(TypeSymbol.Int)),

        // boolean
        new BoundUnaryOperator(SyntaxType.EXCLAMATION_TOKEN, BoundUnaryOperatorType.BooleanNegation,
            new BoundTypeClause(TypeSymbol.Bool)),

        // decimal
        new BoundUnaryOperator(SyntaxType.PLUS_TOKEN, BoundUnaryOperatorType.NumericalIdentity,
            new BoundTypeClause(TypeSymbol.Decimal)),
        new BoundUnaryOperator(SyntaxType.MINUS_TOKEN, BoundUnaryOperatorType.NumericalNegation,
            new BoundTypeClause(TypeSymbol.Decimal)),
    };

    public static BoundUnaryOperator Bind(SyntaxType type, BoundTypeClause operandType) {
        foreach (var op in operators_) {
            var operandIsCorrect = Cast.Classify(operandType, op.operandType).isImplicit;

            if (op.type == type && operandIsCorrect)
                return op;
        }

        return null;
    }
}

internal sealed class BoundUnaryExpression : BoundExpression {
    public override BoundNodeType type => BoundNodeType.UnaryExpression;
    public override BoundTypeClause typeClause => op.resultType;
    public override BoundConstant constantValue { get; }
    public BoundUnaryOperator op { get; }
    public BoundExpression operand { get; }

    public BoundUnaryExpression(BoundUnaryOperator op_, BoundExpression operand_) {
        op = op_;
        operand = operand_;
        constantValue = ConstantFolding.ComputeConstant(op, operand);
    }
}

internal abstract class BoundExpression : BoundNode {
    public abstract BoundTypeClause typeClause { get; }
    public virtual BoundConstant constantValue => null;
}

internal sealed class BoundLiteralExpression : BoundExpression {
    public override BoundNodeType type => BoundNodeType.LiteralExpression;
    public override BoundTypeClause typeClause { get; }
    public override BoundConstant constantValue { get; }
    public object value => constantValue.value;

    public BoundLiteralExpression(object value_) {
        if (value_ is bool)
            typeClause = new BoundTypeClause(TypeSymbol.Bool);
        else if (value_ is int)
            typeClause = new BoundTypeClause(TypeSymbol.Int);
        else if (value_ is string)
            typeClause = new BoundTypeClause(TypeSymbol.String);
        else if (value_ is float)
            typeClause = new BoundTypeClause(TypeSymbol.Decimal);
        else if (value_ == null)
            typeClause = new BoundTypeClause(null);
        else
            throw new Exception($"unexpected literal '{value_}' of type '{value_.GetType()}'");

        constantValue = new BoundConstant(value_);
    }

    public BoundLiteralExpression(object value_, BoundTypeClause override_) {
        typeClause = override_;
        constantValue = new BoundConstant(value_);
    }
}

internal sealed class BoundVariableExpression : BoundExpression {
    public VariableSymbol variable { get; }
    public override BoundTypeClause typeClause => variable.typeClause;
    public override BoundNodeType type => BoundNodeType.VariableExpression;
    public override BoundConstant constantValue => variable.constantValue;

    public BoundVariableExpression(VariableSymbol variable_) {
        variable = variable_;
    }
}

internal sealed class BoundAssignmentExpression : BoundExpression {
    public VariableSymbol variable { get; }
    public BoundExpression expression { get; }
    public override BoundNodeType type => BoundNodeType.AssignmentExpression;
    public override BoundTypeClause typeClause => expression.typeClause;

    public BoundAssignmentExpression(VariableSymbol variable_, BoundExpression expression_) {
        variable = variable_;
        expression = expression_;
    }
}

internal sealed class BoundEmptyExpression : BoundExpression {
    public override BoundNodeType type => BoundNodeType.EmptyExpression;
    public override BoundTypeClause typeClause => null;

    public BoundEmptyExpression() { }
}

internal sealed class BoundErrorExpression : BoundExpression {
    public override BoundNodeType type => BoundNodeType.ErrorExpression;
    public override BoundTypeClause typeClause => new BoundTypeClause(null);

    public BoundErrorExpression() { }
}

internal sealed class BoundCallExpression : BoundExpression {
    public FunctionSymbol function { get; }
    public ImmutableArray<BoundExpression> arguments { get; }
    public override BoundNodeType type => BoundNodeType.CallExpression;
    public override BoundTypeClause typeClause => function.typeClause;

    public BoundCallExpression(FunctionSymbol function_, ImmutableArray<BoundExpression> arguments_) {
        function = function_;
        arguments = arguments_;
    }
}

internal sealed class BoundIndexExpression : BoundExpression {
    public BoundExpression expression { get; }
    public BoundExpression index { get; }
    public override BoundNodeType type => BoundNodeType.IndexExpression;
    public override BoundTypeClause typeClause => expression.typeClause.ChildType();

    public BoundIndexExpression(BoundExpression expression_, BoundExpression index_) {
        expression = expression_;
        index = index_;
    }
}

internal sealed class BoundInitializerListExpression : BoundExpression {
    public ImmutableArray<BoundExpression> items { get; }
    public int dimensions { get; }
    public BoundTypeClause itemType { get; }
    public override BoundNodeType type => BoundNodeType.LiteralExpression;
    public override BoundTypeClause typeClause => new BoundTypeClause(
        itemType.lType, itemType.isImplicit, itemType.isConst, itemType.isRef, dimensions);

    public BoundInitializerListExpression(
        ImmutableArray<BoundExpression> items_, int dimensions_, BoundTypeClause itemType_) {
        items = items_;
        dimensions = dimensions_;
        itemType = itemType_;
    }
}

internal sealed class BoundCastExpression : BoundExpression {
    public BoundExpression expression { get; }
    public override BoundNodeType type => BoundNodeType.CastExpression;
    public override BoundTypeClause typeClause { get; }

    public BoundCastExpression(BoundTypeClause typeClause_, BoundExpression expression_) {
        typeClause = typeClause_;

        if (expression_ is BoundLiteralExpression le)
            expression = new BoundLiteralExpression(expression_.constantValue.value, typeClause);
        else
            expression = expression_;
    }
}

internal sealed class BoundCompoundAssignmentExpression : BoundExpression {
    public VariableSymbol variable { get; }
    public BoundBinaryOperator op { get; }
    public BoundExpression expression { get; }
    public override BoundNodeType type => BoundNodeType.CompoundAssignmentExpression;
    public override BoundTypeClause typeClause => expression.typeClause;

    public BoundCompoundAssignmentExpression(
        VariableSymbol variable_, BoundBinaryOperator op_, BoundExpression expression_) {
        variable = variable_;
        op = op_;
        expression = expression_;
    }
}
