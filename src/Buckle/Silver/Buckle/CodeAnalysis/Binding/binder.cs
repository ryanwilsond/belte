using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding {

    internal class Binder {
        public DiagnosticQueue diagnostics;
        private readonly Dictionary<string, object> variables_;

        public Binder(Dictionary<string, object> variables) {
            diagnostics = new DiagnosticQueue();
            variables_ = variables;
        }

        public BoundExpression BindExpression(Expression expr) {
            switch (expr.type) {
                case SyntaxType.LITERAL_EXPR: return BindLiteralExpression((LiteralExpression)expr);
                case SyntaxType.UNARY_EXPR: return BindUnaryExpression((UnaryExpression)expr);
                case SyntaxType.BINARY_EXPR: return BindBinaryExpression((BinaryExpression)expr);
                case SyntaxType.PAREN_EXPR: return BindParenExpression((ParenExpression)expr);
                case SyntaxType.NAME_EXPR: return BindNameExpression((NameExpression)expr);
                case SyntaxType.ASSIGN_EXPR: return BindAssignmentExpression((AssignmentExpression)expr);
                default:
                    diagnostics.Push(DiagnosticType.fatal, $"unexpected syntax {expr.type}");
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
                diagnostics.Push(Error.InvalidUnaryOperatorUse(expr.op.span, expr.op.text, boundoperand.ltype));
                return boundoperand;
            }

            return new BoundUnaryExpression(boundop, boundoperand);
        }

        private BoundExpression BindBinaryExpression(BinaryExpression expr) {
            var boundleft = BindExpression(expr.left);
            var boundright = BindExpression(expr.right);
            if (boundleft == null || boundright == null) return boundleft;
            var boundop = BoundBinaryOperator.Bind(expr.op.type, boundleft.ltype, boundright.ltype);

            if (boundop == null) {
                diagnostics.Push(
                    Error.InvalidBinaryOperatorUse(expr.op.span, expr.op.text, boundleft.ltype, boundright.ltype));
                return boundleft;
            }

            return new BoundBinaryExpression(boundleft, boundop, boundright);
        }

        private BoundExpression BindParenExpression(ParenExpression expr) {
            return BindExpression(expr.expr);
        }

        private BoundExpression BindNameExpression(NameExpression expr) {
            string name = expr.id.text;

            if (variables_.TryGetValue(name, out var value)) {
                var ltype = value.GetType();
                return new BoundVariableExpression(name, ltype);
            }

            diagnostics.Push(Error.UndefinedName(expr.id.span, name));
            return new BoundLiteralExpression(0);
        }

        private BoundExpression BindAssignmentExpression(AssignmentExpression expr) {
            var boundexpr = BindExpression(expr.expr);

            var defaultval =
                boundexpr.ltype == typeof(int)
                    ? (object)0
                    : boundexpr.ltype == typeof(bool)
                        ? (object)false
                        : null; // no idea what this even does

            if (defaultval == null)
                diagnostics.Push(new Diagnostic(DiagnosticType.fatal, null, $"unsupported variable type {boundexpr.ltype}"));

            variables_[expr.id.text] = defaultval;
            return new BoundAssignmentExpression(expr.id.text, boundexpr);
        }


    }
}
