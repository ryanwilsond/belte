using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis {

    internal class Evaluator {
        private readonly BoundExpression root_;
        public DiagnosticQueue diagnostics;
        private readonly Dictionary<VariableSymbol, object> variables_;

        public Evaluator(BoundExpression root, Dictionary<VariableSymbol, object> variables) {
            root_ = root;
            diagnostics = new DiagnosticQueue();
            variables_ = variables;
        }

        public object Evaluate() { return EvaluteExpression(root_); }

        private object EvaluteExpression(BoundExpression node) {
            if (node is BoundLiteralExpression n) {
                return n.value;
            } else if (node is BoundVariableExpression v) {
                return variables_[v.variable];
            } else if (node is BoundAssignmentExpression a) {
                var value = EvaluteExpression(a.expr);
                variables_[a.variable] = value;
                return value;
            } else if (node is BoundUnaryExpression u) {
                var operand = EvaluteExpression(u.operand);

                switch(u.op.optype) {
                    case BoundUnaryOperatorType.NumericalIdentity: return (int)operand;
                    case BoundUnaryOperatorType.NumericalNegation: return -(int)operand;
                    case BoundUnaryOperatorType.BooleanNegation: return !(bool)operand;
                    default:
                        diagnostics.Push(DiagnosticType.fatal, $"unknown unary operator '{u.op}'");
                        return null;
                }
            } else if (node is BoundBinaryExpression b) {
                var left = EvaluteExpression(b.left);
                var right = EvaluteExpression(b.right);

                switch (b.op.optype) {
                    case BoundBinaryOperatorType.Add: return (int)left + (int)right;
                    case BoundBinaryOperatorType.Subtract: return (int)left - (int)right;
                    case BoundBinaryOperatorType.Multiply: return (int)left * (int)right;
                    case BoundBinaryOperatorType.Divide: return (int)left / (int)right;
                    case BoundBinaryOperatorType.ConditionalAnd: return (bool)left && (bool)right;
                    case BoundBinaryOperatorType.ConditionalOr: return (bool)left || (bool)right;
                    case BoundBinaryOperatorType.EqualityEquals: return Equals(left, right);
                    case BoundBinaryOperatorType.EqualityNotEquals: return !Equals(left, right);
                    default:
                        diagnostics.Push(DiagnosticType.fatal, $"unknown binary operator '{b.op}'");
                        return null;
                }
            }

            diagnostics.Push(DiagnosticType.fatal, $"unexpected node '{node.type}'");
            return null;
        }
    }
}
