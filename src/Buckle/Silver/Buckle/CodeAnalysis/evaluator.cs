using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis {

    internal sealed class Evaluator {
        private readonly BoundStatement root_;
        public DiagnosticQueue diagnostics;
        private readonly Dictionary<VariableSymbol, object> variables_;
        private object lastValue_;

        public Evaluator(BoundStatement root, Dictionary<VariableSymbol, object> variables) {
            root_ = root;
            diagnostics = new DiagnosticQueue();
            variables_ = variables;
        }

        public object Evaluate() {
            EvaluateStatement(root_);
            return lastValue_;
        }

        private void EvaluateStatement(BoundStatement statement) {
            switch(statement.type) {
                case BoundNodeType.BlockStatement:
                    EvaluateBlockStatement((BoundBlockStatement)statement);
                    break;
                case BoundNodeType.ExpressionStatement:
                    EvaluateExpressionStatement((BoundExpressionStatement)statement);
                    break;
                case BoundNodeType.VariableDeclarationStatement:
                    EvaluateVariableDeclarationStatement((BoundVariableDeclarationStatement)statement);
                    break;
                case BoundNodeType.IfStatement:
                    EvaluateIfStatement((BoundIfStatement)statement);
                    break;
                case BoundNodeType.WhileStatement:
                    EvaluateWhileStatement((BoundWhileStatement)statement);
                    break;
                case BoundNodeType.ForStatement:
                    EvaluateForStatement((BoundForStatement)statement);
                    break;
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unexpected statement '{statement.type}'");
                    break;
            }
        }

        private void EvaluateWhileStatement(BoundWhileStatement statement) {
            while ((bool)EvaluateExpression(statement.condition))
                EvaluateStatement(statement.body);
        }

        private void EvaluateForStatement(BoundForStatement statement) {
            EvaluateVariableDeclarationStatement(statement.stepper);

            while ((bool)EvaluateExpression(statement.condition)) {
                EvaluateStatement(statement.body);
                EvaluateAssignment(statement.step);
            }
        }

        private void EvaluateIfStatement(BoundIfStatement statement) {
            var condition = (bool)EvaluateExpression(statement.condition);
            if (condition)
                EvaluateStatement(statement.then);
            else if (statement.elseStatement != null)
                EvaluateStatement(statement.elseStatement);
        }

        private void EvaluateBlockStatement(BoundBlockStatement statement) {
            foreach (var state in statement.statements)
                EvaluateStatement(state);
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
            switch(node.type) {
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

            switch(syntax.op.opType) {
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
                case BoundBinaryOperatorType.Addition: return (int)left + (int)right;
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
                    if (syntax.lType == typeof(int))
                        return (int)left & (int)right;
                    else
                        return (bool)left & (bool)right;
                case BoundBinaryOperatorType.LogicalOr:
                    if (syntax.lType == typeof(int))
                        return (int)left | (int)right;
                    else
                        return (bool)left | (bool)right;
                case BoundBinaryOperatorType.LogicalXor:
                    if (syntax.lType == typeof(int))
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
