using System;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// All binary operator types.
/// </summary>
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

/// <summary>
/// Bound binary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundBinaryOperator {
    private BoundBinaryOperator(
        SyntaxType type, BoundBinaryOperatorType opType,
        BoundTypeClause leftType, BoundTypeClause rightType, BoundTypeClause resultType) {
        this.type = type;
        this.opType = opType;
        this.leftType = leftType;
        this.rightType = rightType;
        typeClause = resultType;
    }

    private BoundBinaryOperator(
        SyntaxType type, BoundBinaryOperatorType opType, BoundTypeClause operandType, BoundTypeClause resultType)
        : this(type, opType, operandType, operandType, resultType) { }

    private BoundBinaryOperator(SyntaxType type, BoundBinaryOperatorType opType, BoundTypeClause typeClause)
        : this(type, opType, typeClause, typeClause, typeClause) { }

    /// <summary>
    /// All defined possible operators, and their operand types.
    /// </summary>
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

    internal SyntaxType type { get; }

    /// <summary>
    /// Operator type.
    /// </summary>
    internal BoundBinaryOperatorType opType { get; }

    /// <summary>
    /// Left side operand type.
    /// </summary>
    internal BoundTypeClause leftType { get; }

    /// <summary>
    /// Right side operand type.
    /// </summary>
    internal BoundTypeClause rightType { get; }

    /// <summary>
    /// Result value type.
    /// </summary>
    internal BoundTypeClause typeClause { get; }

    /// <summary>
    /// Attempts to bind an operator with given sides.
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

/// <summary>
/// A bound binary expression, bound from a parser BinaryExpression.
/// </summary>
internal sealed class BoundBinaryExpression : BoundExpression {
    internal BoundBinaryExpression(
        BoundExpression left, BoundBinaryOperator op, BoundExpression right) {
        this.left = left;
        this.op = op;
        this.right = right;
        constantValue = ConstantFolding.Fold(this.left, this.op, this.right);
    }

    internal override BoundNodeType type => BoundNodeType.BinaryExpression;

    internal override BoundTypeClause typeClause => op.typeClause;

    internal override BoundConstant constantValue { get; }

    internal BoundExpression left { get; }

    internal BoundBinaryOperator op { get; }

    internal BoundExpression right { get; }
}

/// <summary>
/// All unary operator types.
/// </summary>
internal enum BoundUnaryOperatorType {
    Invalid,
    NumericalIdentity,
    NumericalNegation,
    BooleanNegation,
    BitwiseCompliment,
}

/// <summary>
/// Bound unary operator, checks for a known operator otherwise null.
/// </summary>
internal sealed class BoundUnaryOperator {
    private BoundUnaryOperator(
        SyntaxType type, BoundUnaryOperatorType opType, BoundTypeClause operandType, BoundTypeClause resultType) {
        this.type = type;
        this.opType = opType;
        this.operandType = operandType;
        typeClause = resultType;
    }

    private BoundUnaryOperator(SyntaxType type, BoundUnaryOperatorType opType, BoundTypeClause operandType)
        : this(type, opType, operandType, operandType) { }

    /// <summary>
    /// All defined possible operators, and their operand type.
    /// </summary>
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

    internal SyntaxType type { get; }

    /// <summary>
    /// Operator type.
    /// </summary>
    internal BoundUnaryOperatorType opType { get; }

    internal BoundTypeClause operandType { get; }

    /// <summary>
    /// Result value type.
    /// </summary>
    internal BoundTypeClause typeClause { get; }

    /// <summary>
    /// Attempts to bind an operator with given operand.
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

/// <summary>
/// A bound unary expression, bound from a parser UnaryExpression.
/// </summary>
internal sealed class BoundUnaryExpression : BoundExpression {
    internal BoundUnaryExpression(BoundUnaryOperator op, BoundExpression operand) {
        this.op = op;
        this.operand = operand;
        constantValue = ConstantFolding.Fold(this.op, this.operand);
    }

    internal override BoundNodeType type => BoundNodeType.UnaryExpression;

    internal override BoundTypeClause typeClause => op.typeClause;

    internal override BoundConstant constantValue { get; }

    internal BoundUnaryOperator op { get; }

    internal BoundExpression operand { get; }
}

/// <summary>
/// A bound expression, bound from a parser Expression.
/// All expressions have a possible constant value, used for constant folding.
/// If folding is not possible, constant value is null.
/// </summary>
internal abstract class BoundExpression : BoundNode {
    internal abstract BoundTypeClause typeClause { get; }

    internal virtual BoundConstant constantValue => null;
}

/// <summary>
/// A bound literal expression, bound from a parser LiteralExpression.
/// </summary>
internal sealed class BoundLiteralExpression : BoundExpression {
    internal BoundLiteralExpression(object value) {
        if (value is bool)
            typeClause = new BoundTypeClause(TypeSymbol.Bool, isLiteral: true);
        else if (value is int)
            typeClause = new BoundTypeClause(TypeSymbol.Int, isLiteral: true);
        else if (value is string)
            typeClause = new BoundTypeClause(TypeSymbol.String, isLiteral: true);
        else if (value is float)
            typeClause = new BoundTypeClause(TypeSymbol.Decimal, isLiteral: true);
        else if (value == null)
            typeClause = new BoundTypeClause(null, isLiteral: true);
        else
            throw new Exception($"BoundLiteralExpression: unexpected literal '{value}' of type '{value.GetType()}'");

        constantValue = new BoundConstant(value);
    }

    /// <param name="override">Forces a type clause on a value instead of implying</param>
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

/// <summary>
/// A bound variable expression, bound from a parser VariableExpresion.
/// </summary>
internal sealed class BoundVariableExpression : BoundExpression {
    internal BoundVariableExpression(VariableSymbol variable) {
        this.variable = variable;
    }

    internal VariableSymbol variable { get; }

    internal override BoundTypeClause typeClause => variable.typeClause;

    internal override BoundNodeType type => BoundNodeType.VariableExpression;

    internal override BoundConstant constantValue => variable.constantValue;
}

/// <summary>
/// A bound assignment expression, bound from a parser AssignmentExpression.
/// </summary>
internal sealed class BoundAssignmentExpression : BoundExpression {
    internal BoundAssignmentExpression(VariableSymbol variable, BoundExpression expression) {
        this.variable = variable;
        this.expression = expression;
    }

    internal VariableSymbol variable { get; }

    internal BoundExpression expression { get; }

    internal override BoundNodeType type => BoundNodeType.AssignmentExpression;

    internal override BoundTypeClause typeClause => expression.typeClause;
}

/// <summary>
/// A bound inline function expression, bound from a parser InlineFunctionExpression.
/// </summary>
internal sealed class BoundInlineFunctionExpression : BoundExpression {
    internal BoundInlineFunctionExpression(BoundBlockStatement body, BoundTypeClause returnType) {
        this.body = body;
        this.returnType = returnType;
    }

    internal BoundBlockStatement body { get; }

    internal BoundTypeClause returnType { get; }

    internal override BoundNodeType type => BoundNodeType.InlineFunctionExpression;

    internal override BoundTypeClause typeClause => returnType;
}

/// <summary>
/// A bound empty expression, bound from a parser EmptyExpression.
/// Converted to NOP statements eventually.
/// </summary>
internal sealed class BoundEmptyExpression : BoundExpression {
    internal BoundEmptyExpression() { }

    internal override BoundNodeType type => BoundNodeType.EmptyExpression;

    internal override BoundTypeClause typeClause => null;
}

/// <summary>
/// A bound error expression, signally that binding a expression failed.
/// Using at temporary error expression allows catching of future unrelated issues before quitting.
/// Also prevents cascading errors, as if a error expression is seen the Binder ignores it as it knows the error
/// Was already reported.
/// </summary>
internal sealed class BoundErrorExpression : BoundExpression {
    internal BoundErrorExpression() { }

    internal override BoundNodeType type => BoundNodeType.ErrorExpression;

    internal override BoundTypeClause typeClause => new BoundTypeClause(null);
}

/// <summary>
/// A bound call expression, bound from a parser CallExpression.
/// </summary>
internal sealed class BoundCallExpression : BoundExpression {
    internal BoundCallExpression(FunctionSymbol function, ImmutableArray<BoundExpression> arguments) {
        this.function = function;
        this.arguments = arguments;
    }

    internal FunctionSymbol function { get; }

    internal ImmutableArray<BoundExpression> arguments { get; }

    internal override BoundNodeType type => BoundNodeType.CallExpression;

    internal override BoundTypeClause typeClause => function?.typeClause;
}

/// <summary>
/// A bound index expression, bound from a parser IndexExpression.
/// </summary>
internal sealed class BoundIndexExpression : BoundExpression {
    internal BoundIndexExpression(BoundExpression expression, BoundExpression index) {
        this.expression = expression;
        this.index = index;
    }

    internal BoundExpression expression { get; }

    internal BoundExpression index { get; }

    internal override BoundNodeType type => BoundNodeType.IndexExpression;

    internal override BoundTypeClause typeClause => expression.typeClause.ChildType();
}

/// <summary>
/// A bound initializer list expression, bound from a parser InitializerListExpression.
/// </summary>
internal sealed class BoundInitializerListExpression : BoundExpression {
    internal BoundInitializerListExpression(
        ImmutableArray<BoundExpression> items, int dimensions, BoundTypeClause itemType) {
        this.items = items;
        this.dimensions = dimensions;
        this.itemType = itemType;
    }

    internal ImmutableArray<BoundExpression> items { get; }

    internal int dimensions { get; }

    internal BoundTypeClause itemType { get; }

    internal override BoundNodeType type => BoundNodeType.LiteralExpression;

    // TODO Consider factoring out this mass copy into a static method
    // Immutable design makes this required
    internal override BoundTypeClause typeClause => new BoundTypeClause(
        itemType.lType, itemType.isImplicit, itemType.isConstantReference,
        itemType.isReference, itemType.isConstant, true, itemType.isLiteral, dimensions);
}

/// <summary>
/// A bound cast expression, bound from a parser CastExpression.
/// In addition, a bound cast expression can be produced from a call expression using a type name as the function name.
/// E.g. int(3.4)
/// </summary>
internal sealed class BoundCastExpression : BoundExpression {
    internal BoundCastExpression(BoundTypeClause typeClause, BoundExpression expression) {
        this.typeClause = typeClause;

        if (expression is BoundLiteralExpression le)
            this.expression = new BoundLiteralExpression(expression.constantValue.value, this.typeClause);
        else
            this.expression = expression;

        constantValue = this.expression.constantValue;
    }

    internal BoundExpression expression { get; }

    internal override BoundNodeType type => BoundNodeType.CastExpression;

    internal override BoundConstant constantValue { get; }

    internal override BoundTypeClause typeClause { get; }
}

/// <summary>
/// A bound compound assignment expression, bound from a parser CompoundAssignmentExpression.
/// All parser PrefixExpression and PostfixExpressions are converted to bound compound assignment expressions.
/// E.g. x++ -> x+=1
/// </summary>
internal sealed class BoundCompoundAssignmentExpression : BoundExpression {
    internal BoundCompoundAssignmentExpression(
        VariableSymbol variable, BoundBinaryOperator op, BoundExpression expression) {
        this.variable = variable;
        this.op = op;
        this.expression = expression;
    }

    internal VariableSymbol variable { get; }

    internal BoundBinaryOperator op { get; }

    internal BoundExpression expression { get; }

    internal override BoundNodeType type => BoundNodeType.CompoundAssignmentExpression;

    internal override BoundTypeClause typeClause => expression.typeClause;
}

/// <summary>
/// A bound reference expression, bound from a parser ReferenceExpression.
/// </summary>
internal sealed class BoundReferenceExpression : BoundExpression {
    internal BoundReferenceExpression(VariableSymbol variable, BoundTypeClause typeClause) {
        this.variable = variable;
        this.typeClause = typeClause;
    }

    internal VariableSymbol variable { get; }

    internal override BoundNodeType type => BoundNodeType.ReferenceExpression;

    internal override BoundTypeClause typeClause { get; }
}
