using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding {

    internal sealed class Binder {
        public DiagnosticQueue diagnostics;
        private BoundScope scope_;

        public Binder(BoundScope parent) {
            diagnostics = new DiagnosticQueue();
            scope_ = new BoundScope(parent);
        }

        public static BoundGlobalScope BindGlobalScope(BoundGlobalScope prev, CompilationUnit expr) {
            var parentScope = CreateParentScopes(prev);
            var binder = new Binder(parentScope);
            var statement = binder.BindStatement(expr.statement);
            var variables = binder.scope_.GetDeclaredVariables();

            if (prev != null)
                binder.diagnostics.diagnostics_.InsertRange(0, prev.diagnostics.diagnostics_);

            return new BoundGlobalScope(prev, binder.diagnostics, variables, statement);
        }

        private static BoundScope CreateParentScopes(BoundGlobalScope prev) {
            var stack = new Stack<BoundGlobalScope>();
            // make all scopes cascade in order of repl statements

            while (prev != null) {
                stack.Push(prev);
                prev = prev.previous;
            }

            BoundScope parent = null;

            while (stack.Count > 0) {
                prev = stack.Pop();
                var scope = new BoundScope(parent);
                foreach (var variable in prev.variables)
                    scope.TryDeclare(variable);

                parent = scope;
            }

            return parent;
        }

        private BoundStatement BindStatement(Statement syntax) {
            switch (syntax.type) {
                case SyntaxType.BLOCK_STATEMENT: return BindBlockStatement((BlockStatement)syntax);
                case SyntaxType.EXPRESSION_STATEMENT: return BindExpressionStatement((ExpressionStatement)syntax);
                case SyntaxType.VARIABLE_DECLARATION_STATEMENT: return BindVariableDeclaration((VariableDeclaration)syntax);
                default:
                    diagnostics.Push(DiagnosticType.fatal, $"unexpected syntax {syntax.type}");
                    return null;
            }
        }

        private BoundExpression BindExpression(Expression expr) {
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

        private BoundStatement BindBlockStatement(BlockStatement statement) {
            var statements = ImmutableArray.CreateBuilder<BoundStatement>();
            scope_ = new BoundScope(scope_);

            foreach (var statementSyntax in statement.statements) {
                var state = BindStatement(statementSyntax);
                statements.Add(state);
            }

            scope_ = scope_.parent;

            return new BoundBlockStatement(statements.ToImmutable());
        }

        private BoundStatement BindExpressionStatement(ExpressionStatement statement) {
            var expression = BindExpression(statement.expr);
            return new BoundExpressionStatement(expression);
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

            if (scope_.TryLookup(name, out var variable)) {
                return new BoundVariableExpression(variable);
            }

            diagnostics.Push(Error.UndefinedName(expr.id.span, name));
            return new BoundLiteralExpression(0);
        }

        private BoundExpression BindAssignmentExpression(AssignmentExpression expr) {
            var name = expr.id.text;
            var boundexpr = BindExpression(expr.expr);

            if (!scope_.TryLookup(name, out var variable)) {
                diagnostics.Push(Error.UndefinedName(expr.id.span, name));
                return boundexpr;
            }

            if (variable.is_read_only)
                diagnostics.Push(Error.ReadonlyAssign(expr.equals.span, name));

            if (boundexpr.ltype != variable.ltype) {
                diagnostics.Push(Error.CannotConvert(expr.expr.span, boundexpr.ltype, variable.ltype));
                return boundexpr;
            }

            return new BoundAssignmentExpression(variable, boundexpr);
        }

        private BoundStatement BindVariableDeclaration(VariableDeclaration expr) {
            var name = expr.id.text;
            var isReadOnly = expr.keyword.type == SyntaxType.LET_KEYWORD;
            var expression = BindExpression(expr.init);
            var variable = new VariableSymbol(name, isReadOnly, expression.ltype);

            if (!scope_.TryDeclare(variable)) {
                diagnostics.Push(Error.AlreadyDeclared(expr.id.span, name));
            }

            return new BoundVariableDeclaration(variable, expression);
        }
    }
}
