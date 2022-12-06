using System;
using System.Collections.Generic;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utils;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Evaluates statements as an interpreter, inline.
/// </summary>
internal sealed class Evaluator {
    private readonly BoundProgram program_;
    private readonly Dictionary<VariableSymbol, object> globals_;
    private readonly Dictionary<FunctionSymbol, BoundBlockStatement> functions_ =
        new Dictionary<FunctionSymbol, BoundBlockStatement>();
    private readonly Stack<Dictionary<VariableSymbol, object>> locals_ =
        new Stack<Dictionary<VariableSymbol, object>>();
    private object lastValue_;
    private Random random_;
    private bool hasPrint_ = false;

    /// <summary>
    /// Creates an evaluator that can evaluate a program (provided globals).
    /// </summary>
    /// <param name="program">Program</param>
    /// <param name="globals">Globals</param>
    internal Evaluator(BoundProgram program, Dictionary<VariableSymbol, object> globals) {
        diagnostics = new BelteDiagnosticQueue();
        program_ = program;
        globals_ = globals;
        locals_.Push(new Dictionary<VariableSymbol, object>());

        var current = program;
        while (current != null) {
            foreach (var (function, body) in current.functionBodies)
                functions_.Add(function, body);

            current = current.previous;
        }
    }

    /// <summary>
    /// If it has a Print statement, adds a line break to avoid formatting issues (mostly with the REPL).
    /// </summary>
    internal bool hasPrint {
        get {
            return hasPrint_;
        } set {
            hasPrint_ = value;
        }
    }

    /// <summary>
    /// Diagnostics specific to the evaluator.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// Evaluate the provided program.
    /// </summary>
    /// <returns>Result of program (if applicable)</returns>
    internal object Evaluate() {
        var function = program_.mainFunction ?? program_.scriptFunction;
        if (function == null)
            return null;

        var body = LookupMethod(function);
        return EvaluateStatement(body);
    }

    private object EvaluateStatement(BoundBlockStatement statement) {
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
                        var condition = (bool)EvaluateExpression(cgs.condition);

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
                        var lastValue_ = returnStatement.expression == null
                            ? null
                            : EvaluateExpression(returnStatement.expression);

                        return lastValue_;
                    default:
                        throw new Exception($"EvaluateStatement: unexpected statement '{s.type}'");
                }
            }

            return lastValue_;
        } catch (Exception e) {
            var previous = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Write("Unhandled exception: ");
            Console.ForegroundColor = previous;
            Console.WriteLine(e.Message);
            return null;
        }
    }

    private void EvaluateExpressionStatement(BoundExpressionStatement statement) {
        lastValue_ = EvaluateExpression(statement.expression);
    }

    private void EvaluateVariableDeclarationStatement(BoundVariableDeclarationStatement statement) {
        var value = EvaluateExpression(statement.initializer);
        lastValue_ = null;
        Assign(statement.variable, value);
    }

    private void Assign(VariableSymbol variable, object value) {
        if (variable.type == SymbolType.GlobalVariable) {
            globals_[variable] = value;
        } else {
            var locals = locals_.Peek();
            locals[variable] = value;
        }
    }

    private object EvaluateExpression(BoundExpression node) {
        if (node.constantValue != null)
            return EvaluateConstantExpression(node);

        switch (node.type) {
            case BoundNodeType.LiteralExpression:
                if (node is BoundInitializerListExpression il)
                    return EvaluateInitializerListExpression(il);
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
            case BoundNodeType.CallExpression:
                return EvaluateCallExpression((BoundCallExpression)node);
            case BoundNodeType.CastExpression:
                return EvaluateCastExpression((BoundCastExpression)node);
            case BoundNodeType.IndexExpression:
                return EvaluateIndexExpression((BoundIndexExpression)node);
            case BoundNodeType.ReferenceExpression:
                return EvaluateReferenceExpression((BoundReferenceExpression)node);
            case BoundNodeType.TypeofExpression:
                return EvaluateTypeofExpression((BoundTypeofExpression)node);
            case BoundNodeType.EmptyExpression:
                return null;
            default:
                throw new Exception($"EvaluateExpression: unexpected node '{node.type}'");
        }
    }

    private object EvaluateTypeofExpression(BoundTypeofExpression node) {
        // TODO Implement typeof and type types
        return null;
    }

    private object EvaluateReferenceExpression(BoundReferenceExpression node) {
        // TODO Implement references in the Evaluator
        return null;
    }

    private object EvaluateIndexExpression(BoundIndexExpression node) {
        var variable = EvaluateExpression(node.expression);
        var index = EvaluateExpression(node.index);

        return ((object[])variable)[(int)index];
    }

    private object EvaluateInitializerListExpression(BoundInitializerListExpression node) {
        var builder = new List<object>();

        foreach (var item in node.items) {
            object value = EvaluateExpression(item);
            builder.Add(value);
        }

        return builder.ToArray();
    }

    private object EvaluateCastExpression(BoundCastExpression node) {
        var value = EvaluateExpression(node.expression);

        return EvaluateCast(value, node.typeClause);
    }

    private object EvaluateCast(object value, BoundTypeClause typeClause) {
        if (value == null)
            return null;

        var type = typeClause.lType;

        if (type == TypeSymbol.Any) {
            return value;
        } else if (type == TypeSymbol.Bool) {
            return Convert.ToBoolean(value);
        } else if (type == TypeSymbol.Int) {
            if (value.IsFloatingPoint())
                value = Math.Truncate(Convert.ToDouble(value));

            return Convert.ToInt32(value);
        } else if (type == TypeSymbol.String) {
            return Convert.ToString(value);
        } else if (type == TypeSymbol.Decimal) {
            return Convert.ToDouble(value);
        }

        throw new Exception($"EvaluateCast: unexpected type '{typeClause}'");
    }

    private object EvaluateCallExpression(BoundCallExpression node) {
        if (MethodsMatch(node.function, BuiltinFunctions.Input)) {
            return Console.ReadLine();
        } else if (MethodsMatch(node.function, BuiltinFunctions.Print)) {
            var message = (object)EvaluateExpression(node.arguments[0]);
            Console.Write(message);
            hasPrint = true;
        } else if (MethodsMatch(node.function, BuiltinFunctions.PrintLine)) {
            var message = (object)EvaluateExpression(node.arguments[0]);
            Console.WriteLine(message);
        } else if (MethodsMatch(node.function, BuiltinFunctions.RandInt)) {
            var max = (int)EvaluateExpression(node.arguments[0]);

            if (random_ == null)
                random_ = new Random();

            return random_.Next(max);
        } else if (node.function.name == "Value") {
            object? value = EvaluateExpression(node.arguments[0]);

            if (value == null)
                throw new NullReferenceException();

            return value;
        } else if (MethodsMatch(node.function, BuiltinFunctions.HasValue)) {
            object? value = EvaluateExpression(node.arguments[0]);

            if (value == null)
                return false;

            return true;
        } else {
            var locals = new Dictionary<VariableSymbol, object>();

            for (int i=0; i<node.arguments.Length; i++) {
                var parameter = node.function.parameters[i];
                var value = EvaluateExpression(node.arguments[i]);
                locals.Add(parameter, value);
            }

            locals_.Push(locals);
            var statement = LookupMethod(node.function);
            var result = EvaluateStatement(statement);
            locals_.Pop();

            return result;
        }

        return null;
    }

    private BoundBlockStatement LookupMethod(FunctionSymbol function) {
        foreach (var pair in functions_)
            if (MethodsMatch(pair.Key, function))
                return pair.Value;

        throw new Exception($"LookupMethod: could not find method '{function.name}'");
    }

    private bool MethodsMatch(FunctionSymbol left, FunctionSymbol right) {
        if (left.name == right.name && left.parameters.Length == right.parameters.Length) {
            var parametersMatch = true;

            for (int i=0; i<left.parameters.Length; i++) {
                var checkParameter = left.parameters[i];
                var parameter = right.parameters[i];

                if (checkParameter.name != parameter.name || checkParameter.typeClause != parameter.typeClause)
                    parametersMatch = false;
            }

            if (parametersMatch)
                return true;
        }

        return false;
    }

    private object EvaluateConstantExpression(BoundExpression syntax) {
        return EvaluateCast(syntax.constantValue.value, syntax.typeClause);
    }

    private object EvaluateVariableExpression(BoundVariableExpression syntax) {
        if (syntax.variable.type == SymbolType.GlobalVariable)
            return globals_[syntax.variable];

        var locals = locals_.Peek();
        return locals[syntax.variable];
    }

    private object EvaluateAssignmentExpresion(BoundAssignmentExpression syntax) {
        var value = EvaluateExpression(syntax.expression);
        Assign(syntax.variable, value);

        return value;
    }

    private object EvaluateUnaryExpression(BoundUnaryExpression syntax) {
        var operand = EvaluateExpression(syntax.operand);

        if (operand == null)
            return null;

        switch (syntax.op.opType) {
            case BoundUnaryOperatorType.NumericalIdentity:
                if (syntax.operand.typeClause.lType == TypeSymbol.Int)
                    return (int)operand;
                else
                    return (double)operand;
            case BoundUnaryOperatorType.NumericalNegation:
                if (syntax.operand.typeClause.lType == TypeSymbol.Int)
                    return -(int)operand;
                else
                    return -(double)operand;
            case BoundUnaryOperatorType.BooleanNegation:
                return !(bool)operand;
            case BoundUnaryOperatorType.BitwiseCompliment:
                return ~(int)operand;
            default:
                throw new Exception($"EvaluateUnaryExpression: unknown unary operator '{syntax.op}'");
        }
    }

    private object EvaluateBinaryExpression(BoundBinaryExpression syntax) {
        var left = EvaluateExpression(syntax.left);
        var right = EvaluateExpression(syntax.right);

        // TODO remove temp ?? check
        if ((left == null || right == null) && syntax.op.opType != BoundBinaryOperatorType.NullCoalescing)
            return null;

        var syntaxType = syntax.typeClause.lType;
        var leftType = syntax.left.typeClause.lType;

        switch (syntax.op.opType) {
            case BoundBinaryOperatorType.Addition:
                if (syntaxType == TypeSymbol.Int)
                    return (int)left + (int)right;
                else if (syntaxType == TypeSymbol.String)
                    return (string)left + (string)right;
                else
                    return (double)left + (double)right;
            case BoundBinaryOperatorType.Subtraction:
                if (syntaxType == TypeSymbol.Int)
                    return (int)left - (int)right;
                else
                    return (double)left - (double)right;
            case BoundBinaryOperatorType.Multiplication:
                if (syntaxType == TypeSymbol.Int)
                    return (int)left * (int)right;
                else
                    return (double)left * (double)right;
            case BoundBinaryOperatorType.Division:
                if (syntaxType == TypeSymbol.Int)
                    return (int)left / (int)right;
                else
                    return (double)left / (double)right;
            case BoundBinaryOperatorType.Power:
                if (syntaxType == TypeSymbol.Int)
                    return (int)Math.Pow((int)left, (int)right);
                else
                    return (double)Math.Pow((double)left, (double)right);
            case BoundBinaryOperatorType.ConditionalAnd:
                return (bool)left && (bool)right;
            case BoundBinaryOperatorType.ConditionalOr:
                return (bool)left || (bool)right;
            case BoundBinaryOperatorType.EqualityEquals:
                return Equals(left, right);
            case BoundBinaryOperatorType.EqualityNotEquals:
                return !Equals(left, right);
            case BoundBinaryOperatorType.LessThan:
                if (leftType == TypeSymbol.Int)
                    return (int)left < (int)right;
                else
                    return (double)left < (double)right;
            case BoundBinaryOperatorType.GreaterThan:
                if (leftType == TypeSymbol.Int)
                    return (int)left > (int)right;
                else
                    return (double)left > (double)right;
            case BoundBinaryOperatorType.LessOrEqual:
                if (leftType == TypeSymbol.Int)
                    return (int)left <= (int)right;
                else
                    return (double)left <= (double)right;
            case BoundBinaryOperatorType.GreatOrEqual:
                if (leftType == TypeSymbol.Int)
                    return (int)left >= (int)right;
                else
                    return (double)left >= (double)right;
            case BoundBinaryOperatorType.LogicalAnd:
                if (syntaxType == TypeSymbol.Int)
                    return (int)left & (int)right;
                else
                    return (bool)left & (bool)right;
            case BoundBinaryOperatorType.LogicalOr:
                if (syntaxType == TypeSymbol.Int)
                    return (int)left | (int)right;
                else
                    return (bool)left | (bool)right;
            case BoundBinaryOperatorType.LogicalXor:
                if (syntaxType == TypeSymbol.Int)
                    return (int)left ^ (int)right;
                else
                    return (bool)left ^ (bool)right;
            case BoundBinaryOperatorType.LeftShift:
                return (int)left << (int)right;
            case BoundBinaryOperatorType.RightShift:
                return (int)left >> (int)right;
            case BoundBinaryOperatorType.UnsignedRightShift:
                return (int)left >>> (int)right;
            case BoundBinaryOperatorType.Modulo:
                if (syntaxType == TypeSymbol.Int)
                    return (int)left % (int)right;
                else
                    return (double)left % (double)right;
            case BoundBinaryOperatorType.NullCoalescing:
            // TODO remove this temp case
                if (syntaxType == TypeSymbol.Int)
                    return (int?)left ?? (int?)right;
                else if (syntaxType == TypeSymbol.Bool)
                    return (bool?)left ?? (bool?)right;
                else if (syntaxType == TypeSymbol.String)
                    return (string?)left ?? (string?)right;
                else
                    return (double?)left ?? (double?)right;
            default:
                throw new Exception($"EvaluateBinaryExpression: unknown binary operator '{syntax.op}'");
        }
    }
}
