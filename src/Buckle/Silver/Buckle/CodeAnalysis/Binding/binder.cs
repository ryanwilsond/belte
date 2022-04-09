using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Binding {

    internal sealed class Binder {
        public DiagnosticQueue diagnostics;
        private BoundScope scope_;
        private readonly FunctionSymbol function_;
        private Stack<(BoundLabel breakLabel, BoundLabel continueLabel)> loopStack_ =
            new Stack<(BoundLabel breakLabel, BoundLabel continueLabel)>();
        private int labelCount_;

        public Binder(BoundScope parent, FunctionSymbol function) {
            diagnostics = new DiagnosticQueue();
            scope_ = new BoundScope(parent);
            function_ = function;

            if (function != null) {
                foreach (var parameter in function.parameters)
                    scope_.TryDeclareVariable(parameter);
            }
        }

        public static BoundGlobalScope BindGlobalScope(BoundGlobalScope previous, CompilationUnit expression) {
            var parentScope = CreateParentScope(previous);
            var binder = new Binder(parentScope, null);

            foreach (var function in expression.members.OfType<FunctionDeclaration>())
                binder.BindFunctionDeclaration(function);

            var statements = ImmutableArray.CreateBuilder<BoundStatement>();
            foreach (var globalStatement in expression.members.OfType<GlobalStatement>())
                statements.Add(binder.BindStatement(globalStatement.statement));

            var functions = binder.scope_.GetDeclaredFunctions();
            var variables = binder.scope_.GetDeclaredVariables();

            if (previous != null)
                binder.diagnostics.diagnostics_.InsertRange(0, previous.diagnostics.diagnostics_);

            return new BoundGlobalScope(previous, binder.diagnostics, functions, variables, statements.ToImmutable());
        }

        public static BoundProgram BindProgram(BoundGlobalScope globalScope) {
            var parentScope = CreateParentScope(globalScope);
            var functionBodies = ImmutableDictionary.CreateBuilder<FunctionSymbol, BoundBlockStatement>();
            var diagnostics = new DiagnosticQueue();

            var scope = globalScope;
            while (scope != null) {
                foreach (var function in scope.functions) {
                    var binder = new Binder(parentScope, function);
                    var body = binder.BindStatement(function.declaration.body);
                    var loweredBody = Lowerer.Lower(body);
                    functionBodies.Add(function, loweredBody);
                    diagnostics.Move(binder.diagnostics);
                }

                scope = scope.previous;
            }

            var statement = Lowerer.Lower(new BoundBlockStatement(globalScope.statements));
            return new BoundProgram(diagnostics, functionBodies.ToImmutable(), statement);
        }

        private void BindFunctionDeclaration(FunctionDeclaration function) {
            var type = BindTypeClause(function.typeName);
            var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();
            var seenParametersNames = new HashSet<string>();

            foreach (var parameter in function.parameters) {
                var parameterName = parameter.identifier.text;
                var parameterType = BindTypeClause(parameter.typeName);

                if (!seenParametersNames.Add(parameterName)) {
                    diagnostics.Push(Error.ParameterAlreadyDeclared(parameter.span, parameter.identifier.text));
                } else {
                    var boundParameter = new ParameterSymbol(parameterName, parameterType);
                    parameters.Add(boundParameter);
                }
            }

            var newFunction = new FunctionSymbol(function.identifier.text, parameters.ToImmutable(), type, function);
            if (!scope_.TryDeclareFunction(newFunction))
                diagnostics.Push(Error.FunctionAlreadyDeclared(function.identifier.span, newFunction.name));
        }

        private static BoundScope CreateParentScope(BoundGlobalScope previous) {
            var stack = new Stack<BoundGlobalScope>();

            while (previous != null) {
                stack.Push(previous);
                previous = previous.previous;
            }

            var parent = CreateRootScope();

            while (stack.Count > 0) {
                previous = stack.Pop();
                var scope = new BoundScope(parent);

                foreach (var function in previous.functions)
                    scope.TryDeclareFunction(function);

                foreach (var variable in previous.variables)
                    scope.TryDeclareVariable(variable);

                parent = scope;
            }

            return parent;
        }

        private static BoundScope CreateRootScope() {
            var result = new BoundScope(null);

            foreach (var f in BuiltinFunctions.GetAll())
                result.TryDeclareFunction(f);

            return result;
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
                case SyntaxType.DO_WHILE_STATEMENT: return BindDoWhileStatement((DoWhileStatement)syntax);
                case SyntaxType.BREAK_STATEMENT: return BindBreakStatement((BreakStatement)syntax);
                case SyntaxType.CONTINUE_STATEMENT: return BindContinueStatement((ContinueStatement)syntax);
                case SyntaxType.RETURN_STATEMENT: return BindReturnStatement((ReturnStatement)syntax);
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unexpected syntax {syntax.type}");
                    return null;
            }
        }

        private BoundStatement BindReturnStatement(ReturnStatement expression) {
            var boundExpression = expression.expression == null ? null : BindExpression(expression.expression);

            if (function_ == null) {
                diagnostics.Push(Error.ReturnOutsideFunction(expression.keyword.span));
                return new BoundExpressionStatement(new BoundErrorExpression());
            }

            if (function_.lType == TypeSymbol.Void) {
                if (boundExpression != null)
                    diagnostics.Push(Error.UnexpectedReturnValue(expression.keyword.span));
            } else {
                if (boundExpression == null)
                    diagnostics.Push(Error.MissingReturnValue(expression.keyword.span));
                else
                    boundExpression = BindCast(expression.expression.span, boundExpression, function_.lType);
            }

            return new BoundReturnStatement(boundExpression);
        }

        private BoundExpression BindExpression(Expression expression, bool canBeVoid=false) {
            var result = BindExpressionInternal(expression);

            if (!canBeVoid && result.lType == TypeSymbol.Void) {
                diagnostics.Push(Error.NoValue(expression.span));
                return new BoundErrorExpression();
            }

            return result;
        }

        private BoundExpression BindExpressionInternal(Expression expression) {
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
            if (expression.arguments.count == 1 && LookupType(expression.identifier.text) is TypeSymbol type)
                return BindCast(expression.arguments[0], type, true);

            var boundArguments = ImmutableArray.CreateBuilder<BoundExpression>();
            foreach (var argument in expression.arguments) {
                var boundArgument = BindExpression(argument);
                boundArguments.Add(boundArgument);
            }

            if (!scope_.TryLookupFunction(expression.identifier.text, out var function)) {
                if (scope_.TryLookupVariable(expression.identifier.text, out _))
                    diagnostics.Push(
                        Error.CannotCallNonFunction(expression.identifier.span, expression.identifier.text));
                else
                    diagnostics.Push(Error.UndefinedFunction(expression.identifier.span, expression.identifier.text));

                return new BoundErrorExpression();
            }

            if (function == null) {
                diagnostics.Push(Error.UndefinedFunction(expression.identifier.span, expression.identifier.text));
                return new BoundErrorExpression();
            }

            if (expression.arguments.count != function.parameters.Length) {
                TextSpan span;

                if (expression.arguments.count > function.parameters.Length) {
                    Node firstExceedingNode;
                    if (function.parameters.Length > 0)
                        firstExceedingNode = expression.arguments.GetSeparator(function.parameters.Length - 1);
                    else
                        firstExceedingNode = expression.arguments[0];
                    var lastExceedingNode = expression.arguments.Last();
                    span = TextSpan.FromBounds(firstExceedingNode.span.start, lastExceedingNode.span.end);
                } else {
                    span = expression.closeParenthesis.span;
                }

                diagnostics.Push(Error.IncorrectArgumentsCount(
                    span, function.name, function.parameters.Length, expression.arguments.count));
                return new BoundErrorExpression();
            }

            bool hasErrors = false;
            for (int i=0; i<expression.arguments.count; i++) {
                var argument = boundArguments[i];
                var parameter = function.parameters[i];

                if (argument.lType != parameter.lType) {
                    if (argument.lType != TypeSymbol.Error)
                        diagnostics.Push(Error.InvalidArgumentType(
                            expression.arguments[i].span, parameter.name, parameter.lType, argument.lType));
                    hasErrors = true;
                }
            }

            if (hasErrors)
                return new BoundErrorExpression();

            return new BoundCallExpression(function, boundArguments.ToImmutable());
        }

        private BoundExpression BindCast(Expression expression, TypeSymbol type, bool allowExplicit = false) {
            var boundExpression = BindExpression(expression);
            return BindCast(expression.span, boundExpression, type, allowExplicit);
        }

        private BoundExpression BindCast(
            TextSpan diagnosticSpan, BoundExpression expression, TypeSymbol type, bool allowExplicit = false) {
            var conversion = Cast.Classify(expression.lType, type);

            if (!conversion.exists) {
                if (expression.lType != TypeSymbol.Error && type != TypeSymbol.Error)
                    diagnostics.Push(Error.CannotConvert(diagnosticSpan, expression.lType, type));

                return new BoundErrorExpression();
            }

            if (!allowExplicit && conversion.isExplicit) {
                diagnostics.Push(Error.CannotConvertImplicitly(diagnosticSpan, expression.lType, type));
            }

            if (conversion.isIdentity)
                return expression;

            return new BoundCastExpression(type, expression);
        }

        private BoundExpression BindExpression(Expression expression, TypeSymbol target) {
            return BindCast(expression, target);
        }

        private BoundStatement BindErrorStatement() {
            return new BoundExpressionStatement(new BoundErrorExpression());
        }

        private BoundStatement BindContinueStatement(ContinueStatement syntax) {
            if (loopStack_.Count == 0) {
                diagnostics.Push(Error.InvalidBreakOrContinue(syntax.keyword.span, syntax.keyword.text));
                return BindErrorStatement();
            }

            var continueLabel = loopStack_.Peek().continueLabel;
            return new BoundGotoStatement(continueLabel);
        }

        private BoundStatement BindBreakStatement(BreakStatement syntax) {
            if (loopStack_.Count == 0) {
                diagnostics.Push(Error.InvalidBreakOrContinue(syntax.keyword.span, syntax.keyword.text));
                return BindErrorStatement();
            }

            var breakLabel = loopStack_.Peek().breakLabel;
            return new BoundGotoStatement(breakLabel);
        }

        private BoundStatement BindWhileStatement(WhileStatement statement) {
            var condition = BindExpression(statement.condition, TypeSymbol.Bool);
            var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel);
            return new BoundWhileStatement(condition, body, breakLabel, continueLabel);
        }

        private BoundStatement BindDoWhileStatement(DoWhileStatement statement) {
            var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel);
            var condition = BindExpression(statement.condition, TypeSymbol.Bool);
            return new BoundDoWhileStatement(body, condition, breakLabel, continueLabel);
        }

        private BoundStatement BindForStatement(ForStatement statement) {
            scope_ = new BoundScope(scope_);

            var initializer = BindStatement(statement.initializer);
            var condition = BindExpression(statement.condition, TypeSymbol.Bool);
            var step = BindExpression(statement.step);
            var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel);

            scope_ = scope_.parent;
            return new BoundForStatement(initializer, condition, step, body, breakLabel, continueLabel);
        }

        private BoundStatement BindLoopBody(Statement body, out BoundLabel breakLabel, out BoundLabel continueLabel) {
            labelCount_++;
            breakLabel = new BoundLabel($"Break{labelCount_}");
            continueLabel = new BoundLabel($"Continue{labelCount_}");

            loopStack_.Push((breakLabel, continueLabel));
            var boundBody = BindStatement(body);
            loopStack_.Pop();

            return boundBody;
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
            var expression = BindExpression(statement.expression, true);
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

            if (scope_.TryLookupVariable(name, out var variable))
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

            if (!scope_.TryLookupVariable(name, out var variable)) {
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
            var isReadOnly = expression.typeName.type == SyntaxType.LET_KEYWORD;
            var type = BindTypeClause(expression.typeName);
            var initializer = BindExpression(expression.initializer);
            var variableType = type ?? initializer.lType;
            var castedInitializer = BindCast(expression.initializer.span, initializer, variableType);
            var variable = BindVariable(expression.identifier, isReadOnly, variableType);

            return new BoundVariableDeclarationStatement(variable, castedInitializer);
        }

        private TypeSymbol BindTypeClause(Token type) {
            if (type.type == SyntaxType.AUTO_KEYWORD || type.type == SyntaxType.LET_KEYWORD)
                return null;

            var foundType = LookupType(type.text);
            if (foundType == null)
                diagnostics.Push(Error.UnknownType(type.span, type.text));

            return foundType;
        }

        private VariableSymbol BindVariable(Token identifier, bool isReadOnly, TypeSymbol type) {
            var name = identifier.text ?? "?";
            var declare = !identifier.isMissing;
            var variable = function_ == null
                ? (VariableSymbol) new GlobalVariableSymbol(name, isReadOnly, type)
                : new LocalVariableSymbol(name, isReadOnly, type);

            if (declare && !scope_.TryDeclareVariable(variable))
                diagnostics.Push(Error.AlreadyDeclared(identifier.span, name));

            return variable;
        }

        private TypeSymbol LookupType(string name) {
            switch (name) {
                case "bool": return TypeSymbol.Bool;
                case "int": return TypeSymbol.Int;
                case "string": return TypeSymbol.String;
                case "void": return TypeSymbol.Void;
                default: return null;
            }
        }
    }
}
