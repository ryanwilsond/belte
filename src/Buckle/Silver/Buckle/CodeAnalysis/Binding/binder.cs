using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding {

    internal class Binder {
        public List<Diagnostic> diagnostics;

        public Binder() {
            diagnostics = new List<Diagnostic>();
        }

        public BoundExpression BindExpression(Expression expr) {
            switch (expr.type) {
                case SyntaxType.LITERAL_EXPR: return BindLiteralExpression((LiteralExpression)expr);
                case SyntaxType.UNARY_EXPR: return BindUnaryExpression((UnaryExpression)expr);
                case SyntaxType.BINARY_EXPR: return BindBinaryExpression((BinaryExpression)expr);
                default:
                    diagnostics.Add(new Diagnostic(DiagnosticType.fatal, $"unexpected syntax '{expr.type}'"));
                    return null;
            }
        }

        private BoundExpression BindLiteralExpression(LiteralExpression expr) {
            var value = expr.value ?? 0;
            return new BoundLiteralExpression(value);
        }

        private BoundExpression BindUnaryExpression(UnaryExpression expr) {
            var boundoperand = BindExpression(expr.operand);
            var boundop = BoundUnaryOperator.Bind(expr.op.type, boundoperand.ltype);

            if (boundop == null) {
                diagnostics.Add(new Diagnostic(DiagnosticType.error, $"unary operator '{boundoperand.ltype}'"));
                return boundoperand;
            }

            return new BoundUnaryExpression(boundop, boundoperand);
        }

        private BoundExpression BindBinaryExpression(BinaryExpression expr) {
            var boundleft = BindExpression(expr.left);
            var boundright = BindExpression(expr.right);
            var boundop = BoundBinaryOperator.Bind(expr.op.type, boundleft.ltype, boundright.ltype);

            if (boundop == null) {
                diagnostics.Add(new Diagnostic(DiagnosticType.error,
                    $"binary operator '{expr.op.text}' is not defined for types '{boundleft.ltype}' and '{boundright.ltype}'")
                );
                return boundleft;
            }

            return new BoundBinaryExpression(boundleft, boundop, boundright);
        }
    }
}
