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
    Is,
    Isnt,
}

internal sealed class BoundBinaryOperator {
    internal SyntaxType type { get; }
    internal BoundBinaryOperatorType opType { get; }
    internal BoundTypeClause leftType { get; }
    internal BoundTypeClause rightType { get; }
    internal BoundTypeClause typeClause { get; }

    private BoundBinaryOperator(
        SyntaxType type_, BoundBinaryOperatorType opType_,
        BoundTypeClause leftType_, BoundTypeClause rightType_, BoundTypeClause resultType_) {
        type = type_;
        opType = opType_;
        leftType = leftType_;
        rightType = rightType_;
        typeClause = resultType_;
    }

    private BoundBinaryOperator(
        SyntaxType type, BoundBinaryOperatorType opType, BoundTypeClause operandType, BoundTypeClause resultType)
        : this(type, opType, operandType, operandType, resultType) { }

    private BoundBinaryOperator(SyntaxType type, BoundBinaryOperatorType opType, BoundTypeClause typeClause)
        : this(type, opType, typeClause, typeClause, typeClause) { }

    internal static BoundBinaryOperator[] operators_ = {
        // integer
        new BoundBinaryOperator(
            SyntaxType.PLUS_TOKEN, BoundBinaryOperatorType.Addition, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.MINUS_TOKEN, BoundBinaryOperatorType.Subtraction, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.ASTERISK_TOKEN, BoundBinaryOperatorType.Multiplication, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.SLASH_TOKEN, BoundBinaryOperatorType.Division, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.ASTERISK_ASTERISK_TOKEN, BoundBinaryOperatorType.Power, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.AMPERSAND_TOKEN, BoundBinaryOperatorType.LogicalAnd, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.PIPE_TOKEN, BoundBinaryOperatorType.LogicalOr, BoundTypeClause.Int),
        new BoundBinaryOperator(
            SyntaxType.CARET_TOKEN, BoundBinaryOperatorType.LogicalXor, BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxType.LESS_THAN_LESS_THAN_TOKEN, BoundBinaryOperatorType.LeftShift,
            BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxType.GREATER_THAN_GREATER_THAN_TOKEN, BoundBinaryOperatorType.RightShift,
            BoundTypeClause.Int),
        new BoundBinaryOperator(SyntaxType.EQUALS_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityEquals,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.EXCLAMATION_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityNotEquals,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.LESS_THAN_TOKEN, BoundBinaryOperatorType.LessThan,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.GREATER_THAN_TOKEN, BoundBinaryOperatorType.GreaterThan,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.LESS_THAN_EQUALS_TOKEN, BoundBinaryOperatorType.LessOrEqual,
            BoundTypeClause.Int, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.GREATER_THAN_EQUALS_TOKEN, BoundBinaryOperatorType.GreatOrEqual,
            BoundTypeClause.Int, BoundTypeClause.Bool),

        // boolean
        new BoundBinaryOperator(SyntaxType.AMPERSAND_AMPERSAND_TOKEN, BoundBinaryOperatorType.ConditionalAnd,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.PIPE_PIPE_TOKEN, BoundBinaryOperatorType.ConditionalOr,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.AMPERSAND_TOKEN, BoundBinaryOperatorType.LogicalAnd,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.PIPE_TOKEN, BoundBinaryOperatorType.LogicalOr,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.CARET_TOKEN, BoundBinaryOperatorType.LogicalXor,
            BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.EQUALS_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityEquals,
            BoundTypeClause.Bool, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.EXCLAMATION_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityNotEquals,
            BoundTypeClause.Bool, BoundTypeClause.Bool),

        // string
        new BoundBinaryOperator(SyntaxType.PLUS_TOKEN, BoundBinaryOperatorType.Addition,
            BoundTypeClause.String),
        new BoundBinaryOperator(SyntaxType.EQUALS_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityEquals,
            BoundTypeClause.String, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.EXCLAMATION_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityNotEquals,
            BoundTypeClause.String, BoundTypeClause.Bool),

        // decimal
        new BoundBinaryOperator(SyntaxType.PLUS_TOKEN, BoundBinaryOperatorType.Addition,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxType.MINUS_TOKEN, BoundBinaryOperatorType.Subtraction,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxType.ASTERISK_TOKEN, BoundBinaryOperatorType.Multiplication,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxType.SLASH_TOKEN, BoundBinaryOperatorType.Division,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxType.ASTERISK_ASTERISK_TOKEN, BoundBinaryOperatorType.Power,
            BoundTypeClause.Decimal),
        new BoundBinaryOperator(SyntaxType.EQUALS_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityEquals,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.EXCLAMATION_EQUALS_TOKEN, BoundBinaryOperatorType.EqualityNotEquals,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.LESS_THAN_TOKEN, BoundBinaryOperatorType.LessThan,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.GREATER_THAN_TOKEN, BoundBinaryOperatorType.GreaterThan,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.LESS_THAN_EQUALS_TOKEN, BoundBinaryOperatorType.LessOrEqual,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.GREATER_THAN_EQUALS_TOKEN, BoundBinaryOperatorType.GreatOrEqual,
            BoundTypeClause.Decimal, BoundTypeClause.Bool),

        // any
        new BoundBinaryOperator(SyntaxType.IS_KEYWORD, BoundBinaryOperatorType.Is,
            BoundTypeClause.NullableAny, BoundTypeClause.Bool),
        new BoundBinaryOperator(SyntaxType.ISNT_KEYWORD, BoundBinaryOperatorType.Isnt,
            BoundTypeClause.NullableAny, BoundTypeClause.Bool),
    };

    /// <summary>
    /// Attempts to bind an operator with given sides
    /// </summary>
    /// <param name="type">Operator type</param>
    /// <param name="leftType">Left operand type</param>
    /// <param name="rightType">Right operand type</param>
    /// <returns>Bound operator if an operator exists, otherwise null</returns>
    internal static BoundBinaryOperator Bind(SyntaxType type, BoundTypeClause leftType, BoundTypeClause rightType) {
        var nonNullableLeft = BoundTypeClause.NonNullable(leftType);
        var nonNullableRight = BoundTypeClause.NonNullable(rightType);

        foreach (var op in operators_) {
            var leftIsCorrect = Cast.Classify(nonNullableLeft, op.leftType).isImplicit;
            var rightIsCorrect = Cast.Classify(nonNullableRight, op.rightType).isImplicit;

            if (op.type == type && leftIsCorrect && rightIsCorrect)
                return op;
        }

        return null;
    }
}

internal sealed class BoundBinaryExpression : BoundExpression {
    internal override BoundNodeType type => BoundNodeType.BinaryExpression;
    internal override BoundTypeClause typeClause => op.typeClause;
    internal override BoundConstant constantValue { get; }
    internal BoundExpression left { get; }
    internal BoundBinaryOperator op { get; }
    internal BoundExpression right { get; }

    internal BoundBinaryExpression(
        BoundExpression left_, BoundBinaryOperator op_, BoundExpression right_) {
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
    internal SyntaxType type { get; }
    internal BoundUnaryOperatorType opType { get; }
    internal BoundTypeClause operandType { get; }
    internal BoundTypeClause typeClause { get; }

    private BoundUnaryOperator(
        SyntaxType type_, BoundUnaryOperatorType opType_, BoundTypeClause operandType_, BoundTypeClause resultType_) {
        type = type_;
        opType = opType_;
        operandType = operandType_;
        typeClause = resultType_;
    }

    private BoundUnaryOperator(SyntaxType type, BoundUnaryOperatorType opType, BoundTypeClause operandType)
        : this(type, opType, operandType, operandType) { }

    internal static BoundUnaryOperator[] operators_ = {
        // integer
        new BoundUnaryOperator(SyntaxType.PLUS_TOKEN, BoundUnaryOperatorType.NumericalIdentity,
            BoundTypeClause.Int),
        new BoundUnaryOperator(SyntaxType.MINUS_TOKEN, BoundUnaryOperatorType.NumericalNegation,
            BoundTypeClause.Int),
        new BoundUnaryOperator(SyntaxType.TILDE_TOKEN, BoundUnaryOperatorType.BitwiseCompliment,
            BoundTypeClause.Int),

        // boolean
        new BoundUnaryOperator(SyntaxType.EXCLAMATION_TOKEN, BoundUnaryOperatorType.BooleanNegation,
            BoundTypeClause.Bool),

        // decimal
        new BoundUnaryOperator(SyntaxType.PLUS_TOKEN, BoundUnaryOperatorType.NumericalIdentity,
            BoundTypeClause.Decimal),
        new BoundUnaryOperator(SyntaxType.MINUS_TOKEN, BoundUnaryOperatorType.NumericalNegation,
            BoundTypeClause.Decimal),
    };

    /// <summary>
    /// Attempts to bind an operator with given operand
    /// </summary>
    /// <param name="type">Operator type</param>
    /// <param name="operandType">Operand type</param>
    /// <returns>Bound operator if an operator exists, otherwise null</returns>
    internal static BoundUnaryOperator Bind(SyntaxType type, BoundTypeClause operandType) {
        var nonNullableOperand = BoundTypeClause.NonNullable(operandType);

        foreach (var op in operators_) {
            var operandIsCorrect = Cast.Classify(nonNullableOperand, op.operandType).isImplicit;

            if (op.type == type && operandIsCorrect)
                return op;
        }

        return null;
    }
}

internal sealed class BoundUnaryExpression : BoundExpression {
    internal override BoundNodeType type => BoundNodeType.UnaryExpression;
    internal override BoundTypeClause typeClause => op.typeClause;
    internal override BoundConstant constantValue { get; }
    internal BoundUnaryOperator op { get; }
    internal BoundExpression operand { get; }

    internal BoundUnaryExpression(BoundUnaryOperator op_, BoundExpression operand_) {
        op = op_;
        operand = operand_;
        constantValue = ConstantFolding.Fold(op, operand);
    }
}

internal abstract class BoundExpression : BoundNode {
    internal abstract BoundTypeClause typeClause { get; }
    internal virtual BoundConstant constantValue => null;
}

internal sealed class BoundLiteralExpression : BoundExpression {
    internal override BoundNodeType type => BoundNodeType.LiteralExpression;
    internal override BoundTypeClause typeClause { get; }
    internal override BoundConstant constantValue { get; }
    internal object value => constantValue.value;

    internal BoundLiteralExpression(object value_) {
        if (value_ is bool)
            typeClause = new BoundTypeClause(TypeSymbol.Bool, isLiteral_: true);
        else if (value_ is int)
            typeClause = new BoundTypeClause(TypeSymbol.Int, isLiteral_: true);
        else if (value_ is string)
            typeClause = new BoundTypeClause(TypeSymbol.String, isLiteral_: true);
        else if (value_ is float)
            typeClause = new BoundTypeClause(TypeSymbol.Decimal, isLiteral_: true);
        else if (value_ == null)
            typeClause = new BoundTypeClause(null, isLiteral_: true);
        else
            throw new Exception($"BoundLiteralExpression: unexpected literal '{value_}' of type '{value_.GetType()}'");

        constantValue = new BoundConstant(value_);
    }

    internal BoundLiteralExpression(object value_, BoundTypeClause override_) {
        typeClause = new BoundTypeClause(
            override_.lType, override_.isImplicit, override_.isConstantReference, override_.isReference,
            override_.isConstant, override_.isNullable, true, override_.dimensions);

        constantValue = new BoundConstant(value_);
    }
}

internal sealed class BoundVariableExpression : BoundExpression {
    internal VariableSymbol variable { get; }
    internal override BoundTypeClause typeClause => variable.typeClause;
    internal override BoundNodeType type => BoundNodeType.VariableExpression;
    internal override BoundConstant constantValue => variable.constantValue;

    internal BoundVariableExpression(VariableSymbol variable_) {
        variable = variable_;
    }
}

internal sealed class BoundAssignmentExpression : BoundExpression {
    internal VariableSymbol variable { get; }
    internal BoundExpression expression { get; }
    internal override BoundNodeType type => BoundNodeType.AssignmentExpression;
    internal override BoundTypeClause typeClause => expression.typeClause;

    internal BoundAssignmentExpression(VariableSymbol variable_, BoundExpression expression_) {
        variable = variable_;
        expression = expression_;
    }
}

internal sealed class BoundInlineFunctionExpression : BoundExpression {
    internal BoundBlockStatement body { get; }
    internal BoundTypeClause returnType { get; }
    internal override BoundNodeType type => BoundNodeType.InlineFunctionExpression;
    internal override BoundTypeClause typeClause => returnType;

    internal BoundInlineFunctionExpression(BoundBlockStatement body_, BoundTypeClause returnType_) {
        body = body_;
        returnType = returnType_;
    }
}

internal sealed class BoundEmptyExpression : BoundExpression {
    internal override BoundNodeType type => BoundNodeType.EmptyExpression;
    internal override BoundTypeClause typeClause => null;

    internal BoundEmptyExpression() { }
}

internal sealed class BoundErrorExpression : BoundExpression {
    internal override BoundNodeType type => BoundNodeType.ErrorExpression;
    internal override BoundTypeClause typeClause => new BoundTypeClause(null);

    internal BoundErrorExpression() { }
}

internal sealed class BoundCallExpression : BoundExpression {
    internal FunctionSymbol function { get; }
    internal ImmutableArray<BoundExpression> arguments { get; }
    internal override BoundNodeType type => BoundNodeType.CallExpression;
    internal override BoundTypeClause typeClause => function.typeClause;

    internal BoundCallExpression(FunctionSymbol function_, ImmutableArray<BoundExpression> arguments_) {
        function = function_;
        arguments = arguments_;
    }
}

internal sealed class BoundIndexExpression : BoundExpression {
    internal BoundExpression expression { get; }
    internal BoundExpression index { get; }
    internal override BoundNodeType type => BoundNodeType.IndexExpression;
    internal override BoundTypeClause typeClause => expression.typeClause.ChildType();

    internal BoundIndexExpression(BoundExpression expression_, BoundExpression index_) {
        expression = expression_;
        index = index_;
    }
}

internal sealed class BoundInitializerListExpression : BoundExpression {
    internal ImmutableArray<BoundExpression> items { get; }
    internal int dimensions { get; }
    internal BoundTypeClause itemType { get; }
    internal override BoundNodeType type => BoundNodeType.LiteralExpression;
    // TODO Consider factoring out this mass copy into a static method
    // Immutable design makes this required
    internal override BoundTypeClause typeClause => new BoundTypeClause(
        itemType.lType, itemType.isImplicit, itemType.isConstantReference,
        itemType.isReference, itemType.isConstant, true, itemType.isLiteral, dimensions);

    internal BoundInitializerListExpression(
        ImmutableArray<BoundExpression> items_, int dimensions_, BoundTypeClause itemType_) {
        items = items_;
        dimensions = dimensions_;
        itemType = itemType_;
    }
}

internal sealed class BoundCastExpression : BoundExpression {
    internal BoundExpression expression { get; }
    internal override BoundNodeType type => BoundNodeType.CastExpression;
    internal override BoundConstant constantValue { get; }
    internal override BoundTypeClause typeClause { get; }

    internal BoundCastExpression(BoundTypeClause typeClause_, BoundExpression expression_) {
        typeClause = typeClause_;

        if (expression_ is BoundLiteralExpression le)
            expression = new BoundLiteralExpression(expression_.constantValue.value, typeClause);
        else
            expression = expression_;

        constantValue = expression.constantValue;
    }
}

internal sealed class BoundCompoundAssignmentExpression : BoundExpression {
    internal VariableSymbol variable { get; }
    internal BoundBinaryOperator op { get; }
    internal BoundExpression expression { get; }
    internal override BoundNodeType type => BoundNodeType.CompoundAssignmentExpression;
    internal override BoundTypeClause typeClause => expression.typeClause;

    internal BoundCompoundAssignmentExpression(
        VariableSymbol variable_, BoundBinaryOperator op_, BoundExpression expression_) {
        variable = variable_;
        op = op_;
        expression = expression_;
    }
}

internal sealed class BoundReferenceExpression : BoundExpression {
    internal VariableSymbol variable { get; }
    internal override BoundNodeType type => BoundNodeType.ReferenceExpression;
    internal override BoundTypeClause typeClause { get; }

    internal BoundReferenceExpression(VariableSymbol variable_, BoundTypeClause typeClause_) {
        variable = variable_;
        typeClause = typeClause_;
    }
}
