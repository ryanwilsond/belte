using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis {

    internal sealed class Evaluator {
        private readonly BoundProgram program_;
        public DiagnosticQueue diagnostics;
        private readonly Dictionary<VariableSymbol, object> globals_;
        private readonly Stack<Dictionary<VariableSymbol, object>> locals_ =
            new Stack<Dictionary<VariableSymbol, object>>();
        private object lastValue_;
        private Random random_;

        public Evaluator(BoundProgram program, Dictionary<VariableSymbol, object> globals) {
            program_ = program;
            diagnostics = new DiagnosticQueue();
            globals_ = globals;
        }

        public object Evaluate() {
            return EvaluateStatement(program_.statement);
        }

        private object EvaluateStatement(BoundBlockStatement statement) {
            var labelToIndex = new Dictionary<BoundLabel, int>();

            for (int i = 0; i < statement.statements.Length; i++) {
                if (statement.statements[i] is BoundLabelStatement l)
                    labelToIndex.Add(l.label, i + 1);
            }

            var index = 0;
            while (index < statement.statements.Length) {
                var s = statement.statements[index];

                switch (s.type) {
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

        private void EvaluateExpressionStatement(BoundExpressionStatement statement) {
            lastValue_ = EvaluateExpression(statement.expression);
        }

        private void EvaluateVariableDeclarationStatement(BoundVariableDeclarationStatement statement) {
            var value = EvaluateExpression(statement.initializer);
            lastValue_ = value;
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
            switch (node.type) {
                case BoundNodeType.LiteralExpression: return EvaluateLiteral((BoundLiteralExpression)node);
                case BoundNodeType.VariableExpression: return EvaluateVariable((BoundVariableExpression)node);
                case BoundNodeType.AssignmentExpression: return EvaluateAssignment((BoundAssignmentExpression)node);
                case BoundNodeType.UnaryExpression: return EvaluateUnary((BoundUnaryExpression)node);
                case BoundNodeType.BinaryExpression: return EvaluateBinary((BoundBinaryExpression)node);
                case BoundNodeType.CallExpression: return EvaluateCall((BoundCallExpression)node);
                case BoundNodeType.CastExpression: return EvaluateCast((BoundCastExpression)node);
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unexpected node '{node.type}'");
                    return null;
            }
        }

        private object EvaluateCast(BoundCastExpression node) {
            var value = EvaluateExpression(node.expression);

            if (node.lType == TypeSymbol.Bool) return Convert.ToBoolean(value);
            if (node.lType == TypeSymbol.Int) return Convert.ToInt32(value);
            if (node.lType == TypeSymbol.String) return Convert.ToString(value);

            diagnostics.Push(DiagnosticType.Fatal, $"unexpected type {node.lType}");
            return null;
        }

        private object EvaluateCall(BoundCallExpression node) {
            if (node.function == BuiltinFunctions.Input) {
                return Console.ReadLine();
            } else if (node.function == BuiltinFunctions.Print) {
                var message = (string)EvaluateExpression(node.arguments[0]);
                Console.WriteLine(message);
            } else if (node.function == BuiltinFunctions.Randint) {
                var max = (int)EvaluateExpression(node.arguments[0]);

                if (random_ == null) random_ = new Random();

                return random_.Next(max);
            } else {
                var locals = new Dictionary<VariableSymbol, object>();
                for (int i=0; i<node.arguments.Length; i++) {
                    var parameter = node.function.parameters[i];
                    var value = EvaluateExpression(node.arguments[i]);
                    locals.Add(parameter, value);
                }

                locals_.Push(locals);
                var statement = program_.functions[node.function];
                var result = EvaluateStatement(statement);
                locals_.Pop();
                return result;
            }

            return null;
        }

        private object EvaluateLiteral(BoundLiteralExpression syntax) {
            return syntax.value;
        }

        private object EvaluateVariable(BoundVariableExpression syntax) {
            if (syntax.variable.type == SymbolType.GlobalVariable)
                return globals_[syntax.variable];

            var locals = locals_.Peek();
            return locals[syntax.variable];
        }

        private object EvaluateAssignment(BoundAssignmentExpression syntax) {
            var value = EvaluateExpression(syntax.expression);
            Assign(syntax.variable, value);

            return value;
        }

        private object EvaluateUnary(BoundUnaryExpression syntax) {
            var operand = EvaluateExpression(syntax.operand);

            switch (syntax.op.opType) {
                case BoundUnaryOperatorType.NumericalIdentity: return (int)operand;
                case BoundUnaryOperatorType.NumericalNegation: return -(int)operand;
                case BoundUnaryOperatorType.BooleanNegation: return !(bool)operand;
                case BoundUnaryOperatorType.BitwiseCompliment: return ~(int)operand;
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unknown unary operator '{syntax.op}'");
                    return null;
            }
        }

        private object EvaluateBinary(BoundBinaryExpression syntax) {
            var left = EvaluateExpression(syntax.left);
            var right = EvaluateExpression(syntax.right);

            switch (syntax.op.opType) {
                case BoundBinaryOperatorType.Addition:
                    if (syntax.lType == TypeSymbol.Int)
                        return (int)left + (int)right;
                    else
                        return (string)left + (string)right;
                case BoundBinaryOperatorType.Subtraction: return (int)left - (int)right;
                case BoundBinaryOperatorType.Multiplication: return (int)left * (int)right;
                case BoundBinaryOperatorType.Division: return (int)left / (int)right;
                case BoundBinaryOperatorType.Power: return (int)Math.Pow((int)left, (int)right);
                case BoundBinaryOperatorType.ConditionalAnd: return (bool)left && (bool)right;
                case BoundBinaryOperatorType.ConditionalOr: return (bool)left || (bool)right;
                case BoundBinaryOperatorType.EqualityEquals: return Equals(left, right);
                case BoundBinaryOperatorType.EqualityNotEquals: return !Equals(left, right);
                case BoundBinaryOperatorType.LessThan: return (int)left < (int)right;
                case BoundBinaryOperatorType.GreaterThan: return (int)left > (int)right;
                case BoundBinaryOperatorType.LessOrEqual: return (int)left <= (int)right;
                case BoundBinaryOperatorType.GreatOrEqual: return (int)left >= (int)right;
                case BoundBinaryOperatorType.LogicalAnd:
                    if (syntax.lType == TypeSymbol.Int)
                        return (int)left & (int)right;
                    else
                        return (bool)left & (bool)right;
                case BoundBinaryOperatorType.LogicalOr:
                    if (syntax.lType == TypeSymbol.Int)
                        return (int)left | (int)right;
                    else
                        return (bool)left | (bool)right;
                case BoundBinaryOperatorType.LogicalXor:
                    if (syntax.lType == TypeSymbol.Int)
                        return (int)left ^ (int)right;
                    else
                        return (bool)left ^ (bool)right;
                case BoundBinaryOperatorType.LeftShift: return (int)left << (int)right;
                case BoundBinaryOperatorType.RightShift: return (int)left >> (int)right;
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unknown binary operator '{syntax.op}'");
                    return null;
            }
        }
    }
}
