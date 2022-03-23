using System.Collections.Generic;

namespace Buckle.CodeAnalysis {

    internal class Evaluator {
        private readonly Expression root_;
        public List<Diagnostic> diagnostics;

        public Evaluator(Expression root) {
            root_ = root;
            diagnostics = new List<Diagnostic>();
        }

        public int? Evaluate() { return EvaluteExpression(root_); }

        private int? EvaluteExpression(Expression node) {
            if (node is NumberNode n) {
                return (int)n.token.value;
            } else if (node is UnaryExpression u) {
                var operand = EvaluteExpression(u.operand);

                if (u.op.type == SyntaxType.PLUS) return operand;
                else if (u.op.type == SyntaxType.MINUS) return -operand;
                else diagnostics.Add(new Diagnostic(DiagnosticType.fatal, "unknown unary operator '{u.op.type}'"));
            } else if (node is BinaryExpression b) {
                var left = EvaluteExpression(b.left);
                var right = EvaluteExpression(b.right);

                switch (b.op.type) {
                    case SyntaxType.PLUS: return left + right;
                    case SyntaxType.MINUS: return left - right;
                    case SyntaxType.ASTERISK: return left * right;
                    case SyntaxType.SOLIDUS: return left / right;
                    default:
                        diagnostics.Add(new Diagnostic(DiagnosticType.fatal, $"unknown binary operator '{b.op.type}'"));
                        return null;
                }
            } else if (node is ParenExpression p) {
                return EvaluteExpression(p.expr);
            }

            diagnostics.Add(new Diagnostic(DiagnosticType.fatal, $"unexpected node '{node.type}'"));
            return null;
        }
    }
}
