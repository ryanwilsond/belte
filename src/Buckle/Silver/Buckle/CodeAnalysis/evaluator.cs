using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis {

    internal sealed class Evaluator {
        private readonly BoundBlockStatement root_;
        public DiagnosticQueue diagnostics;
        private readonly Dictionary<VariableSymbol, object> variables_;
        private object lastValue_;

        public Evaluator(BoundBlockStatement root, Dictionary<VariableSymbol, object> variables) {
            root_ = root;
            diagnostics = new DiagnosticQueue();
            variables_ = variables;
        }

        public object Evaluate() {
            var labelToIndex = new Dictionary<BoundLabel, int>();

            for (int i = 0; i < root_.statements.Length; i++) {
                if (root_.statements[i] is BoundLabelStatement l)
                    labelToIndex.Add(l.label, i + 1);
            }

            var index = 0;
            while (index < root_.statements.Length) {
                var s = root_.statements[index];

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
            variables_[statement.variable] = value;
            lastValue_ = value;
        }

        private object EvaluateExpression(BoundExpression node) {
            switch (node.type) {
                case BoundNodeType.LiteralExpression: return EvaluateLiteral((BoundLiteralExpression)node);
                case BoundNodeType.VariableExpression: return EvaluateVariable((BoundVariableExpression)node);
                case BoundNodeType.AssignmentExpression: return EvaluateAssignment((BoundAssignmentExpression)node);
                case BoundNodeType.UnaryExpression: return EvaluateUnary((BoundUnaryExpression)node);
                case BoundNodeType.BinaryExpression: return EvaluateBinary((BoundBinaryExpression)node);
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unexpected node '{node.type}'");
                    return null;
            }
        }

        private object EvaluateLiteral(BoundLiteralExpression syntax) {
            return syntax.value;
        }

        private object EvaluateVariable(BoundVariableExpression syntax) {
            return variables_[syntax.variable];
        }

        private object EvaluateAssignment(BoundAssignmentExpression syntax) {
            var value = EvaluateExpression(syntax.expression);
            variables_[syntax.variable] = value;
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
