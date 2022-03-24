using System.Collections.Generic;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis {

    internal class Evaluator {
        private readonly BoundExpression root_;
        public List<Diagnostic> diagnostics;

        public Evaluator(BoundExpression root) {
            root_ = root;
            diagnostics = new List<Diagnostic>();
        }

        public object Evaluate() { return EvaluteExpression(root_); }

        private object EvaluteExpression(BoundExpression node) {
            if (node is BoundLiteralExpression n) {
                return n.value;
            } else if (node is BoundUnaryExpression u) {
                var operand = EvaluteExpression(u.operand);

                switch(u.op) {
                    case BoundUnaryOperatorType.NumericalIdentity: return (int)operand;
                    case BoundUnaryOperatorType.NumericalNegation: return -(int)operand;
                    case BoundUnaryOperatorType.BooleanNegation: return !(bool)operand;
                    default:
                        diagnostics.Add(new Diagnostic(DiagnosticType.fatal, $"unknown unary operator '{u.op}'"));
                        return null;
                }
            } else if (node is BoundBinaryExpression b) {
                var left = EvaluteExpression(b.left);
                var right = EvaluteExpression(b.right);

                switch (b.op) {
                    case BoundBinaryOperatorType.Add: return (int)left + (int)right;
                    case BoundBinaryOperatorType.Subtract: return (int)left - (int)right;
                    case BoundBinaryOperatorType.Multiply: return (int)left * (int)right;
                    case BoundBinaryOperatorType.Divide: return (int)left / (int)right;
                    case BoundBinaryOperatorType.ConditionalAnd: return (bool)left && (bool)right;
                    case BoundBinaryOperatorType.ConditionalOr: return (bool)left || (bool)right;
                    default:
                        diagnostics.Add(new Diagnostic(DiagnosticType.fatal, $"unknown binary operator '{b.op}'"));
                        return null;
                }
            }

            diagnostics.Add(new Diagnostic(DiagnosticType.fatal, $"unexpected node '{node.type}'"));
            return null;
        }
    }
}
