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
        private readonly bool isScript_;
        private readonly FunctionSymbol function_;
        private Stack<(BoundLabel breakLabel, BoundLabel continueLabel)> loopStack_ =
            new Stack<(BoundLabel breakLabel, BoundLabel continueLabel)>();
        private int labelCount_;

        private Binder(bool isScript, BoundScope parent, FunctionSymbol function) {
            isScript_ = isScript;
            diagnostics = new DiagnosticQueue();
            scope_ = new BoundScope(parent);
            function_ = function;

            if (function != null) {
                foreach (var parameter in function.parameters)
                    scope_.TryDeclareVariable(parameter);
            }
        }

        public static BoundGlobalScope BindGlobalScope(
            bool isScript, BoundGlobalScope previous, ImmutableArray<SyntaxTree> syntaxTrees) {
            var parentScope = CreateParentScope(previous);
            var binder = new Binder(isScript, parentScope, null);

            foreach (var syntaxTree in syntaxTrees)
                binder.diagnostics.Move(syntaxTree.diagnostics);

            if (binder.diagnostics.FilterOut(DiagnosticType.Warning).Any())
                return new BoundGlobalScope(
                    previous, binder.diagnostics, null, null, ImmutableArray<FunctionSymbol>.Empty,
                    ImmutableArray<VariableSymbol>.Empty, ImmutableArray<BoundStatement>.Empty);

            var functionDeclarations = syntaxTrees.SelectMany(st => st.root.members).OfType<FunctionDeclaration>();

            foreach (var function in functionDeclarations)
                binder.BindFunctionDeclaration(function);

            var globalStatements = syntaxTrees.SelectMany(st => st.root.members).OfType<GlobalStatement>();

            var statements = ImmutableArray.CreateBuilder<BoundStatement>();
            foreach (var globalStatement in globalStatements)
                statements.Add(binder.BindStatement(globalStatement.statement, true));

            var firstGlobalPerTree = syntaxTrees
                .Select(st => st.root.members.OfType<GlobalStatement>().FirstOrDefault())
                .Where(g => g != null).ToArray();

            if (firstGlobalPerTree.Length > 1)
                foreach (var globalStatement in firstGlobalPerTree)
                    binder.diagnostics.Push(Error.GlobalStatementsInMultipleFiles(globalStatement.location));

            var functions = binder.scope_.GetDeclaredFunctions();

            FunctionSymbol mainFunction;
            FunctionSymbol scriptFunction;

            if (isScript) {
                if (globalStatements.Any())
                    scriptFunction = new FunctionSymbol("$eval", ImmutableArray<ParameterSymbol>.Empty, TypeSymbol.Any);
                else
                    scriptFunction = null;

                mainFunction = null;
            } else {
                scriptFunction = null;
                mainFunction = functions.FirstOrDefault(f => f.name == "main");

                if (mainFunction != null)
                    if ((mainFunction.lType != TypeSymbol.Void && mainFunction.lType != TypeSymbol.Int) ||
                        mainFunction.parameters.Any())
                        binder.diagnostics.Push(Error.InvalidMain(mainFunction.declaration.location));

                if (globalStatements.Any()) {
                    if (mainFunction != null) {
                        binder.diagnostics.Push(Error.MainAndGlobals(mainFunction.declaration.identifier.location));

                        foreach (var globalStatement in firstGlobalPerTree)
                            binder.diagnostics.Push(Error.MainAndGlobals(globalStatement.location));
                    } else {
                        mainFunction = new FunctionSymbol(
                            "main", ImmutableArray<ParameterSymbol>.Empty, TypeSymbol.Void);
                    }
                }
            }

            var variables = binder.scope_.GetDeclaredVariables();

            if (previous != null)
                binder.diagnostics.diagnostics_.InsertRange(0, previous.diagnostics.diagnostics_);

            return new BoundGlobalScope(previous, binder.diagnostics, mainFunction,
                scriptFunction, functions, variables, statements.ToImmutable());
        }

        public static BoundProgram BindProgram(bool isScript, BoundProgram previous, BoundGlobalScope globalScope) {
            var parentScope = CreateParentScope(globalScope);

            if (globalScope.diagnostics.FilterOut(DiagnosticType.Warning).Any())
                return new BoundProgram(previous, globalScope.diagnostics,
                    null, null, ImmutableDictionary<FunctionSymbol, BoundBlockStatement>.Empty);

            var functionBodies = ImmutableDictionary.CreateBuilder<FunctionSymbol, BoundBlockStatement>();
            var diagnostics = new DiagnosticQueue();

            foreach (var function in globalScope.functions) {
                var binder = new Binder(isScript, parentScope, function);
                var body = binder.BindStatement(function.declaration.body);
                var loweredBody = Lowerer.Lower(function, body);

                if (function.lType != TypeSymbol.Void && !ControlFlowGraph.AllPathsReturn(loweredBody))
                    binder.diagnostics.Push(Error.NotAllPathsReturn(function.declaration.identifier.location));

                functionBodies.Add(function, loweredBody);
                diagnostics.Move(binder.diagnostics);
            }

            if (globalScope.mainFunction != null && globalScope.statements.Any()) {
                var body = Lowerer.Lower(globalScope.mainFunction, new BoundBlockStatement(globalScope.statements));
                functionBodies.Add(globalScope.mainFunction, body);
            } else if (globalScope.scriptFunction != null) {
                var statements = globalScope.statements;

                if (statements.Length == 1 &&
                    statements[0] is BoundExpressionStatement es &&
                    es.expression.lType != TypeSymbol.Void) {
                    statements = statements.SetItem(0, new BoundReturnStatement(es.expression));
                } else if (statements.Any() && statements.Last().type != BoundNodeType.ReturnStatement) {
                    var nullValue = new BoundLiteralExpression("");
                    statements = statements.Add(new BoundReturnStatement(nullValue));
                }

                var body = Lowerer.Lower(globalScope.scriptFunction, new BoundBlockStatement(statements));
                functionBodies.Add(globalScope.scriptFunction, body);
            }

            return new BoundProgram(previous, diagnostics, globalScope.mainFunction,
                globalScope.scriptFunction, functionBodies.ToImmutable());
        }

        private void BindFunctionDeclaration(FunctionDeclaration function) {
            var type = BindTypeClause(function.typeName);
            var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();
            var seenParametersNames = new HashSet<string>();

            foreach (var parameter in function.parameters) {
                var parameterName = parameter.identifier.text;
                var parameterType = BindTypeClause(parameter.typeName);

                if (!seenParametersNames.Add(parameterName)) {
                    diagnostics.Push(Error.ParameterAlreadyDeclared(parameter.location, parameter.identifier.text));
                } else {
                    var boundParameter = new ParameterSymbol(parameterName, parameterType, parameters.Count);
                    parameters.Add(boundParameter);
                }
            }

            var newFunction = new FunctionSymbol(function.identifier.text, parameters.ToImmutable(), type, function);
            if (newFunction.declaration.identifier.text != null && !scope_.TryDeclareFunction(newFunction))
                diagnostics.Push(Error.FunctionAlreadyDeclared(function.identifier.location, newFunction.name));
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

        private BoundStatement BindStatement(Statement syntax, bool isGlobal = false) {
            var result = BindStatementInternal(syntax);

            if (!isScript_ || !isGlobal) {
                if (result is BoundExpressionStatement es) {
                    var isAllowedExpression = es.expression.type == BoundNodeType.CallExpression ||
                        es.expression.type == BoundNodeType.AssignmentExpression ||
                        es.expression.type == BoundNodeType.ErrorExpression ||
                        es.expression.type == BoundNodeType.EmptyExpression;

                    if (!isAllowedExpression)
                        diagnostics.Push(Error.InvalidExpressionStatement(syntax.location));
                }
            }

            return result;
        }

        private BoundStatement BindStatementInternal(Statement syntax) {
            switch (syntax.type) {
                case SyntaxType.BLOCK: return BindBlockStatement((BlockStatement)syntax);
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
                if (isScript_) {
                    if (boundExpression == null)
                        boundExpression = new BoundLiteralExpression("");
                } else if (boundExpression != null) {
                    diagnostics.Push(Error.Unsupported.GlobalReturnValue(expression.keyword.location));
                }
            } else {
                if (function_.lType == TypeSymbol.Void) {
                    if (boundExpression != null)
                        diagnostics.Push(Error.UnexpectedReturnValue(expression.keyword.location));
                } else {
                    if (boundExpression == null)
                        diagnostics.Push(Error.MissingReturnValue(expression.keyword.location));
                    else
                        boundExpression = BindCast(expression.expression.location, boundExpression, function_.lType);
                }
            }

            return new BoundReturnStatement(boundExpression);
        }

        private BoundExpression BindExpression(Expression expression, bool canBeVoid=false) {
            var result = BindExpressionInternal(expression);

            if (!canBeVoid && result.lType == TypeSymbol.Void) {
                diagnostics.Push(Error.NoValue(expression.location));
                return new BoundErrorExpression();
            }

            return result;
        }

        private BoundExpression BindExpressionInternal(Expression expression) {
            switch (expression.type) {
                case SyntaxType.LITERAL_EXPRESSION: return BindLiteralExpression((LiteralExpression)expression);
                case SyntaxType.UNARY_EXPRESSION: return BindUnaryExpression((UnaryExpression)expression);
                case SyntaxType.BINARY_EXPRESSION: return BindBinaryExpression((BinaryExpression)expression);
                case SyntaxType.PARENTHESIZED_EXPRESSION: return BindParenExpression((ParenthesisExpression)expression);
                case SyntaxType.NAME_EXPRESSION: return BindNameExpression((NameExpression)expression);
                case SyntaxType.ASSIGN_EXPRESSION: return BindAssignmentExpression((AssignmentExpression)expression);
                case SyntaxType.CALL_EXPRESSION: return BindCallExpression((CallExpression)expression);
                case SyntaxType.EMPTY_EXPRESSION: return BindEmptyExpression((EmptyExpression)expression);
                default:
                    diagnostics.Push(DiagnosticType.Fatal, $"unexpected syntax {expression.type}");
                    return null;
            }
        }

        private BoundExpression BindCallExpression(CallExpression expression) {
            if (expression.arguments.count == 1 && LookupType(expression.identifier.text) is TypeSymbol type)
                return BindCast(expression.arguments[0], type, true);

            var symbol = scope_.LookupSymbol(expression.identifier.text);
            if (symbol == null) {
                diagnostics.Push(
                    Error.UndefinedFunction(expression.identifier.location, expression.identifier.text));
                return new BoundErrorExpression();
            }

            var function = symbol as FunctionSymbol;
            if (function == null) {
                diagnostics.Push(
                    Error.CannotCallNonFunction(expression.identifier.location, expression.identifier.text));
                return new BoundErrorExpression();
            }

            if (function == null) {
                diagnostics.Push(Error.UndefinedFunction(expression.identifier.location, expression.identifier.text));
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

                var location = new TextLocation(expression.syntaxTree.text, span);
                diagnostics.Push(Error.IncorrectArgumentsCount(
                    location, function.name, function.parameters.Length, expression.arguments.count));
                return new BoundErrorExpression();
            }

            var boundArguments = ImmutableArray.CreateBuilder<BoundExpression>();
            for (int i=0; i<expression.arguments.count; i++) {
                var argument = expression.arguments[i];
                var parameter = function.parameters[i];
                var boundArgument = BindCast(argument.location, BindExpression(argument), parameter.lType );
                boundArguments.Add(boundArgument);
            }

            return new BoundCallExpression(function, boundArguments.ToImmutable());
        }

        private BoundExpression BindCast(Expression expression, TypeSymbol type, bool allowExplicit = false) {
            var boundExpression = BindExpression(expression);
            return BindCast(expression.location, boundExpression, type, allowExplicit);
        }

        private BoundExpression BindCast(
            TextLocation diagnosticLocation, BoundExpression expression, TypeSymbol type, bool allowExplicit = false) {
            var conversion = Cast.Classify(expression.lType, type);

            if (!conversion.exists) {
                if (expression.lType != TypeSymbol.Error && type != TypeSymbol.Error)
                    diagnostics.Push(Error.CannotConvert(diagnosticLocation, expression.lType, type));

                return new BoundErrorExpression();
            }

            if (!allowExplicit && conversion.isExplicit) {
                diagnostics.Push(Error.CannotConvertImplicitly(diagnosticLocation, expression.lType, type));
            }

            if (conversion.isIdentity)
                return expression;

            return new BoundCastExpression(type, expression);
        }

        private BoundStatement BindErrorStatement() {
            return new BoundExpressionStatement(new BoundErrorExpression());
        }

        private BoundStatement BindContinueStatement(ContinueStatement syntax) {
            if (loopStack_.Count == 0) {
                diagnostics.Push(Error.InvalidBreakOrContinue(syntax.keyword.location, syntax.keyword.text));
                return BindErrorStatement();
            }

            var continueLabel = loopStack_.Peek().continueLabel;
            return new BoundGotoStatement(continueLabel);
        }

        private BoundStatement BindBreakStatement(BreakStatement syntax) {
            if (loopStack_.Count == 0) {
                diagnostics.Push(Error.InvalidBreakOrContinue(syntax.keyword.location, syntax.keyword.text));
                return BindErrorStatement();
            }

            var breakLabel = loopStack_.Peek().breakLabel;
            return new BoundGotoStatement(breakLabel);
        }

        private BoundStatement BindWhileStatement(WhileStatement statement) {
            var condition = BindCast(statement.condition, TypeSymbol.Bool);

            if (condition.constantValue != null) {
                if (!(bool)condition.constantValue.value)
                    diagnostics.Push(Warning.UnreachableCode(statement.body.location));
            }

            var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel);
            return new BoundWhileStatement(condition, body, breakLabel, continueLabel);
        }

        private BoundStatement BindDoWhileStatement(DoWhileStatement statement) {
            var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel);
            var condition = BindCast(statement.condition, TypeSymbol.Bool);
            return new BoundDoWhileStatement(body, condition, breakLabel, continueLabel);
        }

        private BoundStatement BindForStatement(ForStatement statement) {
            scope_ = new BoundScope(scope_);

            var initializer = BindStatement(statement.initializer);
            var condition = BindCast(statement.condition, TypeSymbol.Bool);
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
            var condition = BindCast(statement.condition, TypeSymbol.Bool);

            if (condition.constantValue != null) {
                if ((bool)condition.constantValue.value == false)
                    diagnostics.Push(Warning.UnreachableCode(statement.then.location));
                else if (statement.elseClause != null)
                    diagnostics.Push(Warning.UnreachableCode(statement.elseClause.then.location));
            }

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
                    Error.InvalidUnaryOperatorUse(expression.op.location, expression.op.text, boundOperand.lType));
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
                        expression.op.location, expression.op.text, boundLeft.lType, boundRight.lType));
                return new BoundErrorExpression();
            }

            return new BoundBinaryExpression(boundLeft, boundOp, boundRight);
        }

        private BoundExpression BindParenExpression(ParenthesisExpression expression) {
            return BindExpression(expression.expression);
        }

        private BoundExpression BindNameExpression(NameExpression expression) {
            string name = expression.identifier.text;
            if (expression.identifier.isMissing)
                return new BoundErrorExpression();

            var variable = BindVariableReference(expression.identifier);
            if (variable == null)
                return new BoundErrorExpression();

            return new BoundVariableExpression(variable);
        }

        private BoundExpression BindEmptyExpression(EmptyExpression expression) {
            return new BoundEmptyExpression();
        }

        private BoundExpression BindAssignmentExpression(AssignmentExpression expression) {
            var name = expression.identifier.text;
            var boundExpression = BindExpression(expression.expression);

            var variable = BindVariableReference(expression.identifier);
            if (variable == null)
                return boundExpression;

            if (variable.isReadOnly)
                diagnostics.Push(Error.ReadonlyAssign(expression.equals.location, name));

            var convertedExpression = BindCast(expression.expression.location, boundExpression, variable.lType);
            return new BoundAssignmentExpression(variable, convertedExpression);
        }

        private VariableSymbol BindVariableReference(Token identifier) {
            var name = identifier.text;
            switch (scope_.LookupSymbol(name)) {
                case VariableSymbol variable:
                    return variable;
                case null:
                    diagnostics.Push(Error.UndefinedName(identifier.location, name));
                    return null;
                default:
                    diagnostics.Push(Error.NotAVariable(identifier.location, name));
                    return null;
            }
        }

        private BoundStatement BindVariableDeclarationStatement(VariableDeclarationStatement expression) {
            var isReadOnly = expression.typeName.type == SyntaxType.LET_KEYWORD;
            var type = BindTypeClause(expression.typeName);
            var initializer = BindExpression(expression.initializer);
            var variableType = type ?? initializer.lType;
            var variable = BindVariable(expression.identifier, isReadOnly, variableType, initializer.constantValue);
            var castedInitializer = BindCast(expression.initializer.location, initializer, variableType);

            return new BoundVariableDeclarationStatement(variable, castedInitializer);
        }

        private TypeSymbol BindTypeClause(Token type) {
            if (type.type == SyntaxType.AUTO_KEYWORD || type.type == SyntaxType.LET_KEYWORD)
                return null;

            var foundType = LookupType(type.text);
            if (foundType == null)
                diagnostics.Push(Error.UnknownType(type.location, type.text));

            return foundType;
        }

        private VariableSymbol BindVariable(
            Token identifier, bool isReadOnly, TypeSymbol type, BoundConstant constant = null) {
            var name = identifier.text ?? "?";
            var declare = !identifier.isMissing;
            var variable = function_ == null
                ? (VariableSymbol) new GlobalVariableSymbol(name, isReadOnly, type, constant)
                : new LocalVariableSymbol(name, isReadOnly, type, constant);

            if (declare && !scope_.TryDeclareVariable(variable))
                diagnostics.Push(Error.AlreadyDeclared(identifier.location, name));

            return variable;
        }

        private TypeSymbol LookupType(string name) {
            switch (name) {
                case "any": return TypeSymbol.Any;
                case "bool": return TypeSymbol.Bool;
                case "int": return TypeSymbol.Int;
                case "string": return TypeSymbol.String;
                case "void": return TypeSymbol.Void;
                default: return null;
            }
        }
    }
}
