using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Lowering;
using System;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class Binder {
    public DiagnosticQueue diagnostics;
    private BoundScope scope_;
    private readonly bool isScript_;
    private readonly FunctionSymbol function_;
    private Stack<(BoundLabel breakLabel, BoundLabel continueLabel)> loopStack_ =
        new Stack<(BoundLabel breakLabel, BoundLabel continueLabel)>();
    private readonly List<(FunctionSymbol function, BoundBlockStatement body)> functionBodies_ =
        new List<(FunctionSymbol function, BoundBlockStatement body)>();
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
            return new BoundGlobalScope(ImmutableArray<(FunctionSymbol function, BoundBlockStatement body)>.Empty,
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
                scriptFunction = new FunctionSymbol(
                "<Eval>$", ImmutableArray<ParameterSymbol>.Empty, BoundTypeClause.NullableAny);
            else
                scriptFunction = null;

            mainFunction = null;
        } else {
            scriptFunction = null;
            mainFunction = functions.FirstOrDefault(f => f.name == "Main" || f.name == "main");

            if (mainFunction != null)
                if ((mainFunction.typeClause.lType != TypeSymbol.Void &&
                    mainFunction.typeClause.lType != TypeSymbol.Int) ||
                    mainFunction.parameters.Any())
                    binder.diagnostics.Push(Error.InvalidMain(mainFunction.declaration.location));

            if (globalStatements.Any()) {
                if (mainFunction != null) {
                    binder.diagnostics.Push(Error.MainAndGlobals(mainFunction.declaration.identifier.location));

                    foreach (var globalStatement in firstGlobalPerTree)
                        binder.diagnostics.Push(Error.MainAndGlobals(globalStatement.location));
                } else {
                    mainFunction = new FunctionSymbol(
                        "Main", ImmutableArray<ParameterSymbol>.Empty, new BoundTypeClause(TypeSymbol.Void));
                }
            }
        }

        var variables = binder.scope_.GetDeclaredVariables();

        if (previous != null)
            binder.diagnostics.diagnostics_.InsertRange(0, previous.diagnostics.diagnostics_);

        var functionBodies = previous == null
            ? binder.functionBodies_.ToImmutableArray()
            : previous.functionBodies.AddRange(binder.functionBodies_);

        return new BoundGlobalScope(functionBodies, previous, binder.diagnostics, mainFunction,
            scriptFunction, functions, variables, statements.ToImmutable());
    }

    public static BoundProgram BindProgram(bool isScript, BoundProgram previous, BoundGlobalScope globalScope) {
        var parentScope = CreateParentScope(globalScope);

        if (globalScope.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return new BoundProgram(previous, globalScope.diagnostics,
                null, null, ImmutableDictionary<FunctionSymbol, BoundBlockStatement>.Empty);

        var functionBodies = ImmutableDictionary.CreateBuilder<FunctionSymbol, BoundBlockStatement>();
        var diagnostics = new DiagnosticQueue();
        diagnostics.Move(globalScope.diagnostics);

        foreach (var function in globalScope.functions) {
            var binder = new Binder(isScript, parentScope, function);
            var body = binder.BindStatement(function.declaration.body);
            var loweredBody = Lowerer.Lower(function, body, functionBodies);

            if (function.typeClause.lType != TypeSymbol.Void && !ControlFlowGraph.AllPathsReturn(loweredBody))
                binder.diagnostics.Push(Error.NotAllPathsReturn(function.declaration.identifier.location));

            functionBodies.Add(function, loweredBody);

            foreach (var functionBody in binder.functionBodies_)
                functionBodies.Add(functionBody.function, functionBody.body);

            diagnostics.Move(binder.diagnostics);
        }

        if (globalScope.mainFunction != null && globalScope.statements.Any()) {
            var body = Lowerer.Lower(
                globalScope.mainFunction, new BoundBlockStatement(globalScope.statements), functionBodies);

            functionBodies.Add(globalScope.mainFunction, body);
        } else if (globalScope.scriptFunction != null) {
            var statements = globalScope.statements;

            if (statements.Length == 1 &&
                statements[0] is BoundExpressionStatement es &&
                es.expression.typeClause?.lType != TypeSymbol.Void) {
                statements = statements.SetItem(0, new BoundReturnStatement(es.expression));
            } else if (statements.Any() && statements.Last().type != BoundNodeType.ReturnStatement) {
                var nullValue = new BoundLiteralExpression(null);
                statements = statements.Add(new BoundReturnStatement(nullValue));
            }

            var body = Lowerer.Lower(globalScope.scriptFunction, new BoundBlockStatement(statements), functionBodies);
            functionBodies.Add(globalScope.scriptFunction, body);
        }

        return new BoundProgram(previous, diagnostics, globalScope.mainFunction,
            globalScope.scriptFunction, functionBodies.ToImmutable());
    }

    private void BindFunctionDeclaration(FunctionDeclaration function) {
        var type = BindTypeClause(function.returnType, true);
        var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();
        var seenParametersNames = new HashSet<string>();

        foreach (var parameter in function.parameters) {
            var parameterName = parameter.identifier.text;
            var parameterType = BindTypeClause(parameter.typeClause);

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

    private BoundStatement BindStatement(Statement syntax, bool isGlobal = false, bool insideInline = false) {
        var result = BindStatementInternal(syntax, insideInline);

        if (!isScript_ || !isGlobal) {
            if (result is BoundExpressionStatement es) {
                var isAllowedExpression = es.expression.type == BoundNodeType.CallExpression ||
                    es.expression.type == BoundNodeType.AssignmentExpression ||
                    es.expression.type == BoundNodeType.ErrorExpression ||
                    es.expression.type == BoundNodeType.EmptyExpression ||
                    es.expression.type == BoundNodeType.CompoundAssignmentExpression;

                if (!isAllowedExpression)
                    diagnostics.Push(Error.InvalidExpressionStatement(syntax.location));
            }
        }

        return result;
    }

    private BoundStatement BindStatementInternal(Statement syntax, bool insideInline = false) {
        switch (syntax.type) {
            case SyntaxType.BLOCK:
                return BindBlockStatement((BlockStatement)syntax, insideInline);
            case SyntaxType.EXPRESSION_STATEMENT:
                return BindExpressionStatement((ExpressionStatement)syntax);
            case SyntaxType.VARIABLE_DECLARATION_STATEMENT:
                return BindVariableDeclarationStatement((VariableDeclarationStatement)syntax);
            case SyntaxType.IF_STATEMENT:
                return BindIfStatement((IfStatement)syntax, insideInline);
            case SyntaxType.WHILE_STATEMENT:
                return BindWhileStatement((WhileStatement)syntax, insideInline);
            case SyntaxType.FOR_STATEMENT:
                return BindForStatement((ForStatement)syntax, insideInline);
            case SyntaxType.DO_WHILE_STATEMENT:
                return BindDoWhileStatement((DoWhileStatement)syntax, insideInline);
            case SyntaxType.TRY_STATEMENT:
                return BindTryStatement((TryStatement)syntax, insideInline);
            case SyntaxType.BREAK_STATEMENT:
                return BindBreakStatement((BreakStatement)syntax);
            case SyntaxType.CONTINUE_STATEMENT:
                return BindContinueStatement((ContinueStatement)syntax);
            case SyntaxType.RETURN_STATEMENT:
                return BindReturnStatement((ReturnStatement)syntax, insideInline);
            case SyntaxType.LOCAL_FUNCTION_DECLARATION:
                return BindLocalFunctionDeclaration((LocalFunctionDeclaration)syntax);
            default:
                throw new Exception($"BindStatementInternal: unexpected syntax '{syntax.type}'");
        }
    }

    private BoundStatement BindLocalFunctionDeclaration(LocalFunctionDeclaration statement) {
        var result = (FunctionSymbol)scope_.LookupSymbol(statement.identifier.text);
        var binder = new Binder(false, scope_, result);
        // need to track all used symbols somehow, and then look them all up and add them to the parameter list
        var body = (BoundBlockStatement)binder.BindBlockStatement(result.declaration.body);

        var builder = ImmutableDictionary.CreateBuilder<FunctionSymbol, BoundBlockStatement>();
        var loweredBody = Lowerer.Lower(result, body, builder);

        foreach (var function in builder.ToImmutable())
            functionBodies_.Add((function.Key, function.Value));

        functionBodies_.Add((result, loweredBody));
        diagnostics.Move(binder.diagnostics);
        functionBodies_.AddRange(binder.functionBodies_);

        return new BoundBlockStatement(ImmutableArray<BoundStatement>.Empty);
    }

    private BoundStatement BindTryStatement(TryStatement expression, bool insideInline = false) {
        var body = (BoundBlockStatement)BindBlockStatement(expression.body, insideInline);
        var catchBody = expression.catchClause == null
            ? null
            : (BoundBlockStatement)BindBlockStatement(expression.catchClause.body, insideInline);
        var finallyBody = expression.finallyClause == null
            ? null
            : (BoundBlockStatement)BindBlockStatement(expression.finallyClause.body, insideInline);

        return new BoundTryStatement(body, catchBody, finallyBody);
    }

    private BoundStatement BindReturnStatement(ReturnStatement expression, bool insideInline = false) {
        var boundExpression = expression.expression == null ? null : BindExpression(expression.expression);

        if (!insideInline) {
            if (function_ == null) {
                if (isScript_) {
                    if (boundExpression == null)
                        boundExpression = new BoundLiteralExpression(null);
                } else if (boundExpression != null) {
                    diagnostics.Push(Error.Unsupported.GlobalReturnValue(expression.keyword.location));
                }
            } else {
                if (function_.typeClause.lType == TypeSymbol.Void) {
                    if (boundExpression != null)
                        diagnostics.Push(Error.UnexpectedReturnValue(expression.keyword.location));
                } else {
                    if (boundExpression == null)
                        diagnostics.Push(Error.MissingReturnValue(expression.keyword.location));
                    else
                        boundExpression = BindCast(
                            expression.expression.location, boundExpression, function_.typeClause);
                }
            }
        }

        return new BoundReturnStatement(boundExpression);
    }

    private BoundExpression BindExpression(Expression expression, bool canBeVoid=false, bool ownStatement = false) {
        var result = BindExpressionInternal(expression, ownStatement);

        if (!canBeVoid && result.typeClause.lType == TypeSymbol.Void) {
            diagnostics.Push(Error.NoValue(expression.location));
            return new BoundErrorExpression();
        }

        return result;
    }

    private BoundExpression BindExpressionInternal(Expression expression, bool ownStatement = false) {
        switch (expression.type) {
            case SyntaxType.LITERAL_EXPRESSION:
                if (expression is InitializerListExpression il)
                    return BindInitializerListExpression(il, null);
                else
                    return BindLiteralExpression((LiteralExpression)expression);
            case SyntaxType.UNARY_EXPRESSION:
                return BindUnaryExpression((UnaryExpression)expression);
            case SyntaxType.BINARY_EXPRESSION:
                return BindBinaryExpression((BinaryExpression)expression);
            case SyntaxType.PARENTHESIZED_EXPRESSION:
                return BindParenExpression((ParenthesisExpression)expression);
            case SyntaxType.NAME_EXPRESSION:
                return BindNameExpression((NameExpression)expression);
            case SyntaxType.ASSIGN_EXPRESSION:
                return BindAssignmentExpression((AssignmentExpression)expression);
            case SyntaxType.CALL_EXPRESSION:
                return BindCallExpression((CallExpression)expression);
            case SyntaxType.INDEX_EXPRESSION:
                return BindIndexExpression((IndexExpression)expression);
            case SyntaxType.EMPTY_EXPRESSION:
                return BindEmptyExpression((EmptyExpression)expression);
            case SyntaxType.POSTFIX_EXPRESSION:
                return BindPostfixExpression((PostfixExpression)expression, ownStatement);
            case SyntaxType.PREFIX_EXPRESSION:
                return BindPrefixExpression((PrefixExpression)expression);
            case SyntaxType.REFERENCE_EXPRESSION:
                return BindReferenceExpression((ReferenceExpression)expression);
            case SyntaxType.INLINE_FUNCTION:
                return BindInlineFunctionExpression((InlineFunctionExpression)expression);
            case SyntaxType.CAST_EXPRESSION:
                return BindCastExpression((CastExpression)expression);
            default:
                throw new Exception($"BindExpressionInternal: unexpected syntax '{expression.type}'");
        }
    }

    private BoundExpression BindReferenceExpression(ReferenceExpression expression) {
        var variable = BindVariableReference(expression.identifier);
        var typeClause = new BoundTypeClause(
            variable.typeClause.lType, variable.typeClause.isImplicit, false, true,
            variable.typeClause.isConstant, variable.typeClause.isNullable, false, variable.typeClause.dimensions);

        return new BoundReferenceExpression(variable, typeClause);
    }

    private BoundExpression BindPostfixExpression(PostfixExpression expression, bool ownStatement = false) {
        var name = expression.identifier.text;

        var variable = BindVariableReference(expression.identifier);
        if (variable == null)
            return new BoundErrorExpression();

        if (variable.typeClause.isConstant)
            diagnostics.Push(Error.ConstAssign(expression.op.location, name));

        var value = new BoundLiteralExpression(1);
        BoundBinaryOperator boundOperator = null;
        BoundBinaryOperator reversalOperator = null;

        if (expression.op.type == SyntaxType.PLUS_PLUS_TOKEN) {
            boundOperator = BoundBinaryOperator.Bind(
                SyntaxType.PLUS_TOKEN, variable.typeClause, value.typeClause);
            reversalOperator = BoundBinaryOperator.Bind(
                SyntaxType.MINUS_TOKEN, variable.typeClause, value.typeClause);
        } else if (expression.op.type == SyntaxType.MINUS_MINUS_TOKEN) {
            boundOperator = BoundBinaryOperator.Bind(
                SyntaxType.MINUS_TOKEN, variable.typeClause, value.typeClause);
            reversalOperator = BoundBinaryOperator.Bind(
                SyntaxType.PLUS_TOKEN, variable.typeClause, value.typeClause);
        }

        var assignmentExpression = new BoundCompoundAssignmentExpression(variable, boundOperator, value);

        if (ownStatement)
            return assignmentExpression;
        else
            return new BoundBinaryExpression(assignmentExpression, reversalOperator, value);
    }

    private BoundExpression BindPrefixExpression(PrefixExpression expression) {
        var name = expression.identifier.text;

        var variable = BindVariableReference(expression.identifier);
        if (variable == null)
            return new BoundErrorExpression();

        if (variable.typeClause.isConstant)
            diagnostics.Push(Error.ConstAssign(expression.op.location, name));

        var value = new BoundLiteralExpression(1);
        BoundBinaryOperator boundOperator = null;

        if (expression.op.type == SyntaxType.PLUS_PLUS_TOKEN)
            boundOperator = BoundBinaryOperator.Bind(
                SyntaxType.PLUS_TOKEN, variable.typeClause, value.typeClause);
        else if (expression.op.type == SyntaxType.MINUS_MINUS_TOKEN)
            boundOperator = BoundBinaryOperator.Bind(
                SyntaxType.MINUS_TOKEN, variable.typeClause, value.typeClause);

        return new BoundCompoundAssignmentExpression(variable, boundOperator, value);
    }

    private BoundExpression BindIndexExpression(IndexExpression expression) {
        var boundExpression = BindExpression(expression.operand);
        boundExpression.typeClause.isNullable = true;

        if (boundExpression.typeClause.dimensions > 0) {
            var index = BindCast(
                expression.index.location, BindExpression(expression.index), new BoundTypeClause(TypeSymbol.Int));
            return new BoundIndexExpression(boundExpression, index);
        } else {
            diagnostics.Push(Error.CannotApplyIndexing(expression.location, boundExpression.typeClause));
            return new BoundErrorExpression();
        }
    }

    private BoundExpression BindCallExpression(CallExpression expression) {
        var name = expression.identifier.identifier.text;

        var symbol = scope_.LookupSymbol(name);
        if (symbol == null) {
            diagnostics.Push(Error.UndefinedFunction(expression.identifier.location, name));
            return new BoundErrorExpression();
        }

        var function = symbol as FunctionSymbol;
        if (function == null) {
            diagnostics.Push(Error.CannotCallNonFunction(expression.identifier.location, name));
            return new BoundErrorExpression();
        }

        if (function == null) {
            diagnostics.Push(Error.UndefinedFunction(expression.identifier.location, name));
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
            var boundArgument = BindCast(argument.location, BindExpression(argument), parameter.typeClause);
            boundArguments.Add(boundArgument);
        }

        return new BoundCallExpression(function, boundArguments.ToImmutable());
    }

    private BoundExpression BindCast(Expression expression, BoundTypeClause type, bool allowExplicit = false) {
        var boundExpression = BindExpression(expression);
        return BindCast(expression.location, boundExpression, type, allowExplicit);
    }

    private BoundExpression BindCastExpression(CastExpression expression) {
        var toType = BindTypeClause(expression.typeClause);
        var boundExpression = BindExpression(expression.expression);

        return BindCast(expression.location, boundExpression, toType, true);
    }

    private BoundExpression BindCast(
        TextLocation diagnosticLocation, BoundExpression expression, BoundTypeClause type, bool allowExplicit = false) {
        var conversion = Cast.Classify(expression.typeClause, type);

        if (!conversion.exists) {
            if (expression.typeClause.lType != TypeSymbol.Error && type.lType != TypeSymbol.Error)
                diagnostics.Push(Error.CannotConvert(diagnosticLocation, expression.typeClause, type));

            return new BoundErrorExpression();
        }

        if (!allowExplicit && conversion.isExplicit)
            diagnostics.Push(Error.CannotConvertImplicitly(diagnosticLocation, expression.typeClause, type));

        if (conversion.isIdentity) {
            if (expression is not BoundLiteralExpression le || le.typeClause.lType != null)
                return expression;
        }

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

    private BoundStatement BindWhileStatement(WhileStatement statement, bool insideInline = false) {
        var condition = BindCast(statement.condition, new BoundTypeClause(TypeSymbol.Bool));

        if (condition.constantValue != null && !(bool)condition.constantValue.value)
            diagnostics.Push(Warning.UnreachableCode(statement.body));

        var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel, insideInline);
        return new BoundWhileStatement(condition, body, breakLabel, continueLabel);
    }

    private BoundStatement BindDoWhileStatement(DoWhileStatement statement, bool insideInline = false) {
        var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel, insideInline);
        var condition = BindCast(statement.condition, new BoundTypeClause(TypeSymbol.Bool));
        return new BoundDoWhileStatement(body, condition, breakLabel, continueLabel);
    }

    private BoundStatement BindForStatement(ForStatement statement, bool insideInline = false) {
        scope_ = new BoundScope(scope_);

        var initializer = BindStatement(statement.initializer, insideInline: insideInline);
        var condition = BindCast(statement.condition, new BoundTypeClause(TypeSymbol.Bool));
        var step = BindExpression(statement.step);
        var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel, insideInline);

        scope_ = scope_.parent;
        return new BoundForStatement(initializer, condition, step, body, breakLabel, continueLabel);
    }

    private BoundStatement BindLoopBody(
        Statement body, out BoundLabel breakLabel, out BoundLabel continueLabel, bool insideInline = false) {
        labelCount_++;
        breakLabel = new BoundLabel($"Break{labelCount_}");
        continueLabel = new BoundLabel($"Continue{labelCount_}");

        loopStack_.Push((breakLabel, continueLabel));
        var boundBody = BindStatement(body, insideInline: insideInline);
        loopStack_.Pop();

        return boundBody;
    }

    private BoundStatement BindIfStatement(IfStatement statement, bool insideInline = false) {
        var condition = BindCast(statement.condition, new BoundTypeClause(TypeSymbol.Bool));

        if (condition.constantValue != null) {
            if ((bool)condition.constantValue.value == false)
                diagnostics.Push(Warning.UnreachableCode(statement.then));
            else if (statement.elseClause != null)
                diagnostics.Push(Warning.UnreachableCode(statement.elseClause.body));
        }

        var then = BindStatement(statement.then, insideInline: insideInline);
        var elseStatement = statement.elseClause == null
            ? null
            : BindStatement(statement.elseClause.body, insideInline: insideInline);

        return new BoundIfStatement(condition, then, elseStatement);
    }

    private BoundStatement BindBlockStatement(BlockStatement statement, bool insideInline = false) {
        var statements = ImmutableArray.CreateBuilder<BoundStatement>();
        scope_ = new BoundScope(scope_);

        foreach (var statementSyntax in statement.statements) {
            if (statementSyntax is LocalFunctionDeclaration fd) {
                var declaration = new FunctionDeclaration(
                    fd.syntaxTree, fd.returnType, fd.identifier, fd.openParenthesis,
                    fd.parameters, fd.closeParenthesis, fd.body);

                BindFunctionDeclaration(declaration);
            }
        }

        foreach (var statementSyntax in statement.statements) {
            var state = BindStatement(statementSyntax, insideInline: insideInline);
            statements.Add(state);
        }

        scope_ = scope_.parent;

        return new BoundBlockStatement(statements.ToImmutable());
    }

    private BoundExpression BindInlineFunctionExpression(InlineFunctionExpression statement) {
        var statements = ImmutableArray.CreateBuilder<BoundStatement>();
        scope_ = new BoundScope(scope_);

        foreach (var statementSyntax in statement.statements) {
            var boundStatement = BindStatement(statementSyntax, insideInline: true);
            statements.Add(boundStatement);
        }

        scope_ = scope_.parent;

        var boundBlockStatement = new BoundBlockStatement(statements.ToImmutable());
        var returnType = new BoundTypeClause(TypeSymbol.Any);

        var tempFunction = new FunctionSymbol("$Inline", ImmutableArray<ParameterSymbol>.Empty, returnType);
        var tempBuilder = ImmutableDictionary.CreateBuilder<FunctionSymbol, BoundBlockStatement>();
        var loweredBlock = Lowerer.Lower(tempFunction, boundBlockStatement, tempBuilder);

        returnType = null;

        foreach (var loweredStatement in loweredBlock.statements) {
            if (loweredStatement.type == BoundNodeType.ReturnStatement) {
                if (returnType == null) {
                    returnType = ((BoundReturnStatement)loweredStatement).expression.typeClause;
                } else {
                    var typeClause = ((BoundReturnStatement)loweredStatement).expression.typeClause;

                    if (!BoundTypeClause.AboutEqual(returnType, typeClause)) {
                        diagnostics.Push(Error.InconsistentReturnTypes(statement.closeBrace.location));
                        break;
                    }
                }
            }
        }

        if (!ControlFlowGraph.AllPathsReturn(loweredBlock))
            diagnostics.Push(Error.NotAllPathsReturn(statement.closeBrace.location));

        return new BoundInlineFunctionExpression(boundBlockStatement, returnType);
    }

    private BoundStatement BindExpressionStatement(ExpressionStatement statement) {
        var expression = BindExpression(statement.expression, true, true);
        return new BoundExpressionStatement(expression);
    }

    private BoundExpression BindInitializerListExpression(InitializerListExpression expression, BoundTypeClause type) {
        var boundItems = ImmutableArray.CreateBuilder<BoundExpression>();

        foreach (var item in expression.items) {
            BoundExpression tempItem = BindExpression(item);
            tempItem.typeClause.isNullable = true;

            if (type == null) {
                var typeClause = tempItem.typeClause;

                type = new BoundTypeClause(
                    typeClause.lType, false, typeClause.isConstantReference, typeClause.isReference,
                    typeClause.isConstant, true, true, typeClause.dimensions + 1);
            }

            var childType = type.ChildType();
            var boundItem = BindCast(item.location, tempItem, childType);
            boundItems.Add(boundItem);
        }

        return new BoundInitializerListExpression(boundItems.ToImmutable(), type.dimensions, type.ChildType());
    }

    private BoundExpression BindLiteralExpression(LiteralExpression expression) {
        var value = expression.value;
        return new BoundLiteralExpression(value);
    }

    private BoundExpression BindUnaryExpression(UnaryExpression expression) {
        var boundOperand = BindExpression(expression.operand);

        if (boundOperand.typeClause.lType == TypeSymbol.Error)
            return new BoundErrorExpression();

        var boundOp = BoundUnaryOperator.Bind(expression.op.type, boundOperand.typeClause);

        if (boundOp == null) {
            diagnostics.Push(
                Error.InvalidUnaryOperatorUse(expression.op.location, expression.op.text, boundOperand.typeClause));
            return new BoundErrorExpression();
        }

        return new BoundUnaryExpression(boundOp, boundOperand);
    }

    private BoundExpression BindBinaryExpression(BinaryExpression expression) {
        // TODO: can optimize to see if non-nullable var is compared to null and create a warning
        var boundLeft = BindExpression(expression.left);
        var boundRight = BindExpression(expression.right);

        if (boundLeft.typeClause.lType == TypeSymbol.Error || boundRight.typeClause.lType == TypeSymbol.Error)
            return new BoundErrorExpression();

        var boundOp = BoundBinaryOperator.Bind(expression.op.type, boundLeft.typeClause, boundRight.typeClause);

        if (boundOp == null) {
            diagnostics.Push(Error.InvalidBinaryOperatorUse(
                    expression.op.location, expression.op.text, boundLeft.typeClause, boundRight.typeClause));
            return new BoundErrorExpression();
        }

        // could possible move this to ComputeConstant
        if (boundOp.opType == BoundBinaryOperatorType.LessThan ||
            boundOp.opType == BoundBinaryOperatorType.LessOrEqual ||
            boundOp.opType == BoundBinaryOperatorType.GreaterThan ||
            boundOp.opType == BoundBinaryOperatorType.GreatOrEqual ||
            boundOp.opType == BoundBinaryOperatorType.EqualityEquals ||
            boundOp.opType == BoundBinaryOperatorType.EqualityNotEquals) {
            if (boundLeft.constantValue != null && boundRight.constantValue != null &&
                (boundLeft.constantValue.value == null) != (boundRight.constantValue.value == null))
                diagnostics.Push(Warning.AlwaysValue(expression.location, false));
        }

        return new BoundBinaryExpression(boundLeft, boundOp, boundRight);
    }

    private BoundExpression BindParenExpression(ParenthesisExpression expression) {
        return BindExpression(expression.expression);
    }

    private BoundExpression BindNameExpression(NameExpression expression) {
        var name = expression.identifier.text;
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

        if (!variable.typeClause.isNullable && boundExpression is BoundLiteralExpression le && le.value == null) {
            diagnostics.Push(Error.NullAssignOnNotNull(expression.expression.location));
            return boundExpression;
        }

        if ((variable.typeClause.isReference && variable.typeClause.isConstantReference &&
            boundExpression.type == BoundNodeType.ReferenceExpression) ||
            (variable.typeClause.isConstant && boundExpression.type != BoundNodeType.ReferenceExpression))
            diagnostics.Push(Error.ConstAssign(expression.assignmentToken.location, name));

        if (expression.assignmentToken.type != SyntaxType.EQUALS_TOKEN) {
            var equivalentOperatorTokenType = SyntaxFacts.GetBinaryOperatorOfAssignmentOperator(
                expression.assignmentToken.type);
            var boundOperator = BoundBinaryOperator.Bind(
                equivalentOperatorTokenType, variable.typeClause, boundExpression.typeClause);

            if (boundOperator == null) {
                diagnostics.Push(Error.InvalidBinaryOperatorUse(
                    expression.assignmentToken.location, expression.assignmentToken.text,
                    variable.typeClause, boundExpression.typeClause));
                return new BoundErrorExpression();
            }

            var convertedExpression = BindCast(expression.expression.location, boundExpression, variable.typeClause);
            return new BoundCompoundAssignmentExpression(variable, boundOperator, convertedExpression);
        } else {
            var convertedExpression = BindCast(expression.expression.location, boundExpression, variable.typeClause);
            return new BoundAssignmentExpression(variable, convertedExpression);
        }
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
        var typeClause = BindTypeClause(expression.typeClause, true);

        if (typeClause.isImplicit && expression.initializer == null) {
            diagnostics.Push(Error.NoInitOnImplicit(expression.identifier.location));
            return null;
        }

        if (typeClause.isReference && expression.initializer == null) {
            diagnostics.Push(Error.ReferenceNoInitialization(expression.identifier.location));
            return null;
        }

        if (typeClause.isReference && expression.initializer?.type != SyntaxType.REFERENCE_EXPRESSION) {
            diagnostics.Push(Error.ReferenceWrongInitialization(expression.equals.location));
            return null;
        }

        if (expression.initializer is LiteralExpression le) {
            if (le.token.type == SyntaxType.NULL_KEYWORD && typeClause.isImplicit) {
                diagnostics.Push(Error.NullAssignOnImplicit(expression.initializer.location));
                return null;
            }
        }

        if (typeClause.lType == TypeSymbol.Void) {
            diagnostics.Push(Error.VoidVariable(expression.typeClause.typeName.location));
            return null;
        }

        if (!typeClause.isReference && expression.initializer?.type == SyntaxType.REFERENCE_EXPRESSION) {
            diagnostics.Push(Error.WrongInitializationReference(expression.equals.location));
            return null;
        }

        var nullable = typeClause.isNullable;

        if (expression.initializer?.type == SyntaxType.REFERENCE_EXPRESSION) {
            var initializer = BindReferenceExpression((ReferenceExpression)expression.initializer);
            // references cant have implicit casts
            var variable = BindVariable(expression.identifier, typeClause, initializer.constantValue);

            return new BoundVariableDeclarationStatement(variable, initializer);
        } else if (typeClause.dimensions > 0 ||
            (typeClause.isImplicit && expression.initializer is InitializerListExpression)) {
            var initializer = expression.initializer.type != SyntaxType.NULL_KEYWORD
                ? BindInitializerListExpression(
                    (InitializerListExpression)expression.initializer, typeClause)
                : new BoundLiteralExpression(null);

            if (initializer is BoundInitializerListExpression il) {
                if (il.items.Length == 0 && typeClause.isImplicit) {
                    diagnostics.Push(Error.EmptyInitializerListOnImplicit(expression.initializer.location));
                    return null;
                }
            }

            if (typeClause.isImplicit && typeClause.dimensions > 0) {
                diagnostics.Push(Error.ImpliedDimensions(expression.initializer.location));
                return null;
            }

            var variableType = typeClause.isImplicit
                ? initializer.typeClause
                : typeClause;

            variableType.isNullable = nullable;

            if (!variableType.isNullable && initializer is BoundLiteralExpression ble && ble.value == null) {
                diagnostics.Push(Error.NullAssignOnNotNull(expression.initializer.location));
                return null;
            }

            var itemType = variableType.BaseType();

            var castedInitializer = BindCast(expression.initializer?.location, initializer, variableType);
            var variable = BindVariable(expression.identifier,
                new BoundTypeClause(
                    itemType.lType, typeClause.isImplicit, typeClause.isConstantReference, typeClause.isReference,
                    typeClause.isConstant, typeClause.isNullable, false, variableType.dimensions),
                    castedInitializer.constantValue);

            return new BoundVariableDeclarationStatement(variable, castedInitializer);
        } else {
            var initializer = expression.initializer != null
                ? BindExpression(expression.initializer)
                : new BoundLiteralExpression(null);

            var variableType = typeClause.isImplicit
                ? initializer.typeClause
                : typeClause;

            variableType.isNullable = nullable;

            if (!variableType.isNullable && initializer is BoundLiteralExpression ble && ble.value == null) {
                diagnostics.Push(Error.NullAssignOnNotNull(expression.initializer.location));
                return null;
            }

            var castedInitializer = BindCast(expression.initializer?.location, initializer, variableType);
            var variable = BindVariable(expression.identifier, variableType, castedInitializer.constantValue);

            return new BoundVariableDeclarationStatement(variable, castedInitializer);
        }
    }

    private BoundTypeClause BindTypeClause(TypeClause type, bool isNullable = false) {
        foreach (var attribute in type.attributes) {
            if (attribute.identifier.text == "NotNull")
                isNullable = false;
            else
                diagnostics.Push(Error.UnknownAttribute(attribute.identifier.location, attribute.identifier.text));
        }

        var isConstRef = type.constRefKeyword != null;
        var isRef = type.refKeyword != null;
        var isConst = type.constKeyword != null;
        var isImplicit = type.typeName.type == SyntaxType.VAR_KEYWORD;
        var dimensions = type.brackets.Length;

        var foundType = LookupType(type.typeName.text);
        if (foundType == null && !isImplicit)
            diagnostics.Push(Error.UnknownType(type.location, type.typeName.text));

        return new BoundTypeClause(
            foundType, isImplicit, isConstRef, isRef, isConst, isNullable, false, dimensions);
    }

    private VariableSymbol BindVariable(
        Token identifier, BoundTypeClause type, BoundConstant constant = null) {
        var name = identifier.text ?? "?";
        var declare = !identifier.isMissing;
        var variable = function_ == null
            ? (VariableSymbol) new GlobalVariableSymbol(name, type, constant)
            : new LocalVariableSymbol(name, type, constant);

        if (declare && !scope_.TryDeclareVariable(variable))
            diagnostics.Push(Error.AlreadyDeclared(identifier.location, name));

        return variable;
    }

    private TypeSymbol LookupType(string name) {
        switch (name) {
            case "any":
                return TypeSymbol.Any;
            case "bool":
                return TypeSymbol.Bool;
            case "int":
                return TypeSymbol.Int;
            case "decimal":
                return TypeSymbol.Decimal;
            case "string":
                return TypeSymbol.String;
            case "void":
                return TypeSymbol.Void;
            default:
                return null;
        }
    }
}
