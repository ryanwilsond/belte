using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding {

    internal sealed class Binder {
        public DiagnosticQueue diagnostics;
        private BoundScope scope_;

        public Binder(BoundScope parent) {
            diagnostics = new DiagnosticQueue();
            scope_ = new BoundScope(parent);
        }

        public static BoundGlobalScope BindGlobalScope(BoundGlobalScope previous, CompilationUnit expression) {
            var parentScope = CreateParentScopes(previous);
            var binder = new Binder(parentScope);
            var statements = ImmutableArray.CreateBuilder<BoundStatement>();
            foreach (var statement in expression.statements) {
                statements.Add(binder.BindStatement(statement));
            }
            var variables = binder.scope_.GetDeclaredVariables();

            if (previous != null)
                binder.diagnostics.diagnostics_.InsertRange(0, previous.diagnostics.diagnostics_);

            return new BoundGlobalScope(previous, binder.diagnostics, variables, statements.ToImmutable());
        }

        private static BoundScope CreateParentScopes(BoundGlobalScope previous) {
            var stack = new Stack<BoundGlobalScope>();
            // make all scopes cascade in order of repl statements

            while (previous != null) {
                stack.Push(previous);
                previous = previous.previous;
            }

            BoundScope parent = null;

            while (stack.Count > 0) {
                previous = stack.Pop();
                var scope = new BoundScope(parent);
                foreach (var variable in previous.variables)
                    scope.TryDeclare(variable);

                parent = scope;
            }

            return parent;
        }

        private BoundStatement BindStatement(Statement syntax) {
            switch (syntax.type) {
                case SyntaxType.BLOCK_STATEMENT: return BindBlockStatement((BlockStatement)syntax);
                case SyntaxType.EXPRESSION_STATEMENT: return BindExpressionStatement((ExpressionStatement)syntax);
                case SyntaxType.VARIABLE_DECLARATION_STATEMENT:
                    return BindVariableDeclarationStatement((VariableDeclarationStatement)syntax);
                case SyntaxType.IF_STATEMENT: return BindIfStatement((IfStatement)syntax);
                case SyntaxType.WHILE_STATEMENT: return BindWhileStatement((WhileStatement)syntax);
                case SyntaxType.FOR_STATEMENT: return BindForStatement((ForStatement)syntax);
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unexpected syntax {syntax.type}");
                    return null;
            }
        }

        private BoundExpression BindExpression(Expression expression) {
            switch (expression.type) {
                case SyntaxType.LITERAL_EXPR: return BindLiteralExpression((LiteralExpression)expression);
                case SyntaxType.UNARY_EXPR: return BindUnaryExpression((UnaryExpression)expression);
                case SyntaxType.BINARY_EXPR: return BindBinaryExpression((BinaryExpression)expression);
                case SyntaxType.PAREN_EXPR: return BindParenExpression((ParenExpression)expression);
                case SyntaxType.NAME_EXPR: return BindNameExpression((NameExpression)expression);
                case SyntaxType.ASSIGN_EXPR: return BindAssignmentExpression((AssignmentExpression)expression);
                case SyntaxType.CALL_EXPR: return BindCallExpression((CallExpression)expression);
                case SyntaxType.EMPTY_EXPR: return BindEmptyExpression((EmptyExpression)expression);
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unexpected syntax {expression.type}");
                    return null;
            }
        }

        private BoundExpression BindCallExpression(CallExpression expression) {
            var functions = BuiltinFunctions.GetAll();
            var function = functions.SingleOrDefault(f => f.name == expression.identifier.text);
            if (function == null) {
                diagnostics.Push(Error.UndefinedFunction(expression.identifier.span, expression.identifier.text));
                return new BoundErrorExpression();
            }

            if (expression.arguments.count != function.parameters.Length) {
                diagnostics.Push(Error.IncorrectArgumentsCount(
                    expression.span, function.name, function.parameters.Length, expression.arguments.count));
                return new BoundErrorExpression();
            }

            var arguments = ImmutableArray.CreateBuilder<BoundExpression>();

            for (int i=0; i<expression.arguments.count; i++) {
                var parameter = function.parameters[i];
                // var argument = ; // 55:46

                // if (argument.lType != parameter)
            }

            return new BoundErrorExpression();
        }

        private BoundExpression BindExpression(Expression expression, TypeSymbol target) {
            var result = BindExpression(expression);
            if (target != TypeSymbol.Error && result.lType != TypeSymbol.Error && result.lType != target)
                diagnostics.Push(Error.CannotConvert(expression.span, result.lType, target));

            return result;
        }

        private BoundStatement BindWhileStatement(WhileStatement statement) {
            var condition = BindExpression(statement.condition);
            var body = BindStatement(statement.body);
            return new BoundWhileStatement(condition, body);
        }

        private BoundStatement BindForStatement(ForStatement statement) {
            scope_ = new BoundScope(scope_);

            var initializer = BindStatement(statement.initializer);
            var condition = BindExpression(statement.condition);
            var step = BindExpression(statement.step);
            var body = BindStatement(statement.body);

            scope_ = scope_.parent;
            return new BoundForStatement(initializer, condition, step, body);
        }

        private BoundStatement BindIfStatement(IfStatement statement) {
            var condition = BindExpression(statement.condition, TypeSymbol.Bool);
            var then = BindStatement(statement.then);
            var elseStatement = statement.elseClause == null ? null : BindStatement(statement.elseClause.then);
            return new BoundIfStatement(condition, then, elseStatement);
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
            var expression = BindExpression(statement.expression);
            return new BoundExpressionStatement(expression);
        }

        private BoundExpression BindLiteralExpression(LiteralExpression expression) {
            var value = expression.value ?? 0;
            return new BoundLiteralExpression(value);
        }

        private BoundExpression BindUnaryExpression(UnaryExpression expression) {
            var boundOperand = BindExpression(expression.operand);

            if (boundOperand.lType == TypeSymbol.Error)
                return new BoundErrorExpression();

            var boundOp = BoundUnaryOperator.Bind(expression.op.type, boundOperand.lType);

            if (boundOp == null) {
                diagnostics.Push(
                    Error.InvalidUnaryOperatorUse(expression.op.span, expression.op.text, boundOperand.lType));
                return new BoundErrorExpression();
            }

            return new BoundUnaryExpression(boundOp, boundOperand);
        }

        private BoundExpression BindBinaryExpression(BinaryExpression expression) {
            var boundLeft = BindExpression(expression.left);
            var boundRight = BindExpression(expression.right);

            if (boundLeft.lType == TypeSymbol.Error || boundRight.lType == TypeSymbol.Error)
                return new BoundErrorExpression();

            var boundOp = BoundBinaryOperator.Bind(expression.op.type, boundLeft.lType, boundRight.lType);

            if (boundOp == null) {
                diagnostics.Push(
                    Error.InvalidBinaryOperatorUse(
                        expression.op.span, expression.op.text, boundLeft.lType, boundRight.lType));
                return new BoundErrorExpression();
            }

            return new BoundBinaryExpression(boundLeft, boundOp, boundRight);
        }

        private BoundExpression BindParenExpression(ParenExpression expression) {
            return BindExpression(expression.expression);
        }

        private BoundExpression BindNameExpression(NameExpression expression) {
            string name = expression.identifier.text;
            if (expression.identifier.isMissing)
                return new BoundErrorExpression();

            if (scope_.TryLookup(name, out var variable))
                return new BoundVariableExpression(variable);

            diagnostics.Push(Error.UndefinedName(expression.identifier.span, name));
            return new BoundErrorExpression();
        }

        private BoundExpression BindEmptyExpression(EmptyExpression expression) {
            return new BoundEmptyExpression();
        }

        private BoundExpression BindAssignmentExpression(AssignmentExpression expression) {
            var name = expression.identifier.text;
            var boundExpression = BindExpression(expression.expression);

            if (!scope_.TryLookup(name, out var variable)) {
                diagnostics.Push(Error.UndefinedName(expression.identifier.span, name));
                return boundExpression;
            }

            if (variable.isReadOnly)
                diagnostics.Push(Error.ReadonlyAssign(expression.equals.span, name));

            if (boundExpression.lType != variable.lType) {
                diagnostics.Push(
                    Error.CannotConvert(expression.expression.span, boundExpression.lType, variable.lType));
                return boundExpression;
            }

            return new BoundAssignmentExpression(variable, boundExpression);
        }

        private BoundStatement BindVariableDeclarationStatement(VariableDeclarationStatement expression) {
            var isReadOnly = expression.keyword.type == SyntaxType.LET_KEYWORD;
            var initializer = BindExpression(expression.initializer);
            var variable = BindVariable(expression.identifier, isReadOnly, initializer.lType);

            return new BoundVariableDeclarationStatement(variable, initializer);
        }

        private VariableSymbol BindVariable(Token identifier, bool isReadOnly, TypeSymbol type) {
            var name = identifier.text ?? "?";
            var declare = !identifier.isMissing;
            var variable = new VariableSymbol(name, isReadOnly, type);

            if (declare && !scope_.TryDeclare(variable))
                diagnostics.Push(Error.AlreadyDeclared(identifier.span, name));

            return variable;
        }
    }
}
