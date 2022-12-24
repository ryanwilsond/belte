using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;
using static Buckle.Utilities.FunctionUtilities;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Evaluates BoundStatements inline similar to an interpreter.
/// </summary>
internal sealed class Evaluator {
    private readonly BoundProgram _program;
    private readonly Dictionary<FunctionSymbol, BoundBlockStatement> _functions =
        new Dictionary<FunctionSymbol, BoundBlockStatement>();
    private readonly Dictionary<TypeSymbol, ImmutableList<FieldSymbol>> _types =
        new Dictionary<TypeSymbol, ImmutableList<FieldSymbol>>();
    private readonly Dictionary<VariableSymbol, EvaluatorObject> _globals;
    private readonly Stack<Dictionary<VariableSymbol, EvaluatorObject>> _locals =
        new Stack<Dictionary<VariableSymbol, EvaluatorObject>>();

    /// <summary>
    /// Creates an <see cref="Evaluator" /> that can evaluate a <see cref="BoundProgram" /> (provided globals).
    /// </summary>
    /// <param name="program"><see cref="BoundProgram" />.</param>
    /// <param name="globals">Globals.</param>
    internal Evaluator(BoundProgram program, Dictionary<VariableSymbol, EvaluatorObject> globals) {
        diagnostics = new BelteDiagnosticQueue();
        exceptions = new List<Exception>();
        _program = program;
        _globals = globals;
        _locals.Push(new Dictionary<VariableSymbol, EvaluatorObject>());

        var current = program;
        while (current != null) {
            foreach (var (function, body) in current.functionBodies)
                _functions.Add(function, body);

            foreach (var (@struct, body) in current.structMembers)
                // Because structs do not store their declarations, shadowing ones have the same key
                // As what they are shadowing, so this will just update instead of adding and throwing
                _types[@struct] = body;

            current = current.previous;
        }
    }

    /// <summary>
    /// If it has a Print statement, adds a line break to avoid formatting issues
    /// (mostly for the <see cref="BelteRepl" />).
    /// </summary>
    internal bool hasPrint {
        get {
            return _hasPrint;
        } set {
            _hasPrint = value;
        }
    }

    /// <summary>
    /// All thrown exceptions during evaluation.
    /// </summary>
    internal List<Exception> exceptions { get; set; }

    /// <summary>
    /// Diagnostics specific to the <see cref="Evaluator" />.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    private EvaluatorObject _lastValue;
    private Random _random;
    private bool _hasPrint = false;
    private bool _hasValue = false;

    /// <summary>
    /// Evaluate the provided <see cref="BoundProgram" />.
    /// </summary>
    /// <returns>Result of <see cref="BoundProgram" /> (if applicable).</returns>
    internal object Evaluate(out bool hasValue) {
        var function = _program.mainFunction ?? _program.scriptFunction;
        if (function == null) {
            hasValue = false;
            return null;
        }

        var body = LookupMethod(_functions, function);
        var result = EvaluateStatement(body);
        hasValue = _hasValue;

        return Value(result, true);
    }

    private object GetVariableValue(VariableSymbol variable, bool traceCollections = false) {
        var value = Get(variable);

        return Value(value, traceCollections);
    }

    private EvaluatorObject GetFrom(Dictionary<VariableSymbol, EvaluatorObject> variables, VariableSymbol variable) {
        foreach (var pair in variables)
            if (variable.name == pair.Key.name && BoundTypeClause.Equals(variable.typeClause, pair.Key.typeClause))
                return pair.Value;

        throw new BelteInternalException($"GetFrom: '{variable.name}' was not found in the scope");
    }

    private EvaluatorObject Get(VariableSymbol variable) {
        if (variable.type == SymbolType.GlobalVariable) {
            return GetFrom(_globals, variable);
        } else {
            var locals = _locals.Peek();
            return GetFrom(locals, variable);
        }
    }

    private object DictionaryValue(Dictionary<FieldSymbol, EvaluatorObject> value) {
        var dictionary = new Dictionary<object, object>();

        foreach (var pair in value)
            dictionary.Add(pair.Key.name, Value(pair.Value, true));

        return dictionary;
    }

    private object CollectionValue(EvaluatorObject[] value) {
        var builder = new List<Object>();

        foreach (var item in value)
            builder.Add(Value(item, true));

        return builder.ToArray();
    }

    private object Value(EvaluatorObject value, bool traceCollections = false) {
        if (value.isReference)
            return GetVariableValue(value.reference, traceCollections);
        else if (value.value is EvaluatorObject)
            return Value(value.value as EvaluatorObject, traceCollections);
        else if (value.value is EvaluatorObject[] && traceCollections)
            return CollectionValue(value.value as EvaluatorObject[]);
        else if (traceCollections && value.value == null && value.members != null)
            return DictionaryValue(value.members);
        else
            return value.value;
    }

    private EvaluatorObject Copy(EvaluatorObject value) {
        if (value.reference != null && value.isExplicitReference == false)
            return Copy(Get(value.reference));
        else if (value.reference != null)
            return new EvaluatorObject(value.reference, isExplicitReference: true);
        else if (value.members != null)
            return new EvaluatorObject(Copy(value.members));
        else
            return new EvaluatorObject(value.value);
    }

    private Dictionary<FieldSymbol, EvaluatorObject> Copy(Dictionary<FieldSymbol, EvaluatorObject> members) {
        var newMembers = new Dictionary<FieldSymbol, EvaluatorObject>();

        foreach (var member in members)
            newMembers.Add(member.Key, Copy(member.Value));

        return newMembers;
    }

    private void Create(VariableSymbol left, EvaluatorObject right) {
        if (left.type == SymbolType.GlobalVariable) {
            var set = false;

            foreach (var global in _globals) {
                if (global.Key.name == left.name) {
                    _globals.Remove(global.Key);
                    _globals[left] = Copy(right);
                    set = true;
                    break;
                }
            }

            if (!set)
                _globals[left] = Copy(right);
        } else {
            var locals = _locals.Peek();
            var set = false;

            foreach (var local in locals) {
                if (local.Key.name == left.name) {
                    locals.Remove(local.Key);
                    locals[left] = Copy(right);
                    set = true;
                    break;
                }
            }

            if (!set)
                locals[left] = Copy(right);
        }
    }

    private void Assign(EvaluatorObject left, EvaluatorObject right) {
        while (right.isReference && !right.isExplicitReference)
            right = Get(right.reference);

        while (left.isReference && !left.isExplicitReference)
            left = Get(left.reference);

        if (right.isExplicitReference) {
            left.reference = right.reference;
            return;
        } else if (left.isExplicitReference) {
            while (left.isReference)
                left = Get(left.reference);
        }

        if (right.value == null && right.members != null) {
            left.members = Copy(right.members);
        } else {
            left.value = Value(right);
        }
    }

    private EvaluatorObject EvaluateStatement(BoundBlockStatement statement) {
        _hasValue = false;

        try {
            var labelToIndex = new Dictionary<BoundLabel, int>();

            for (int i=0; i<statement.statements.Length; i++) {
                if (statement.statements[i] is BoundLabelStatement l)
                    labelToIndex.Add(l.label, i + 1);
            }

            var index = 0;
            while (index < statement.statements.Length) {
                var s = statement.statements[index];

                switch (s.type) {
                    case BoundNodeType.NopStatement:
                        index++;
                        break;
                    case BoundNodeType.ExpressionStatement:
                        _hasValue = true;
                        EvaluateExpressionStatement((BoundExpressionStatement)s);
                        index++;
                        break;
                    case BoundNodeType.VariableDeclarationStatement:
                        EvaluateVariableDeclarationStatement((BoundVariableDeclarationStatement)s);
                        index++;
                        break;
                    case BoundNodeType.GotoStatement:
                        var gs = (BoundGotoStatement)s;
                        index = labelToIndex[gs.label];
                        break;
                    case BoundNodeType.ConditionalGotoStatement:
                        var cgs = (BoundConditionalGotoStatement)s;
                        var condition = (bool)EvaluateExpression(cgs.condition).value;

                        if (condition == cgs.jumpIfTrue)
                            index = labelToIndex[cgs.label];
                        else
                            index++;

                        break;
                    case BoundNodeType.LabelStatement:
                        index++;
                        break;
                    case BoundNodeType.ReturnStatement:
                        var returnStatement = (BoundReturnStatement)s;
                        var _lastValue = returnStatement.expression == null
                            ? new EvaluatorObject()
                            : Copy(EvaluateExpression(returnStatement.expression));

                        _hasValue = returnStatement.expression == null ? false : true;
                        return _lastValue;
                    default:
                        throw new BelteInternalException($"EvaluateStatement: unexpected statement '{s.type}'");
                }
            }

            return _lastValue;
        } catch (Exception e) when (!(e is BelteInternalException)) {
            exceptions.Add(e);
            var previous = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("Unhandled exception: ");
            Console.ForegroundColor = previous;
            Console.WriteLine(e.Message);
            _hasValue = false;
            return new EvaluatorObject();
        }
    }

    private void EvaluateExpressionStatement(BoundExpressionStatement statement) {
        _lastValue = EvaluateExpression(statement.expression);
    }

    private void EvaluateVariableDeclarationStatement(BoundVariableDeclarationStatement statement) {
        var value = EvaluateExpression(statement.initializer);
        _lastValue = null;
        Create(statement.variable, value);
    }

    private EvaluatorObject EvaluateExpression(BoundExpression node) {
        if (node.constantValue != null)
            return EvaluateConstantExpression(node);

        switch (node.type) {
            case BoundNodeType.LiteralExpression:
                if (node is BoundInitializerListExpression il)
                    return new EvaluatorObject(EvaluateInitializerListExpression(il));
                else
                    goto default;
            case BoundNodeType.VariableExpression:
                return EvaluateVariableExpression((BoundVariableExpression)node);
            case BoundNodeType.AssignmentExpression:
                return EvaluateAssignmentExpresion((BoundAssignmentExpression)node);
            case BoundNodeType.UnaryExpression:
                return EvaluateUnaryExpression((BoundUnaryExpression)node);
            case BoundNodeType.BinaryExpression:
                return EvaluateBinaryExpression((BoundBinaryExpression)node);
            case BoundNodeType.TernaryExpression:
                return EvaluateTernaryExpression((BoundTernaryExpression)node);
            case BoundNodeType.CallExpression:
                return EvaluateCallExpression((BoundCallExpression)node);
            case BoundNodeType.CastExpression:
                return EvaluateCastExpression((BoundCastExpression)node);
            case BoundNodeType.IndexExpression:
                return EvaluateIndexExpression((BoundIndexExpression)node);
            case BoundNodeType.ReferenceExpression:
                return EvaluateReferenceExpression((BoundReferenceExpression)node);
            case BoundNodeType.TypeOfExpression:
                return EvaluateTypeOfExpression((BoundTypeOfExpression)node);
            case BoundNodeType.EmptyExpression:
                return new EvaluatorObject();
            case BoundNodeType.ConstructorExpression:
                return EvaluateConstructorExpression((BoundConstructorExpression)node);
            case BoundNodeType.MemberAccessExpression:
                return EvaluateMemberAccessExpression((BoundMemberAccessExpression)node);
            default:
                throw new BelteInternalException($"EvaluateExpression: unexpected node '{node.type}'");
        }
    }

    private EvaluatorObject EvaluateMemberAccessExpression(BoundMemberAccessExpression node) {
        var operand = EvaluateExpression(node.operand);

        if (operand.isReference)
            return Get(operand.reference).members[node.member];
        else
            return operand.members[node.member];
    }

    private EvaluatorObject EvaluateConstructorExpression(BoundConstructorExpression node) {
        var body = _types[node.symbol];
        var members = new Dictionary<FieldSymbol, EvaluatorObject>();

        foreach (var field in body)
            members.Add(field, new EvaluatorObject());

        return new EvaluatorObject(members);
    }

    private EvaluatorObject EvaluateTypeOfExpression(BoundTypeOfExpression node) {
        // TODO Implement typeof and type types
        return new EvaluatorObject();
    }

    private EvaluatorObject EvaluateReferenceExpression(BoundReferenceExpression node) {
        return new EvaluatorObject(node.variable, isExplicitReference: true);
    }

    private EvaluatorObject EvaluateIndexExpression(BoundIndexExpression node) {
        var variable = EvaluateExpression(node.expression);
        var index = EvaluateExpression(node.index);

        return ((EvaluatorObject[])Value(variable))[(int)Value(index)];
    }

    private EvaluatorObject[] EvaluateInitializerListExpression(BoundInitializerListExpression node) {
        var builder = new List<EvaluatorObject>();

        foreach (var item in node.items) {
            EvaluatorObject value = EvaluateExpression(item);
            builder.Add(value);
        }

        return builder.ToArray();
    }

    private EvaluatorObject EvaluateCastExpression(BoundCastExpression node) {
        var value = EvaluateExpression(node.expression);

        return EvaluateCast(value, node.typeClause);
    }

    private EvaluatorObject EvaluateCast(EvaluatorObject value, BoundTypeClause typeClause) {
        var valueValue = Value(value);

        if (valueValue == null)
            return value;

        var type = typeClause.lType;
        valueValue = CastUtilities.Cast(valueValue, type);
        return new EvaluatorObject(valueValue);
    }

    private EvaluatorObject EvaluateCallExpression(BoundCallExpression node) {
        if (node.function.MethodMatches(BuiltinFunctions.Input)) {
            return new EvaluatorObject(Console.ReadLine());
        } else if (node.function.MethodMatches(BuiltinFunctions.Print)) {
            var message = EvaluateExpression(node.arguments[0]);
            Console.Write(Value(message));
            hasPrint = true;
        } else if (node.function.MethodMatches(BuiltinFunctions.PrintLine)) {
            var message = EvaluateExpression(node.arguments[0]);
            Console.WriteLine(Value(message));
        } else if (node.function.MethodMatches(BuiltinFunctions.RandInt)) {
            var max = (int)Value(EvaluateExpression(node.arguments[0]));

            if (_random == null)
                _random = new Random();

            return new EvaluatorObject(_random.Next(max));
        } else if (node.function.name == "Value") {
            EvaluatorObject? value = EvaluateExpression(node.arguments[0]);

            if (Value(value) == null)
                throw new NullReferenceException();

            return new EvaluatorObject(Value(value));
        } else if (node.function.MethodMatches(BuiltinFunctions.HasValue)) {
            EvaluatorObject? value = EvaluateExpression(node.arguments[0]);

            if (Value(value) == null)
                return new EvaluatorObject(false);

            return new EvaluatorObject(true);
        } else {
            var locals = new Dictionary<VariableSymbol, EvaluatorObject>();

            for (int i=0; i<node.arguments.Length; i++) {
                var parameter = node.function.parameters[i];
                var value = EvaluateExpression(node.arguments[i]);
                locals.Add(parameter, value);
            }

            _locals.Push(locals);
            var statement = LookupMethod(_functions, node.function);
            var result = EvaluateStatement(statement);
            _locals.Pop();

            return result;
        }

        return new EvaluatorObject();
    }

    private EvaluatorObject EvaluateConstantExpression(BoundExpression expression) {
        return EvaluateCast(new EvaluatorObject(expression.constantValue.value), expression.typeClause);
    }

    private EvaluatorObject EvaluateVariableExpression(BoundVariableExpression expression) {
        return new EvaluatorObject(expression.variable);
    }

    private EvaluatorObject EvaluateAssignmentExpresion(BoundAssignmentExpression expression) {
        var left = EvaluateExpression(expression.left);
        var right = EvaluateExpression(expression.right);
        Assign(left, right);

        return right;
    }

    private EvaluatorObject EvaluateUnaryExpression(BoundUnaryExpression expression) {
        var operand = EvaluateExpression(expression.operand);
        var operandValue = Value(operand);

        if (operandValue == null)
            return new EvaluatorObject();

        operandValue = CastUtilities.Cast(operandValue, expression.op.operandType.lType);

        switch (expression.op.opType) {
            case BoundUnaryOperatorType.NumericalIdentity:
                if (expression.operand.typeClause.lType == TypeSymbol.Int)
                    return new EvaluatorObject((int)operandValue);
                else
                    return new EvaluatorObject((double)operandValue);
            case BoundUnaryOperatorType.NumericalNegation:
                if (expression.operand.typeClause.lType == TypeSymbol.Int)
                    return new EvaluatorObject(-(int)operandValue);
                else
                    return new EvaluatorObject(-(double)operandValue);
            case BoundUnaryOperatorType.BooleanNegation:
                return new EvaluatorObject(!(bool)operandValue);
            case BoundUnaryOperatorType.BitwiseCompliment:
                return new EvaluatorObject(~(int)operandValue);
            default:
                throw new BelteInternalException($"EvaluateUnaryExpression: unknown unary operator '{expression.op}'");
        }
    }

    private EvaluatorObject EvaluateTernaryExpression(BoundTernaryExpression expression) {
        var left = EvaluateExpression(expression.left);
        var leftValue = Value(left);
        leftValue = CastUtilities.Cast(leftValue, expression.op.leftType.lType);

        switch (expression.op.opType) {
            case BoundTernaryOperatorType.Conditional:
                // This is so unused sides do not get evaluated (incase they would throw)
                if ((bool)leftValue)
                    return EvaluateExpression(expression.center);
                else
                    return EvaluateExpression(expression.right);
            default:
                throw new BelteInternalException(
                    $"EvaluateTernaryExpression: unknown ternary operator '{expression.op}'");
        }
    }

    private EvaluatorObject EvaluateBinaryExpression(BoundBinaryExpression expression) {
        var left = EvaluateExpression(expression.left);
        var leftValue = Value(left);

        // Only evaluates right side if necessary
        if (expression.op.opType == BoundBinaryOperatorType.ConditionalAnd) {
            if (leftValue == null || !(bool)leftValue)
                return new EvaluatorObject(false);

            var _right = EvaluateExpression(expression.right);
            var _rightValue = Value(_right);

            if (_rightValue == null || !(bool)_rightValue)
                return new EvaluatorObject(false);

            return new EvaluatorObject(true);
        }

        if (expression.op.opType == BoundBinaryOperatorType.ConditionalOr) {
            if (leftValue != null && (bool)leftValue)
                return new EvaluatorObject(true);

            var _right = EvaluateExpression(expression.right);
            var _rightValue = Value(_right);

            if (_rightValue != null && (bool)_rightValue)
                return new EvaluatorObject(true);

            return new EvaluatorObject(false);
        }

        var right = EvaluateExpression(expression.right);
        var rightValue = Value(right);

        if (leftValue == null || rightValue == null)
            return new EvaluatorObject();

        var expressionType = expression.typeClause.lType;
        var leftType = expression.left.typeClause.lType;
        var rightType = expression.right.typeClause.lType;

        leftValue = CastUtilities.Cast(leftValue, expression.op.leftType.lType);
        rightValue = CastUtilities.Cast(rightValue, expression.op.rightType.lType);

        switch (expression.op.opType) {
            case BoundBinaryOperatorType.Addition:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue + (int)rightValue);
                else if (expressionType == TypeSymbol.String)
                    return new EvaluatorObject((string)leftValue + (string)rightValue);
                else
                    return new EvaluatorObject((double)leftValue + (double)rightValue);
            case BoundBinaryOperatorType.Subtraction:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue - (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue - (double)rightValue);
            case BoundBinaryOperatorType.Multiplication:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue * (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue * (double)rightValue);
            case BoundBinaryOperatorType.Division:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue / (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue / (double)rightValue);
            case BoundBinaryOperatorType.Power:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)Math.Pow((int)leftValue, (int)rightValue));
                else
                    return new EvaluatorObject((double)Math.Pow((double)leftValue, (double)rightValue));
            case BoundBinaryOperatorType.ConditionalAnd:
                return new EvaluatorObject((bool)leftValue && (bool)rightValue);
            case BoundBinaryOperatorType.ConditionalOr:
                return new EvaluatorObject((bool)leftValue || (bool)rightValue);
            case BoundBinaryOperatorType.EqualityEquals:
                return new EvaluatorObject(Equals(leftValue, rightValue));
            case BoundBinaryOperatorType.EqualityNotEquals:
                return new EvaluatorObject(!Equals(leftValue, rightValue));
            case BoundBinaryOperatorType.LessThan:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue < (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue < (double)rightValue);
            case BoundBinaryOperatorType.GreaterThan:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue > (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue > (double)rightValue);
            case BoundBinaryOperatorType.LessOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue <= (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue <= (double)rightValue);
            case BoundBinaryOperatorType.GreatOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue >= (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue >= (double)rightValue);
            case BoundBinaryOperatorType.LogicalAnd:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue & (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue & (bool)rightValue);
            case BoundBinaryOperatorType.LogicalOr:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue | (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue | (bool)rightValue);
            case BoundBinaryOperatorType.LogicalXor:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue ^ (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue ^ (bool)rightValue);
            case BoundBinaryOperatorType.LeftShift:
                return new EvaluatorObject((int)leftValue << (int)rightValue);
            case BoundBinaryOperatorType.RightShift:
                return new EvaluatorObject((int)leftValue >> (int)rightValue);
            case BoundBinaryOperatorType.UnsignedRightShift:
                return new EvaluatorObject((int)leftValue >>> (int)rightValue);
            case BoundBinaryOperatorType.Modulo:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue % (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue % (double)rightValue);
            default:
                throw new BelteInternalException(
                    $"EvaluateBinaryExpression: unknown binary operator '{expression.op}'");
        }
    }
}
