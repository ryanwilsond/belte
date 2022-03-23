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

        public int? Evaluate() { return EvaluteExpression(root_); }

        private int? EvaluteExpression(BoundExpression node) {
            if (node is BoundLiteralExpression n) {
                return (int)n.value;
            } else if (node is BoundUnaryExpression u) {
                var operand = EvaluteExpression(u.operand);

                if (u.op == BoundUnaryOperatorType.Identity) return operand;
                else if (u.op == BoundUnaryOperatorType.Negation) return -operand;
                else diagnostics.Add(new Diagnostic(DiagnosticType.fatal, $"unknown unary operator '{u.op}'"));
            } else if (node is BoundBinaryExpression b) {
                var left = EvaluteExpression(b.left);
                var right = EvaluteExpression(b.right);

                switch (b.op) {
                    case BoundBinaryOperatorType.Add: return left + right;
                    case BoundBinaryOperatorType.Subtract: return left - right;
                    case BoundBinaryOperatorType.Multiply: return left * right;
                    case BoundBinaryOperatorType.Divide: return left / right;
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
