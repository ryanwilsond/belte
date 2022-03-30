using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis {

    internal sealed class Evaluator {
        private readonly BoundStatement root_;
        public DiagnosticQueue diagnostics;
        private readonly Dictionary<VariableSymbol, object> variables_;
        private object last_value_;

        public Evaluator(BoundStatement root, Dictionary<VariableSymbol, object> variables) {
            root_ = root;
            diagnostics = new DiagnosticQueue();
            variables_ = variables;
        }

        public object Evaluate() {
            EvaluateStatement(root_);
            return last_value_;
        }

        private void EvaluateStatement(BoundStatement statement) {
            switch(statement.type) {
                case BoundNodeType.BLOCK_STATEMENT:
                    EvaluateBlockStatement((BoundBlockStatement)statement);
                    break;
                case BoundNodeType.EXPRESSION_STATEMENT:
                    EvaluateExpressionStatement((BoundExpressionStatement)statement);
                    break;
                case BoundNodeType.VARIABLE_DECLARATION_STATEMENT:
                    EvaluteVariableDeclaration((BoundVariableDeclaration)statement);
                    break;
                default:
                    diagnostics.Push(DiagnosticType.fatal, $"unexpected statement '{statement.type}'");
                    break;
            }
        }

        private void EvaluateBlockStatement(BoundBlockStatement statement) {
            foreach (var state in statement.statements)
                EvaluateStatement(state);
        }

        private void EvaluateExpressionStatement(BoundExpressionStatement statement) {
            last_value_ = EvaluateExpression(statement.expr);
        }

        private void EvaluteVariableDeclaration(BoundVariableDeclaration statement) {
            var value = EvaluateExpression(statement.init);
            variables_[statement.variable] = value;
            last_value_ = value;
        }

        private object EvaluateExpression(BoundExpression node) {
            switch(node.type) {
                case BoundNodeType.LITERAL_EXPR: return EvaluateLiteral((BoundLiteralExpression)node);
                case BoundNodeType.VARIABLE_EXPR: return EvaluateVariable((BoundVariableExpression)node);
                case BoundNodeType.ASSIGN_EXPR: return EvaluateAssignment((BoundAssignmentExpression)node);
                case BoundNodeType.UNARY_EXPR: return EvaluateUnary((BoundUnaryExpression)node);
                case BoundNodeType.BINARY_EXPR: return EvaluateBinary((BoundBinaryExpression)node);
                default:
                    diagnostics.Push(DiagnosticType.fatal, $"unexpected node '{node.type}'");
                    return null;
            }
        }

        private object EvaluateLiteral(BoundLiteralExpression expr) {
            return expr.value;
        }

        private object EvaluateVariable(BoundVariableExpression expr) {
            return variables_[expr.variable];
        }

        private object EvaluateAssignment(BoundAssignmentExpression expr) {
            var value = EvaluateExpression(expr.expr);
            variables_[expr.variable] = value;
            return value;
        }

        private object EvaluateUnary(BoundUnaryExpression expr) {
            var operand = EvaluateExpression(expr.operand);

            switch(expr.op.optype) {
                case BoundUnaryOperatorType.NumericalIdentity: return (int)operand;
                case BoundUnaryOperatorType.NumericalNegation: return -(int)operand;
                case BoundUnaryOperatorType.BooleanNegation: return !(bool)operand;
                default:
                    diagnostics.Push(DiagnosticType.fatal, $"unknown unary operator '{expr.op}'");
                    return null;
            }
        }

        private object EvaluateBinary(BoundBinaryExpression expr) {
            var left = EvaluateExpression(expr.left);
            var right = EvaluateExpression(expr.right);

            switch (expr.op.optype) {
                case BoundBinaryOperatorType.Add: return (int)left + (int)right;
                case BoundBinaryOperatorType.Subtract: return (int)left - (int)right;
                case BoundBinaryOperatorType.Multiply: return (int)left * (int)right;
                case BoundBinaryOperatorType.Divide: return (int)left / (int)right;
                case BoundBinaryOperatorType.Power: return (int)Math.Pow((int)left, (int)right);
                case BoundBinaryOperatorType.ConditionalAnd: return (bool)left && (bool)right;
                case BoundBinaryOperatorType.ConditionalOr: return (bool)left || (bool)right;
                case BoundBinaryOperatorType.EqualityEquals: return Equals(left, right);
                case BoundBinaryOperatorType.EqualityNotEquals: return !Equals(left, right);
                case BoundBinaryOperatorType.LessThan: return (int)left < (int)right;
                case BoundBinaryOperatorType.GreaterThan: return (int)left > (int)right;
                case BoundBinaryOperatorType.LessOrEqual: return (int)left <= (int)right;
                case BoundBinaryOperatorType.GreatOrEqual: return (int)left >= (int)right;
                default:
                    diagnostics.Push(DiagnosticType.fatal, $"unknown binary operator '{expr.op}'");
                    return null;
            }
        }
    }
}
