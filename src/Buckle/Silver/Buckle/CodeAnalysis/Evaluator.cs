using System;
using System.Collections.Generic;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis;

internal sealed class Evaluator {
    private readonly BoundProgram program_;
    public DiagnosticQueue diagnostics;
    private readonly Dictionary<VariableSymbol, object> globals_;
    private readonly Dictionary<FunctionSymbol, BoundBlockStatement> functions_ =
        new Dictionary<FunctionSymbol, BoundBlockStatement>();
    private readonly Stack<Dictionary<VariableSymbol, object>> locals_ =
        new Stack<Dictionary<VariableSymbol, object>>();
    private object lastValue_;
    private Random random_;
    internal bool hasPrint = false;

    public Evaluator(BoundProgram program, Dictionary<VariableSymbol, object> globals) {
        diagnostics = new DiagnosticQueue();
        program_ = program;
        globals_ = globals;
        locals_.Push(new Dictionary<VariableSymbol, object>());

        var current = program;
        while (current != null) {
            foreach (var (function, body) in current.functions)
                functions_.Add(function, body);

            current = current.previous;
        }
    }

    public object Evaluate() {
        var function = program_.mainFunction ?? program_.scriptFunction;
        if (function == null)
            return null;

        var body = functions_[function];
        return EvaluateStatement(body);
    }

    internal object EvaluateStatement(BoundBlockStatement statement) {
        var labelToIndex = new Dictionary<BoundLabel, int>();

        for (int i = 0; i < statement.statements.Length; i++) {
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
                    diagnostics.Push(DiagnosticType.Fatal, $"unexpected statement '{s.type}'");
                    index++;
                    break;
            }
        }

        return lastValue_;
    }

    internal void EvaluateExpressionStatement(BoundExpressionStatement statement) {
        lastValue_ = EvaluateExpression(statement.expression);
    }

    internal void EvaluateVariableDeclarationStatement(BoundVariableDeclarationStatement statement) {
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

    internal object EvaluateExpression(BoundExpression node) {
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
            case BoundNodeType.EmptyExpression:
                return null;
            default:
                diagnostics.Push(DiagnosticType.Fatal, $"unexpected node '{node.type}'");
                return null;
        }
    }

    internal object EvaluateIndexExpression(BoundIndexExpression node) {
        object[] variable = null;

        if (node.variable.type == SymbolType.GlobalVariable) {
            variable = (object[])globals_[node.variable];
        } else {
            var locals = locals_.Peek();
            variable = (object[])locals[node.variable];
        }

        var index = EvaluateExpression(node.index);
        return variable[(int)index];
    }

    internal object EvaluateInitializerListExpression(BoundInitializerListExpression node) {
        var builder = new List<object>();

        foreach (var item in node.items) {
            object value = EvaluateExpression(item);
            builder.Add(value);
        }

        return builder.ToArray();
    }

    internal object EvaluateCastExpression(BoundCastExpression node) {
        var value = EvaluateExpression(node.expression);

        if (value == null)
            return null;

        var type = node.typeClause.lType;

        if (type == TypeSymbol.Any)
            return value;
        if (type == TypeSymbol.Bool)
            return Convert.ToBoolean(value);
        if (type == TypeSymbol.Int)
            return Convert.ToInt32(value);
        if (type == TypeSymbol.String)
            return Convert.ToString(value);
        if (type == TypeSymbol.Decimal)
            return Convert.ToSingle(value);

        diagnostics.Push(DiagnosticType.Fatal, $"unexpected type '{node.typeClause}'");
        return null;
    }

    internal object EvaluateCallExpression(BoundCallExpression node) {
        if (node.function == BuiltinFunctions.Input) {
            return Console.ReadLine();
        } else if (node.function == BuiltinFunctions.Print) {
            var message = (object)EvaluateExpression(node.arguments[0]);
            Console.Write(message);
            hasPrint = true;
        } else if (node.function == BuiltinFunctions.Randint) {
            var max = (int)EvaluateExpression(node.arguments[0]);

            if (random_ == null)
                random_ = new Random();

            return random_.Next(max);
        } else {
            var locals = new Dictionary<VariableSymbol, object>();
            for (int i=0; i<node.arguments.Length; i++) {
                var parameter = node.function.parameters[i];
                var value = EvaluateExpression(node.arguments[i]);
                locals.Add(parameter, value);
            }

            locals_.Push(locals);
            var statement = functions_[node.function];
            var result = EvaluateStatement(statement);
            locals_.Pop();
            return result;
        }

        return null;
    }

    internal object EvaluateConstantExpression(BoundExpression syntax) {
        return syntax.constantValue.value;
    }

    internal object EvaluateVariableExpression(BoundVariableExpression syntax) {
        if (syntax.variable.type == SymbolType.GlobalVariable)
            return globals_[syntax.variable];

        var locals = locals_.Peek();
        return locals[syntax.variable];
    }

    internal object EvaluateAssignmentExpresion(BoundAssignmentExpression syntax) {
        var value = EvaluateExpression(syntax.expression);
        Assign(syntax.variable, value);

        return value;
    }

    internal object EvaluateUnaryExpression(BoundUnaryExpression syntax) {
        var operand = EvaluateExpression(syntax.operand);

        if (operand == null)
            return null;

        switch (syntax.op.opType) {
            case BoundUnaryOperatorType.NumericalIdentity:
                if (syntax.operand.typeClause.lType == TypeSymbol.Int)
                    return (int)operand;
                else
                    return (float)operand;
            case BoundUnaryOperatorType.NumericalNegation:
                if (syntax.operand.typeClause.lType == TypeSymbol.Int)
                    return -(int)operand;
                else
                    return -(float)operand;
            case BoundUnaryOperatorType.BooleanNegation:
                return !(bool)operand;
            case BoundUnaryOperatorType.BitwiseCompliment:
                return ~(int)operand;
            default:
                diagnostics.Push(DiagnosticType.Fatal, $"unknown unary operator '{syntax.op}'");
                return null;
        }
    }

    internal object EvaluateBinaryExpression(BoundBinaryExpression syntax) {
        var left = EvaluateExpression(syntax.left);
        var right = EvaluateExpression(syntax.right);

        if (left == null || right == null)
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
                    return (float)left + (float)right;
            case BoundBinaryOperatorType.Subtraction:
                if (syntaxType == TypeSymbol.Int)
                    return (int)left - (int)right;
                else
                    return (float)left - (float)right;
            case BoundBinaryOperatorType.Multiplication:
                if (syntaxType == TypeSymbol.Int)
                    return (int)left * (int)right;
                else
                    return (float)left * (float)right;
            case BoundBinaryOperatorType.Division:
                if (syntaxType == TypeSymbol.Int)
                    return (int)left / (int)right;
                else
                    return (float)left / (float)right;
            case BoundBinaryOperatorType.Power:
                if (syntaxType == TypeSymbol.Int)
                    return (int)Math.Pow((int)left, (int)right);
                else
                    return (float)Math.Pow((float)left, (float)right);
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
                    return (float)left < (float)right;
            case BoundBinaryOperatorType.GreaterThan:
                if (leftType == TypeSymbol.Int)
                    return (int)left > (int)right;
                else
                    return (float)left > (float)right;
            case BoundBinaryOperatorType.LessOrEqual:
                if (leftType == TypeSymbol.Int)
                    return (int)left <= (int)right;
                else
                    return (float)left <= (float)right;
            case BoundBinaryOperatorType.GreatOrEqual:
                if (leftType == TypeSymbol.Int)
                    return (int)left >= (int)right;
                else
                    return (float)left >= (float)right;
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
            default:
                diagnostics.Push(DiagnosticType.Fatal, $"unknown binary operator '{syntax.op}'");
                return null;
        }
    }
}
