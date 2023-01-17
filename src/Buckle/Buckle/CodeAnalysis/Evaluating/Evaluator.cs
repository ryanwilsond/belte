using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;
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
    internal object Evaluate(ref bool abort, out bool hasValue) {
        var function = _program.mainFunction ?? _program.scriptFunction;

        if (function == null) {
            hasValue = false;

            return null;
        }

        var body = LookupMethod(_functions, function);
        var result = EvaluateStatement(body, ref abort, out _);
        hasValue = _hasValue;

        return Value(result, true);
    }

    private object GetVariableValue(VariableSymbol variable, bool traceCollections = false) {
        var value = Get(variable);

        return Value(value, traceCollections);
    }

    private EvaluatorObject GetFrom(Dictionary<VariableSymbol, EvaluatorObject> variables, VariableSymbol variable) {
        foreach (var pair in variables) {
            if (variable.name == pair.Key.name && BoundType.Equals(variable.type, pair.Key.type))
                return pair.Value;
        }

        throw new BelteInternalException($"GetFrom: '{variable.name}' was not found in the scope");
    }

    private EvaluatorObject Get(VariableSymbol variable, Dictionary<VariableSymbol, EvaluatorObject> scope = null) {
        if (scope != null) {
            return GetFrom(scope, variable);
        } else if (variable.kind == SymbolKind.GlobalVariable) {
            return GetFrom(_globals, variable);
        } else {
            foreach (var frame in _locals) {
                try {
                    return GetFrom(frame, variable);
                } catch (BelteInternalException) { }
            }

            // If we get here it means the variable was not found in the local scope, or any direct parent local scopes
            throw new BelteInternalException($"Get: '{variable.name}' was not found in any accessible local scopes");
        }
    }

    private object DictionaryValue(Dictionary<FieldSymbol, EvaluatorObject> value) {
        var dictionary = new Dictionary<object, object>();

        foreach (var pair in value)
            dictionary.Add(pair.Key.name, Value(pair.Value, true));

        return dictionary;
    }

    private object CollectionValue(EvaluatorObject[] value) {
        var builder = new List<object>();

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
        if (left.kind == SymbolKind.GlobalVariable) {
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

        if (right.members == null)
            left.members = null;

        if (right.value == null && right.members != null)
            left.members = Copy(right.members);
        else
            left.value = Value(right);
    }

    private EvaluatorObject EvaluateCast(EvaluatorObject value, BoundType type) {
        var valueValue = Value(value);

        if (value.members != null)
            return value;

        valueValue = CastUtilities.Cast(valueValue, type);

        return new EvaluatorObject(valueValue);
    }

    private EvaluatorObject EvaluateStatement(
        BoundBlockStatement statement, ref bool abort, out bool hasReturn, bool insideTry = false) {
        _hasValue = false;
        hasReturn = false;

        try {
            var labelToIndex = new Dictionary<BoundLabel, int>();

            for (int i=0; i<statement.statements.Length; i++) {
                if (statement.statements[i] is BoundLabelStatement l)
                    labelToIndex.Add(l.label, i + 1);
            }

            var index = 0;

            while (index < statement.statements.Length) {
                if (abort)
                    throw new BelteThreadException();

                var s = statement.statements[index];

                switch (s.kind) {
                    case BoundNodeKind.NopStatement:
                        index++;
                        break;
                    case BoundNodeKind.ExpressionStatement:
                        EvaluateExpressionStatement((BoundExpressionStatement)s, ref abort);
                        index++;
                        break;
                    case BoundNodeKind.VariableDeclarationStatement:
                        EvaluateVariableDeclarationStatement((BoundVariableDeclarationStatement)s, ref abort);
                        index++;
                        break;
                    case BoundNodeKind.GotoStatement:
                        var gs = (BoundGotoStatement)s;
                        index = labelToIndex[gs.label];
                        break;
                    case BoundNodeKind.ConditionalGotoStatement:
                        var cgs = (BoundConditionalGotoStatement)s;
                        var condition = (bool)Value(EvaluateExpression(cgs.condition, ref abort));

                        if (condition == cgs.jumpIfTrue)
                            index = labelToIndex[cgs.label];
                        else
                            index++;

                        break;
                    case BoundNodeKind.LabelStatement:
                        index++;
                        break;
                    case BoundNodeKind.TryStatement:
                        EvaluateTryStatement((BoundTryStatement)s, ref abort, out var returned);

                        if (returned) {
                            hasReturn = true;
                            return _lastValue;
                        }

                        index++;

                        break;
                    case BoundNodeKind.ReturnStatement:
                        var returnStatement = (BoundReturnStatement)s;
                        _lastValue = returnStatement.expression == null
                            ? new EvaluatorObject()
                            : Copy(EvaluateExpression(returnStatement.expression, ref abort));

                        _hasValue =
                            (returnStatement.expression == null || returnStatement.expression is BoundEmptyExpression)
                                ? false : true;

                        hasReturn = true;

                        return _lastValue;
                    default:
                        throw new BelteInternalException($"EvaluateStatement: unexpected statement '{s.kind}'");
                }
            }

            return _lastValue;
        } catch (Exception e) when (e is not BelteInternalException) {
            if (e is BelteThreadException)
                return new EvaluatorObject();

            if (insideTry)
                throw;

            exceptions.Add(e);
            _hasPrint = false;
            _hasValue = false;

            if (!Console.IsOutputRedirected) {
                // TODO Move this logic to the Repl
                if (Console.CursorLeft != 0)
                    Console.WriteLine();

                var previous = Console.ForegroundColor;
                Console.ForegroundColor = ConsoleColor.Red;
                Console.Write($"Unhandled exception ({e.GetType()}): ");
                Console.ForegroundColor = previous;
                Console.WriteLine(e.Message);
            }

            abort = true;
            return new EvaluatorObject();
        }
    }

    private void EvaluateExpressionStatement(BoundExpressionStatement statement, ref bool abort) {
        _lastValue = EvaluateExpression(statement.expression, ref abort);
    }

    private void EvaluateTryStatement(BoundTryStatement statement, ref bool abort, out bool hasReturn) {
        hasReturn = false;

        try {
            EvaluateStatement(statement.body, ref abort, out hasReturn, true);
        } catch (Exception e) when (e is not BelteInternalException) {
            if (statement.catchBody != null && !hasReturn)
                EvaluateStatement(statement.catchBody, ref abort, out hasReturn);
            else
                throw;
        } finally {
            if (statement.finallyBody != null && !hasReturn)
                EvaluateStatement(statement.finallyBody, ref abort, out hasReturn);
        }
    }

    private void EvaluateVariableDeclarationStatement(BoundVariableDeclarationStatement statement, ref bool abort) {
        var value = EvaluateExpression(statement.initializer, ref abort);
        _lastValue = null;
        Create(statement.variable, value);
    }

    private EvaluatorObject EvaluateExpression(BoundExpression node, ref bool abort) {
        if (node.constantValue != null)
            return EvaluateConstantExpression(node, ref abort);

        switch (node.kind) {
            case BoundNodeKind.LiteralExpression:
                if (node is BoundInitializerListExpression il)
                    return new EvaluatorObject(EvaluateInitializerListExpression(il, ref abort));
                else
                    goto default;
            case BoundNodeKind.VariableExpression:
                return EvaluateVariableExpression((BoundVariableExpression)node, ref abort);
            case BoundNodeKind.AssignmentExpression:
                return EvaluateAssignmentExpresion((BoundAssignmentExpression)node, ref abort);
            case BoundNodeKind.UnaryExpression:
                return EvaluateUnaryExpression((BoundUnaryExpression)node, ref abort);
            case BoundNodeKind.BinaryExpression:
                return EvaluateBinaryExpression((BoundBinaryExpression)node, ref abort);
            case BoundNodeKind.TernaryExpression:
                return EvaluateTernaryExpression((BoundTernaryExpression)node, ref abort);
            case BoundNodeKind.CallExpression:
                return EvaluateCallExpression((BoundCallExpression)node, ref abort);
            case BoundNodeKind.CastExpression:
                return EvaluateCastExpression((BoundCastExpression)node, ref abort);
            case BoundNodeKind.IndexExpression:
                return EvaluateIndexExpression((BoundIndexExpression)node, ref abort);
            case BoundNodeKind.ReferenceExpression:
                return EvaluateReferenceExpression((BoundReferenceExpression)node, ref abort);
            case BoundNodeKind.TypeOfExpression:
                return EvaluateTypeOfExpression((BoundTypeOfExpression)node, ref abort);
            case BoundNodeKind.EmptyExpression:
                return new EvaluatorObject();
            case BoundNodeKind.ConstructorExpression:
                return EvaluateConstructorExpression((BoundConstructorExpression)node, ref abort);
            case BoundNodeKind.MemberAccessExpression:
                return EvaluateMemberAccessExpression((BoundMemberAccessExpression)node, ref abort);
            default:
                throw new BelteInternalException($"EvaluateExpression: unexpected node '{node.kind}'");
        }
    }

    private EvaluatorObject EvaluateMemberAccessExpression(BoundMemberAccessExpression node, ref bool abort) {
        var operand = EvaluateExpression(node.operand, ref abort);

        if (operand.isReference) {
            do {
                operand = Get(operand.reference, operand.referenceScope);
            } while (operand.isReference == true);

            return operand.members[node.member];
        } else {
            return operand.members[node.member];
        }
    }

    private EvaluatorObject EvaluateConstructorExpression(BoundConstructorExpression node, ref bool abort) {
        var body = _types[node.symbol];
        var members = new Dictionary<FieldSymbol, EvaluatorObject>();

        foreach (var field in body)
            members.Add(field, new EvaluatorObject());

        return new EvaluatorObject(members);
    }

    private EvaluatorObject EvaluateTypeOfExpression(BoundTypeOfExpression node, ref bool abort) {
        // TODO Implement typeof and type types
        return new EvaluatorObject();
    }

    private EvaluatorObject EvaluateReferenceExpression(BoundReferenceExpression node, ref bool abort) {
        Dictionary<VariableSymbol, EvaluatorObject> referenceScope;

        if (node.variable.kind == SymbolKind.GlobalVariable)
            referenceScope = _globals;
        else
            referenceScope = _locals.Peek();

        return new EvaluatorObject(node.variable, isExplicitReference: true, referenceScope: referenceScope);
    }

    private EvaluatorObject EvaluateIndexExpression(BoundIndexExpression node, ref bool abort) {
        var variable = EvaluateExpression(node.operand, ref abort);
        var index = EvaluateExpression(node.index, ref abort);

        return ((EvaluatorObject[])Value(variable))[(int)Value(index)];
    }

    private EvaluatorObject[] EvaluateInitializerListExpression(BoundInitializerListExpression node, ref bool abort) {
        var builder = new List<EvaluatorObject>();

        foreach (var item in node.items) {
            EvaluatorObject value = EvaluateExpression(item, ref abort);
            builder.Add(value);
        }

        return builder.ToArray();
    }

    private EvaluatorObject EvaluateCastExpression(BoundCastExpression node, ref bool abort) {
        var value = EvaluateExpression(node.expression, ref abort);

        return EvaluateCast(value, node.type);
    }

    private EvaluatorObject EvaluateCallExpression(BoundCallExpression node, ref bool abort) {
        if (node.function.MethodMatches(BuiltinFunctions.Input)) {
            return new EvaluatorObject(Console.ReadLine());
        } else if (node.function.MethodMatches(BuiltinFunctions.Print)) {
            var message = EvaluateExpression(node.arguments[0], ref abort);
            Console.Write(Value(message));
            hasPrint = true;
        } else if (node.function.MethodMatches(BuiltinFunctions.PrintLine)) {
            var message = EvaluateExpression(node.arguments[0], ref abort);
            Console.WriteLine(Value(message));
        } else if (node.function.MethodMatches(BuiltinFunctions.PrintLineNoValue)) {
            Console.WriteLine();
        } else if (node.function.MethodMatches(BuiltinFunctions.RandInt)) {
            var max = (int)Value(EvaluateExpression(node.arguments[0], ref abort));

            if (_random == null)
                _random = new Random();

            return new EvaluatorObject(_random.Next(max));
        } else if (node.function.name == "Value") {
            var value = EvaluateExpression(node.arguments[0], ref abort);
            var hasNoMembers = value.isReference ? Get(value.reference).members == null : value.members == null;

            if (Value(value) == null && hasNoMembers)
                throw new NullReferenceException();

            if (hasNoMembers)
                return new EvaluatorObject(Value(value));
            else
                return Copy(value);
        } else if (node.function.MethodMatches(BuiltinFunctions.HasValue)) {
            var value = EvaluateExpression(node.arguments[0], ref abort);
            var hasNoMembers = value.isReference ? Get(value.reference).members == null : value.members == null;

            if (Value(value) == null && hasNoMembers)
                return new EvaluatorObject(false);

            return new EvaluatorObject(true);
        } else {
            var locals = new Dictionary<VariableSymbol, EvaluatorObject>();

            for (int i=0; i<node.arguments.Length; i++) {
                var parameter = node.function.parameters[i];
                var value = EvaluateExpression(node.arguments[i], ref abort);

                while (!parameter.type.isReference && value.isReference)
                    value = Get(value.reference);

                locals.Add(parameter, Copy(value));
            }

            _locals.Push(locals);
            var statement = LookupMethod(_functions, node.function);
            var result = EvaluateStatement(statement, ref abort, out _);
            _locals.Pop();

            return result;
        }

        return new EvaluatorObject();
    }

    private EvaluatorObject EvaluateConstantExpression(BoundExpression expression, ref bool abort) {
        return EvaluateCast(new EvaluatorObject(expression.constantValue.value), expression.type);
    }

    private EvaluatorObject EvaluateVariableExpression(BoundVariableExpression expression, ref bool abort) {
        return new EvaluatorObject(expression.variable);
    }

    private EvaluatorObject EvaluateAssignmentExpresion(BoundAssignmentExpression expression, ref bool abort) {
        var left = EvaluateExpression(expression.left, ref abort);
        var right = EvaluateExpression(expression.right, ref abort);
        Assign(left, right);

        return right;
    }

    private EvaluatorObject EvaluateUnaryExpression(BoundUnaryExpression expression, ref bool abort) {
        var operand = EvaluateExpression(expression.operand, ref abort);
        var operandValue = Value(operand);

        if (operandValue == null)
            return new EvaluatorObject();

        operandValue = CastUtilities.Cast(operandValue, expression.op.operandType);

        switch (expression.op.opKind) {
            case BoundUnaryOperatorKind.NumericalIdentity:
                if (expression.operand.type.typeSymbol == TypeSymbol.Int)
                    return new EvaluatorObject((int)operandValue);
                else
                    return new EvaluatorObject((double)operandValue);
            case BoundUnaryOperatorKind.NumericalNegation:
                if (expression.operand.type.typeSymbol == TypeSymbol.Int)
                    return new EvaluatorObject(-(int)operandValue);
                else
                    return new EvaluatorObject(-(double)operandValue);
            case BoundUnaryOperatorKind.BooleanNegation:
                return new EvaluatorObject(!(bool)operandValue);
            case BoundUnaryOperatorKind.BitwiseCompliment:
                return new EvaluatorObject(~(int)operandValue);
            default:
                throw new BelteInternalException($"EvaluateUnaryExpression: unknown unary operator '{expression.op}'");
        }
    }

    private EvaluatorObject EvaluateTernaryExpression(BoundTernaryExpression expression, ref bool abort) {
        var left = EvaluateExpression(expression.left, ref abort);
        var leftValue = Value(left);
        leftValue = CastUtilities.Cast(leftValue, expression.op.leftType);

        switch (expression.op.opKind) {
            case BoundTernaryOperatorKind.Conditional:
                // This is so unused sides do not get evaluated (incase they would throw)
                if ((bool)leftValue)
                    return EvaluateExpression(expression.center, ref abort);
                else
                    return EvaluateExpression(expression.right, ref abort);
            default:
                throw new BelteInternalException(
                    $"EvaluateTernaryExpression: unknown ternary operator '{expression.op}'"
                );
        }
    }

    private EvaluatorObject EvaluateBinaryExpression(BoundBinaryExpression expression, ref bool abort) {
        var left = EvaluateExpression(expression.left, ref abort);
        var leftValue = Value(left);

        // Only evaluates right side if necessary
        if (expression.op.opKind == BoundBinaryOperatorKind.ConditionalAnd) {
            if (leftValue == null || !(bool)leftValue)
                return new EvaluatorObject(false);

            var _right = EvaluateExpression(expression.right, ref abort);
            var _rightValue = Value(_right);

            if (_rightValue == null || !(bool)_rightValue)
                return new EvaluatorObject(false);

            return new EvaluatorObject(true);
        }

        if (expression.op.opKind == BoundBinaryOperatorKind.ConditionalOr) {
            if (leftValue != null && (bool)leftValue)
                return new EvaluatorObject(true);

            var _right = EvaluateExpression(expression.right, ref abort);
            var _rightValue = Value(_right);

            if (_rightValue != null && (bool)_rightValue)
                return new EvaluatorObject(true);

            return new EvaluatorObject(false);
        }

        var right = EvaluateExpression(expression.right, ref abort);
        var rightValue = Value(right);

        if (leftValue == null || rightValue == null)
            return new EvaluatorObject();

        var expressionType = expression.type.typeSymbol;
        var leftType = expression.left.type.typeSymbol;
        var rightType = expression.right.type.typeSymbol;

        leftValue = CastUtilities.Cast(leftValue, expression.left.type);
        rightValue = CastUtilities.Cast(rightValue, expression.right.type);

        switch (expression.op.opKind) {
            case BoundBinaryOperatorKind.Addition:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue + (int)rightValue);
                else if (expressionType == TypeSymbol.String)
                    return new EvaluatorObject((string)leftValue + (string)rightValue);
                else
                    return new EvaluatorObject((double)leftValue + (double)rightValue);
            case BoundBinaryOperatorKind.Subtraction:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue - (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue - (double)rightValue);
            case BoundBinaryOperatorKind.Multiplication:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue * (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue * (double)rightValue);
            case BoundBinaryOperatorKind.Division:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue / (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue / (double)rightValue);
            case BoundBinaryOperatorKind.Power:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)Math.Pow((int)leftValue, (int)rightValue));
                else
                    return new EvaluatorObject((double)Math.Pow((double)leftValue, (double)rightValue));
            case BoundBinaryOperatorKind.ConditionalAnd:
                return new EvaluatorObject((bool)leftValue && (bool)rightValue);
            case BoundBinaryOperatorKind.ConditionalOr:
                return new EvaluatorObject((bool)leftValue || (bool)rightValue);
            case BoundBinaryOperatorKind.EqualityEquals:
                return new EvaluatorObject(Equals(leftValue, rightValue));
            case BoundBinaryOperatorKind.EqualityNotEquals:
                return new EvaluatorObject(!Equals(leftValue, rightValue));
            case BoundBinaryOperatorKind.LessThan:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue < (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue < (double)rightValue);
            case BoundBinaryOperatorKind.GreaterThan:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue > (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue > (double)rightValue);
            case BoundBinaryOperatorKind.LessOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue <= (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue <= (double)rightValue);
            case BoundBinaryOperatorKind.GreatOrEqual:
                if (leftType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue >= (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue >= (double)rightValue);
            case BoundBinaryOperatorKind.LogicalAnd:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue & (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue & (bool)rightValue);
            case BoundBinaryOperatorKind.LogicalOr:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue | (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue | (bool)rightValue);
            case BoundBinaryOperatorKind.LogicalXor:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue ^ (int)rightValue);
                else
                    return new EvaluatorObject((bool)leftValue ^ (bool)rightValue);
            case BoundBinaryOperatorKind.LeftShift:
                return new EvaluatorObject((int)leftValue << (int)rightValue);
            case BoundBinaryOperatorKind.RightShift:
                return new EvaluatorObject((int)leftValue >> (int)rightValue);
            case BoundBinaryOperatorKind.UnsignedRightShift:
                return new EvaluatorObject((int)leftValue >>> (int)rightValue);
            case BoundBinaryOperatorKind.Modulo:
                if (expressionType == TypeSymbol.Int)
                    return new EvaluatorObject((int)leftValue % (int)rightValue);
                else
                    return new EvaluatorObject((double)leftValue % (double)rightValue);
            default:
                throw new BelteInternalException(
                    $"EvaluateBinaryExpression: unknown binary operator '{expression.op}'"
                );
        }
    }
}
