using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Lowering;
using System;
using Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Binds a <see cref="Parser" /> output into a immutable "bound" tree. This is where most error checking happens.
/// The <see cref="Lowerer" /> is also called here to simplify the code,
/// And convert control of flow into gotos and labels. Dead code is also removed here, as well as other optimizations.
/// </summary>
internal sealed class Binder {
    private readonly bool _isScript;
    private readonly FunctionSymbol _function;
    private readonly List<(FunctionSymbol function, BoundBlockStatement body)> _functionBodies =
        new List<(FunctionSymbol function, BoundBlockStatement body)>();
    private readonly List<(StructSymbol @struct, ImmutableList<FieldSymbol> members)> _structMembers =
        new List<(StructSymbol @struct, ImmutableList<FieldSymbol> members)>();
    private BoundScope _scope;
    private Stack<(BoundLabel breakLabel, BoundLabel continueLabel)> _loopStack =
        new Stack<(BoundLabel breakLabel, BoundLabel continueLabel)>();
    private int _labelCount;
    // * Temporary, inlines will be disabled until the StackFrameParser is added
    // private Stack<int> _inlineCounts = new Stack<int>();
    // private int _inlineCount;

    // Functions should be available correctly, so only track variables
    private Stack<HashSet<VariableSymbol>> _trackedSymbols = new Stack<HashSet<VariableSymbol>>();
    private Stack<HashSet<VariableSymbol>> _trackedDeclarations = new Stack<HashSet<VariableSymbol>>();
    private bool _trackSymbols = false;
    private Stack<string> _innerPrefix = new Stack<string>();
    private Stack<List<string>> _localLocals = new Stack<List<string>>();
    private List<string> _resolvedLocals = new List<string>();
    private Dictionary<string, LocalFunctionStatementSyntax> _unresolvedLocals =
        new Dictionary<string, LocalFunctionStatementSyntax>();

    private Binder(bool isScript, BoundScope parent, FunctionSymbol function) {
        _isScript = isScript;
        diagnostics = new BelteDiagnosticQueue();
        _scope = new BoundScope(parent);
        _function = function;

        if (function != null) {
            foreach (var parameter in function.parameters)
                _scope.TryDeclareVariable(parameter);
        }
    }

    /// <summary>
    /// Diagnostics produced by the <see cref="Binder" /> (and <see cref="Lowerer" />).
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// Binds everything in the global scope.
    /// </summary>
    /// <param name="isScript">
    /// If being bound as a script (used by the <see cref="BelteRepl" />), otherwise an application.
    /// </param>
    /// <param name="previous">Previous <see cref="BoundGlobalScope" /> (if applicable).</param>
    /// <param name="syntaxTrees">All SyntaxTrees, as files are bound together.</param>
    /// <returns>A new <see cref="BoundGlobalScope" />.</returns>
    internal static BoundGlobalScope BindGlobalScope(
        bool isScript, BoundGlobalScope previous, ImmutableArray<SyntaxTree> syntaxTrees) {
        var parentScope = CreateParentScope(previous);
        var binder = new Binder(isScript, parentScope, null);

        foreach (var syntaxTree in syntaxTrees)
            binder.diagnostics.Move(syntaxTree.diagnostics);

        if (binder.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return new BoundGlobalScope(ImmutableArray<(FunctionSymbol function, BoundBlockStatement body)>.Empty,
                ImmutableArray<(StructSymbol function, ImmutableList<FieldSymbol> members)>.Empty, previous,
                binder.diagnostics, null, null, ImmutableArray<FunctionSymbol>.Empty,
                ImmutableArray<VariableSymbol>.Empty, ImmutableArray<TypeSymbol>.Empty,
                ImmutableArray<BoundStatement>.Empty);

        var methodDeclarations = syntaxTrees.SelectMany(st => st.root.members).OfType<MethodDeclarationSyntax>();

        foreach (var method in methodDeclarations)
            binder.BindMethodDeclaration(method);

        var typeDeclarations = syntaxTrees.SelectMany(st => st.root.members).OfType<TypeDeclarationSyntax>();

        foreach (var @type in typeDeclarations)
            binder.BindTypeDeclaration(@type);

        var globalStatements = syntaxTrees.SelectMany(st => st.root.members).OfType<GlobalStatementSyntax>();

        var statements = ImmutableArray.CreateBuilder<BoundStatement>();
        foreach (var globalStatement in globalStatements)
            statements.Add(binder.BindStatement(globalStatement.statement, true));

        var firstGlobalPerTree = syntaxTrees
            .Select(st => st.root.members.OfType<GlobalStatementSyntax>().FirstOrDefault())
            .Where(g => g != null).ToArray();

        if (firstGlobalPerTree.Length > 1)
            foreach (var globalStatement in firstGlobalPerTree)
                binder.diagnostics.Push(Error.GlobalStatementsInMultipleFiles(globalStatement.location));

        var functions = binder._scope.GetDeclaredFunctions();

        FunctionSymbol mainFunction;
        FunctionSymbol scriptFunction;

        if (isScript) {
            if (globalStatements.Any())
                scriptFunction = new FunctionSymbol(
                "<Eval>$", ImmutableArray<ParameterSymbol>.Empty, BoundType.NullableAny);
            else
                scriptFunction = null;

            mainFunction = null;
        } else {
            scriptFunction = null;
            mainFunction = functions.FirstOrDefault(f => f.name.ToLower() == "main");

            if (mainFunction != null) {
                if (mainFunction.type.typeSymbol != TypeSymbol.Void &&
                    mainFunction.type.typeSymbol != TypeSymbol.Int)
                    binder.diagnostics.Push(Error.InvalidMain(mainFunction.declaration.returnType.location));

                if (mainFunction.parameters.Any()) {
                    var span = TextSpan.FromBounds(
                        mainFunction.declaration.openParenthesis.span.start + 1,
                        mainFunction.declaration.closeParenthesis.span.end - 1);

                    var location = new TextLocation(mainFunction.declaration.syntaxTree.text, span);
                    binder.diagnostics.Push(Error.InvalidMain(location));
                }
            }

            if (globalStatements.Any()) {
                if (mainFunction != null) {
                    binder.diagnostics.Push(Error.MainAndGlobals(mainFunction.declaration.identifier.location));

                    foreach (var globalStatement in firstGlobalPerTree)
                        binder.diagnostics.Push(Error.MainAndGlobals(globalStatement.location));
                } else {
                    mainFunction = new FunctionSymbol(
                        "<Main>$", ImmutableArray<ParameterSymbol>.Empty, new BoundType(TypeSymbol.Void));
                }
            }
        }

        var variables = binder._scope.GetDeclaredVariables();
        var types = binder._scope.GetDeclaredTypes();

        if (previous != null)
            binder.diagnostics.CopyToFront(previous.diagnostics);

        var functionBodies = previous == null
            ? binder._functionBodies.ToImmutableArray()
            : previous.functionBodies.AddRange(binder._functionBodies);

        var structMembers = previous == null
            ? binder._structMembers.ToImmutableArray()
            : previous.structMembers.AddRange(binder._structMembers);

        return new BoundGlobalScope(functionBodies, structMembers, previous, binder.diagnostics, mainFunction,
            scriptFunction, functions, variables, types, statements.ToImmutable());
    }

    /// <summary>
    /// Binds a program.
    /// </summary>
    /// <param name="isScript">If being bound as a script (used by the REPL), otherwise an application.</param>
    /// <param name="previous">Previous <see cref="BoundProgram" /> (if applicable).</param>
    /// <param name="globalScope">The already bound <see cref="BoundGlobalScope" />.</param>
    /// <returns>A new <see cref="BoundProgram" /> (then either emitted or evaluated).</returns>
    internal static BoundProgram BindProgram(bool isScript, BoundProgram previous, BoundGlobalScope globalScope) {
        var parentScope = CreateParentScope(globalScope);

        if (globalScope.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return new BoundProgram(previous, globalScope.diagnostics,
                null, null, ImmutableDictionary<FunctionSymbol, BoundBlockStatement>.Empty,
                ImmutableDictionary<StructSymbol, ImmutableList<FieldSymbol>>.Empty);

        var functionBodies = ImmutableDictionary.CreateBuilder<FunctionSymbol, BoundBlockStatement>();
        var structMembers = ImmutableDictionary.CreateBuilder<StructSymbol, ImmutableList<FieldSymbol>>();

        foreach (var @struct in globalScope.structMembers)
            structMembers.Add(@struct.@struct, @struct.members);

        var diagnostics = new BelteDiagnosticQueue();
        diagnostics.Move(globalScope.diagnostics);

        foreach (var function in globalScope.functions) {
            var binder = new Binder(isScript, parentScope, function);

            binder._innerPrefix = new Stack<string>();
            binder._innerPrefix.Push(function.name);

            BoundBlockStatement loweredBody = null;

            if (!function.name.Contains(">g__$Inline")) {
                var body = binder.BindStatement(function.declaration.body);
                diagnostics.Move(binder.diagnostics);

                if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
                    return new BoundProgram(previous, diagnostics, null, null,
                    ImmutableDictionary<FunctionSymbol, BoundBlockStatement>.Empty,
                    ImmutableDictionary<StructSymbol, ImmutableList<FieldSymbol>>.Empty);

                loweredBody = Lowerer.Lower(function, body);
            } else {
                // Inlines are bound when they are called for the first time in BindCallExpression
                // Using function.declaration.body uses a temporary old body
                var functionBody = globalScope.functionBodies.Where(t => t.function == function).Single();
                loweredBody = Lowerer.Lower(function, functionBody.body);
            }

            if (function.type.typeSymbol != TypeSymbol.Void && !ControlFlowGraph.AllPathsReturn(loweredBody))
                binder.diagnostics.Push(Error.NotAllPathsReturn(function.declaration.identifier.location));

            binder._functionBodies.Add((function, loweredBody));

            foreach (var functionBody in binder._functionBodies) {
                var newParameters = ImmutableArray.CreateBuilder<ParameterSymbol>();

                foreach (var parameter in functionBody.function.parameters) {
                    var name = parameter.name.StartsWith("$")
                        ? parameter.name.Substring(1)
                        : parameter.name;

                    var newParameter = new ParameterSymbol(name, parameter.type, parameter.ordinal);
                    newParameters.Add(newParameter);
                }

                var newFunction = new FunctionSymbol(
                    functionBody.function.name, newParameters.ToImmutable(), functionBody.function.type,
                    functionBody.function.declaration);

                functionBodies.Add(newFunction, functionBody.body);
            }

            diagnostics.Move(binder.diagnostics);
        }

        if (globalScope.mainFunction != null && globalScope.statements.Any()) {
            var body = Lowerer.Lower(globalScope.mainFunction, new BoundBlockStatement(globalScope.statements));
            functionBodies.Add(globalScope.mainFunction, body);
        } else if (globalScope.scriptFunction != null) {
            var statements = globalScope.statements;

            if (statements.Length == 1 &&
                statements[0] is BoundExpressionStatement es &&
                es.expression.type?.typeSymbol != TypeSymbol.Void) {
                statements = statements.SetItem(0, new BoundReturnStatement(es.expression));
            } else if (statements.Any() && statements.Last().kind != BoundNodeKind.ReturnStatement) {
                statements = statements.Add(new BoundReturnStatement(null));
            }

            var body = Lowerer.Lower(globalScope.scriptFunction, new BoundBlockStatement(statements));
            functionBodies.Add(globalScope.scriptFunction, body);
        }

        return new BoundProgram(previous, diagnostics, globalScope.mainFunction,
            globalScope.scriptFunction, functionBodies.ToImmutable(), structMembers.ToImmutable());
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

            foreach (var @type in previous.types)
                scope.TryDeclareType(@type);

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

    private void BindMethodDeclaration(MethodDeclarationSyntax method) {
        var type = BindTypeClause(method.returnType);
        var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();
        var seenParametersNames = new HashSet<string>();

        foreach (var parameter in method.parameters) {
            var parameterName = parameter.identifier.text;
            var parameterType = BindTypeClause(parameter.type);

            if (!seenParametersNames.Add(parameterName)) {
                diagnostics.Push(Error.ParameterAlreadyDeclared(parameter.location, parameter.identifier.text));
            } else {
                var boundParameter = new ParameterSymbol(parameterName, parameterType, parameters.Count);
                parameters.Add(boundParameter);
            }
        }

        var newMethod = new FunctionSymbol(method.identifier.text, parameters.ToImmutable(), type, method);
        if (newMethod.declaration.identifier.text != null && !_scope.TryDeclareFunction(newMethod))
            diagnostics.Push(Error.MethodAlreadyDeclared(method.identifier.location, newMethod.name));
    }

    private void BindTypeDeclaration(TypeDeclarationSyntax @type) {
        if (@type is StructDeclarationSyntax)
            BindStructDeclaration(@type as StructDeclarationSyntax);
    }

    private void BindStructDeclaration(StructDeclarationSyntax @struct) {
        var builder = ImmutableList.CreateBuilder<FieldSymbol>();
        var symbols = ImmutableArray.CreateBuilder<Symbol>();
        _scope = new BoundScope(_scope);

        foreach (var fieldDeclaration in @struct.members.OfType<FieldDeclarationSyntax>()) {
            var field = BindFieldDeclaration(fieldDeclaration);
            builder.Add(field);
            symbols.Add(field);
        }

        _scope = _scope.parent;
        var newStruct = new StructSymbol(@struct.identifier.text, symbols.ToImmutable(), @struct);
        _structMembers.Add((newStruct, builder.ToImmutable()));

        if (!_scope.TryDeclareType(newStruct))
            throw new BelteInternalException($"BindStructDeclaration: failed to declare {newStruct.name}");
    }

    private FieldSymbol BindFieldDeclaration(FieldDeclarationSyntax fieldDeclaration) {
        var type = BindTypeClause(fieldDeclaration.declaration.type);

        return BindVariable(
            fieldDeclaration.declaration.identifier, type, bindAsField: true) as FieldSymbol;
    }

    private BoundStatement BindStatement(StatementSyntax syntax, bool isGlobal = false) {
        var result = BindStatementInternal(syntax);

        if (!_isScript || !isGlobal) {
            if (result is BoundExpressionStatement es) {
                var isAllowedExpression = es.expression.kind == BoundNodeKind.CallExpression ||
                    es.expression.kind == BoundNodeKind.AssignmentExpression ||
                    es.expression.kind == BoundNodeKind.ErrorExpression ||
                    es.expression.kind == BoundNodeKind.EmptyExpression ||
                    es.expression.kind == BoundNodeKind.CompoundAssignmentExpression;

                if (!isAllowedExpression)
                    diagnostics.Push(Error.InvalidExpressionStatement(syntax.location));
            }
        }

        return result;
    }

    private BoundStatement BindStatementInternal(StatementSyntax syntax) {
        switch (syntax.kind) {
            case SyntaxKind.Block:
                return BindBlockStatement((BlockStatementSyntax)syntax);
            case SyntaxKind.ExpressionStatement:
                return BindExpressionStatement((ExpressionStatementSyntax)syntax);
            case SyntaxKind.VariableDeclarationStatement:
                return BindVariableDeclarationStatement((VariableDeclarationStatementSyntax)syntax);
            case SyntaxKind.IfStatement:
                return BindIfStatement((IfStatementSyntax)syntax);
            case SyntaxKind.WhileStatement:
                return BindWhileStatement((WhileStatementSyntax)syntax);
            case SyntaxKind.ForStatement:
                return BindForStatement((ForStatementSyntax)syntax);
            case SyntaxKind.DoWhileStatement:
                return BindDoWhileStatement((DoWhileStatementSyntax)syntax);
            case SyntaxKind.TryStatement:
                return BindTryStatement((TryStatementSyntax)syntax);
            case SyntaxKind.BreakStatement:
                return BindBreakStatement((BreakStatementSyntax)syntax);
            case SyntaxKind.ContinueStatement:
                return BindContinueStatement((ContinueStatementSyntax)syntax);
            case SyntaxKind.ReturnStatement:
                return BindReturnStatement((ReturnStatementSyntax)syntax);
            case SyntaxKind.LocalFunctionStatement:
                return new BoundBlockStatement(ImmutableArray<BoundStatement>.Empty);
            default:
                throw new BelteInternalException($"BindStatementInternal: unexpected syntax '{syntax.kind}'");
        }
    }

    private BoundStatement BindLocalFunctionDeclaration(LocalFunctionStatementSyntax statement) {
        var functionSymbol = (FunctionSymbol)_scope.LookupSymbol(statement.identifier.text);
        var binder = new Binder(_isScript, _scope, functionSymbol);
        binder._innerPrefix = new Stack<string>(_innerPrefix.Reverse());
        var oldTrackSymbols = _trackSymbols;
        binder._trackSymbols = true;
        binder._trackedSymbols = _trackedSymbols;
        binder._trackedDeclarations = _trackedDeclarations;
        binder._trackedSymbols.Push(new HashSet<VariableSymbol>());
        binder._trackedDeclarations.Push(new HashSet<VariableSymbol>());
        _innerPrefix.Push(functionSymbol.name);
        binder._innerPrefix.Push(functionSymbol.name);
        var body = (BoundBlockStatement)binder.BindBlockStatement(functionSymbol.declaration.body);
        _trackSymbols = oldTrackSymbols;

        var innerName = ConstructInnerName();
        _innerPrefix.Pop();

        var usedVariables = binder._trackedSymbols.Pop();
        var declaredVariables = binder._trackedDeclarations.Pop();
        var ordinal = functionSymbol.parameters.Count();
        var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();

        foreach (var parameter in functionSymbol.parameters)
            parameters.Add(parameter);

        foreach (var variable in usedVariables) {
            if (declaredVariables.Contains(variable) || parameters.Contains(variable))
                continue;

            var parameter = new ParameterSymbol($"${variable.name}", variable.type, ordinal++);
            parameters.Add(parameter);
        }

        var newFunctionSymbol = new FunctionSymbol(
            innerName, parameters.ToImmutable(), functionSymbol.type, functionSymbol.declaration);

        var loweredBody = Lowerer.Lower(newFunctionSymbol, body);

        if (newFunctionSymbol.type.typeSymbol != TypeSymbol.Void && !ControlFlowGraph.AllPathsReturn(loweredBody))
            diagnostics.Push(Error.NotAllPathsReturn(newFunctionSymbol.declaration.identifier.location));

        _functionBodies.Add((newFunctionSymbol, loweredBody));
        diagnostics.Move(binder.diagnostics);
        _functionBodies.AddRange(binder._functionBodies);

        if (!_scope.TryModifySymbol(functionSymbol.name, newFunctionSymbol))
            throw new BelteInternalException($"BindLocalFunction: failed to set function '{functionSymbol.name}'");

        return new BoundBlockStatement(ImmutableArray<BoundStatement>.Empty);
    }

    private BoundStatement BindTryStatement(TryStatementSyntax expression) {
        var body = (BoundBlockStatement)BindBlockStatement(expression.body);
        var catchBody = expression.catchClause == null
            ? null
            : (BoundBlockStatement)BindBlockStatement(expression.catchClause.body);
        var finallyBody = expression.finallyClause == null
            ? null
            : (BoundBlockStatement)BindBlockStatement(expression.finallyClause.body);

        return new BoundTryStatement(body, catchBody, finallyBody);
    }

    private BoundStatement BindReturnStatement(ReturnStatementSyntax expression) {
        var boundExpression = expression.expression == null ? null : BindExpression(expression.expression);

        if (_function == null) {
            if (_isScript) {
                if (boundExpression == null)
                    boundExpression = new BoundLiteralExpression(null);
            } else if (boundExpression != null) {
                diagnostics.Push(Error.Unsupported.GlobalReturnValue(expression.keyword.location));
            }
        } else {
            if (_function.type.typeSymbol == TypeSymbol.Void) {
                if (boundExpression != null)
                    diagnostics.Push(Error.UnexpectedReturnValue(expression.keyword.location));
            } else {
                if (boundExpression == null)
                    diagnostics.Push(Error.MissingReturnValue(expression.keyword.location));
                else
                    boundExpression = BindCast(
                        expression.expression.location, boundExpression, _function.type);
            }
        }

        return new BoundReturnStatement(boundExpression);
    }

    private BoundExpression BindExpression(
        ExpressionSyntax expression, bool canBeVoid = false, bool ownStatement = false) {
        var result = BindExpressionInternal(expression, ownStatement);

        if (!canBeVoid && result.type.typeSymbol == TypeSymbol.Void) {
            diagnostics.Push(Error.NoValue(expression.location));
            return new BoundErrorExpression();
        }

        return result;
    }

    private BoundExpression BindExpressionInternal(ExpressionSyntax expression, bool ownStatement = false) {
        switch (expression.kind) {
            case SyntaxKind.LiteralExpression:
                if (expression is InitializerListExpressionSyntax il)
                    return BindInitializerListExpression(il, null);
                else
                    return BindLiteralExpression((LiteralExpressionSyntax)expression);
            case SyntaxKind.UnaryExpression:
                return BindUnaryExpression((UnaryExpressionSyntax)expression);
            case SyntaxKind.BinaryExpression:
                return BindBinaryExpression((BinaryExpressionSyntax)expression);
            case SyntaxKind.TernaryExpression:
                return BindTernaryExpression((TernaryExpressionSyntax)expression);
            case SyntaxKind.ParenthesizedExpression:
                return BindParenExpression((ParenthesisExpressionSyntax)expression);
            case SyntaxKind.NameExpression:
                return BindNameExpression((NameExpressionSyntax)expression);
            case SyntaxKind.AssignExpression:
                return BindAssignmentExpression((AssignmentExpressionSyntax)expression);
            case SyntaxKind.CallExpression:
                return BindCallExpression((CallExpressionSyntax)expression);
            case SyntaxKind.IndexExpression:
                return BindIndexExpression((IndexExpressionSyntax)expression);
            case SyntaxKind.EmptyExpression:
                return BindEmptyExpression((EmptyExpressionSyntax)expression);
            case SyntaxKind.PostfixExpression:
                return BindPostfixExpression((PostfixExpressionSyntax)expression, ownStatement);
            case SyntaxKind.PrefixExpression:
                return BindPrefixExpression((PrefixExpressionSyntax)expression);
            case SyntaxKind.RefExpression:
                return BindReferenceExpression((ReferenceExpressionSyntax)expression);
            case SyntaxKind.InlineFunction:
                // * Temporary, inlines will be disabled until the StackFrameParser is added
                // return BindInlineFunctionExpression((InlineFunctionExpression)expression);
                goto default;
            case SyntaxKind.CastExpression:
                return BindCastExpression((CastExpressionSyntax)expression);
            case SyntaxKind.TypeOfExpression:
                return BindTypeOfExpression((TypeOfExpressionSyntax)expression);
            case SyntaxKind.MemberAccessExpression:
                return BindMemberAccessExpression((MemberAccessExpressionSyntax)expression);
            default:
                throw new BelteInternalException($"BindExpressionInternal: unexpected syntax '{expression.kind}'");
        }
    }

    private BoundExpression BindMemberAccessExpression(MemberAccessExpressionSyntax expression) {
        var operand = BindExpression(expression.operand);

        if (operand is BoundErrorExpression)
            return operand;

        if (!(operand.type.typeSymbol is StructSymbol)) {
            diagnostics.Push(
                Error.NoSuchMember(expression.identifier.location, operand.type, expression.identifier.text));
            return new BoundErrorExpression();
        }

        var @struct = operand?.type?.typeSymbol as StructSymbol;

        FieldSymbol symbol = null;
        foreach (var field in @struct.symbols.Where(f => f is FieldSymbol))
            if (field.name == expression.identifier.text)
                symbol = field as FieldSymbol;

        if (symbol == null) {
            diagnostics.Push(
                Error.NoSuchMember(expression.identifier.location, operand.type, expression.identifier.text));
            return new BoundErrorExpression();
        }

        if (operand.type.isNullable)
            diagnostics.Push(Warning.NullDeference(expression.period.location));

        return new BoundMemberAccessExpression(operand, symbol);
    }

    private BoundExpression BindTypeOfExpression(TypeOfExpressionSyntax expression) {
        var type = BindTypeClause(expression.type);

        return new BoundTypeOfExpression(type);
    }

    private BoundExpression BindReferenceExpression(ReferenceExpressionSyntax expression) {
        var variable = BindVariableReference(expression.identifier);
        var type = BoundType.Reference(variable.type);

        return new BoundReferenceExpression(variable, type);
    }

    private BoundExpression BindPostfixExpression(PostfixExpressionSyntax expression, bool ownStatement = false) {
        var operand = BindExpression(expression.operand);

        if (!(operand is BoundVariableExpression || operand is BoundMemberAccessExpression)) {
            diagnostics.Push(Error.CannotAssign(expression.operand.location));
            return new BoundErrorExpression();
        }

        VariableSymbol variable = null;

        if (operand is BoundVariableExpression v)
            variable = v.variable;
        else if (operand is BoundMemberAccessExpression m)
            variable = m.member;

        if (variable == null)
            return new BoundErrorExpression();

        if (variable.type.isConstant)
            diagnostics.Push(Error.ConstantAssignment(expression.op.location, variable.name));

        var value = new BoundLiteralExpression(1);
        BoundBinaryOperator boundOperator = null;
        BoundBinaryOperator reversalOperator = null;

        if (expression.op.kind == SyntaxKind.PlusPlusToken) {
            boundOperator = BoundBinaryOperator.Bind(
                SyntaxKind.PlusToken, variable.type, value.type);
            reversalOperator = BoundBinaryOperator.Bind(
                SyntaxKind.MinusToken, variable.type, value.type);
        } else if (expression.op.kind == SyntaxKind.MinusMinusToken) {
            boundOperator = BoundBinaryOperator.Bind(
                SyntaxKind.MinusToken, variable.type, value.type);
            reversalOperator = BoundBinaryOperator.Bind(
                SyntaxKind.PlusToken, variable.type, value.type);
        }

        var assignmentExpression = new BoundCompoundAssignmentExpression(operand, boundOperator, value);

        if (ownStatement)
            return assignmentExpression;
        else
            return new BoundBinaryExpression(assignmentExpression, reversalOperator, value);
    }

    private BoundExpression BindPrefixExpression(PrefixExpressionSyntax expression) {
        var operand = BindExpression(expression.operand);

        if (!(operand is BoundVariableExpression || operand is BoundMemberAccessExpression)) {
            diagnostics.Push(Error.CannotAssign(expression.operand.location));
            return new BoundErrorExpression();
        }

        VariableSymbol variable = null;

        if (operand is BoundVariableExpression v)
            variable = v.variable;
        else if (operand is BoundMemberAccessExpression m)
            variable = m.member;

        if (variable == null)
            return new BoundErrorExpression();

        if (variable.type.isConstant)
            diagnostics.Push(Error.ConstantAssignment(expression.op.location, variable.name));

        var value = new BoundLiteralExpression(1);
        BoundBinaryOperator boundOperator = null;

        if (expression.op.kind == SyntaxKind.PlusPlusToken)
            boundOperator = BoundBinaryOperator.Bind(
                SyntaxKind.PlusToken, variable.type, value.type);
        else if (expression.op.kind == SyntaxKind.MinusMinusToken)
            boundOperator = BoundBinaryOperator.Bind(
                SyntaxKind.MinusToken, variable.type, value.type);

        return new BoundCompoundAssignmentExpression(operand, boundOperator, value);
    }

    private BoundExpression BindIndexExpression(IndexExpressionSyntax expression) {
        var boundExpression = BindExpression(expression.operand);
        boundExpression.type.isNullable = true;

        if (boundExpression.type.dimensions > 0) {
            var index = BindCast(
                expression.index.location, BindExpression(expression.index), new BoundType(TypeSymbol.Int));
            return new BoundIndexExpression(boundExpression, index);
        } else {
            diagnostics.Push(Error.CannotApplyIndexing(expression.location, boundExpression.type));
            return new BoundErrorExpression();
        }
    }

    private BoundExpression BindCallExpression(CallExpressionSyntax expression) {
        var name = expression.identifier.identifier.text;

        var typeSymbol = _scope.LookupSymbol<TypeSymbol>(name);

        if (typeSymbol != null)
            return new BoundConstructorExpression(typeSymbol);

        var boundArguments = ImmutableArray.CreateBuilder<BoundExpression>();
        FunctionSymbol finalFunction = null;

        _innerPrefix.Push(name);
        var innerName = ConstructInnerName();
        _innerPrefix.Pop();

        var symbols = _scope.LookupOverloads(name, innerName);

        if (symbols == null || symbols.Length == 0) {
            diagnostics.Push(Error.UndefinedFunction(expression.identifier.location, name));
            return new BoundErrorExpression();
        }

        var tempDiagnostics = new BelteDiagnosticQueue();
        tempDiagnostics.Move(diagnostics);

        var preBoundArgumentsBuilder = ImmutableArray.CreateBuilder<BoundExpression>();

        for (int i=0; i<expression.arguments.count; i++) {
            var boundArgument = BindExpression(expression.arguments[i]);
            preBoundArgumentsBuilder.Add(boundArgument);
        }

        var preBoundArguments = preBoundArgumentsBuilder.ToImmutable();
        var minScore = Int32.MaxValue;
        var possibleOverloads = 0;

        foreach (var symbol in symbols) {
            var beforeCount = diagnostics.count;
            var score = 0;
            var actualSymbol = symbol;
            var isInner = symbol.name.Contains(">g__");

            if (_unresolvedLocals.ContainsKey(innerName) && !_resolvedLocals.Contains(innerName)) {
                BindLocalFunctionDeclaration(_unresolvedLocals[innerName]);
                _resolvedLocals.Add(innerName);
                actualSymbol = _scope.LookupSymbol(innerName) as FunctionSymbol;
                isInner = true;
            }

            var function = actualSymbol as FunctionSymbol;

            if (function == null) {
                diagnostics.Push(Error.CannotCallNonFunction(expression.identifier.location, name));
                return new BoundErrorExpression();
            }

            if (expression.arguments.count != function.parameters.Length) {
                var count = 0;

                if (isInner) {
                    foreach (var parameter in function.parameters)
                        if (parameter.name.StartsWith("$"))
                            count++;
                }

                if (!isInner || expression.arguments.count + count != function.parameters.Length) {
                    TextSpan span;

                    if (expression.arguments.count > function.parameters.Length) {
                        SyntaxNode firstExceedingNode;

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
                    diagnostics.Push(Error.IncorrectArgumentCount(
                        location, function.name, function.parameters.Length, expression.arguments.count));
                    continue;
                }
            }

            var currentBoundArguments = ImmutableArray.CreateBuilder<BoundExpression>();

            for (int i=0; i<preBoundArguments.Length; i++) {
                var argument = preBoundArguments[i];
                var parameter = function.parameters[i];
                var boundArgument =
                    BindCast(expression.arguments[i].location, argument, parameter.type, out var castType);

                if (castType.isImplicit && !castType.isIdentity)
                    score++;

                currentBoundArguments.Add(boundArgument);
            }

            if (isInner) {
                // No need to worry about currentBoundArguments because generated inlines never have overloads
                if (symbols.Length != 1)
                    throw new BelteInternalException("BindCallExpression: overloaded inline");

                for (int i=expression.arguments.count; i<function.parameters.Length; i++) {
                    var parameter = function.parameters[i];

                    var oldTrackSymbols = _trackSymbols;
                    _trackSymbols = false;

                    var argument = new NameExpressionSyntax(null, new SyntaxToken(
                        null, SyntaxKind.IdentifierToken, -1, parameter.name.Substring(1), null,
                        ImmutableArray<SyntaxTrivia>.Empty, ImmutableArray<SyntaxTrivia>.Empty)
                    );

                    _trackSymbols = oldTrackSymbols;
                    var boundArgument = BindCast(null, BindExpression(argument), parameter.type);
                    boundArguments.Add(boundArgument);
                }
            }

            if (symbols.Length == 1 && diagnostics.FilterOut(DiagnosticType.Warning).Any()) {
                tempDiagnostics.Move(diagnostics);
                diagnostics.Move(tempDiagnostics);
                return new BoundErrorExpression();
            }

            if (diagnostics.count == beforeCount) {
                if (score < minScore) {
                    boundArguments.Clear();
                    boundArguments.AddRange(currentBoundArguments);
                    minScore = score;
                    possibleOverloads = 0;
                }

                if (score == minScore) {
                    possibleOverloads++;
                    finalFunction = function;
                }
            }
        }

        if (symbols.Length > 1) {
            diagnostics.Clear();
            diagnostics.Move(tempDiagnostics);
        } else if (symbols.Length == 1) {
            tempDiagnostics.Move(diagnostics);
            diagnostics.Move(tempDiagnostics);
        }

        if (symbols.Length > 1 && possibleOverloads == 0) {
            diagnostics.Push(Error.NoOverload(expression.identifier.location, name));
            return new BoundErrorExpression();
        } else if (symbols.Length > 1 && possibleOverloads > 1) {
            diagnostics.Push(Error.AmbiguousOverload(expression.identifier.location, name));
            return new BoundErrorExpression();
        }

        return new BoundCallExpression(finalFunction, boundArguments.ToImmutable());
    }

    private BoundExpression BindCast(ExpressionSyntax expression, BoundType type, bool allowExplicit = false) {
        var boundExpression = BindExpression(expression);
        return BindCast(expression.location, boundExpression, type, allowExplicit);
    }

    private BoundExpression BindCast(
        ExpressionSyntax expression, BoundType type, out Cast castType, bool allowExplicit = false) {
        var boundExpression = BindExpression(expression);
        return BindCast(expression.location, boundExpression, type, out castType, allowExplicit);
    }

    private BoundExpression BindCastExpression(CastExpressionSyntax expression) {
        var toType = BindTypeClause(expression.type);
        var boundExpression = BindExpression(expression.expression);

        return BindCast(expression.location, boundExpression, toType, true);
    }

    private BoundExpression BindCast(
        TextLocation diagnosticLocation, BoundExpression expression, BoundType type, bool allowExplicit = false) {
        return BindCast(diagnosticLocation, expression, type, out _, allowExplicit);
    }

    private BoundExpression BindCast(
        TextLocation diagnosticLocation, BoundExpression expression,
        BoundType type, out Cast castType, bool allowExplicit = false) {
        var conversion = Cast.Classify(expression.type, type);
        castType = conversion;

        if (!conversion.exists) {
            if (expression.type.typeSymbol != TypeSymbol.Error && type.typeSymbol != TypeSymbol.Error)
                diagnostics.Push(Error.CannotConvert(diagnosticLocation, expression.type, type));

            return new BoundErrorExpression();
        }

        if (!allowExplicit && conversion.isExplicit)
            diagnostics.Push(Error.CannotConvertImplicitly(diagnosticLocation, expression.type, type));

        if (conversion.isIdentity) {
            if (expression is not BoundLiteralExpression le || le.type.typeSymbol != null)
                return expression;
        }

        return new BoundCastExpression(type, expression);
    }

    private BoundStatement BindErrorStatement() {
        return new BoundExpressionStatement(new BoundErrorExpression());
    }

    private BoundStatement BindContinueStatement(ContinueStatementSyntax syntax) {
        if (_loopStack.Count == 0) {
            diagnostics.Push(Error.InvalidBreakOrContinue(syntax.keyword.location, syntax.keyword.text));
            return BindErrorStatement();
        }

        var continueLabel = _loopStack.Peek().continueLabel;
        return new BoundGotoStatement(continueLabel);
    }

    private BoundStatement BindBreakStatement(BreakStatementSyntax syntax) {
        if (_loopStack.Count == 0) {
            diagnostics.Push(Error.InvalidBreakOrContinue(syntax.keyword.location, syntax.keyword.text));
            return BindErrorStatement();
        }

        var breakLabel = _loopStack.Peek().breakLabel;
        return new BoundGotoStatement(breakLabel);
    }

    private BoundStatement BindWhileStatement(WhileStatementSyntax statement) {
        var condition = BindCast(statement.condition, BoundType.NullableBool);

        if (condition.constantValue != null && !(bool)condition.constantValue.value)
            diagnostics.Push(Warning.UnreachableCode(statement.body));

        var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel);
        return new BoundWhileStatement(condition, body, breakLabel, continueLabel);
    }

    private BoundStatement BindDoWhileStatement(DoWhileStatementSyntax statement) {
        var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel);
        var condition = BindCast(statement.condition, BoundType.NullableBool);

        return new BoundDoWhileStatement(body, condition, breakLabel, continueLabel);
    }

    private BoundStatement BindForStatement(ForStatementSyntax statement) {
        _scope = new BoundScope(_scope);

        var initializer = BindStatement(statement.initializer);
        var condition = BindCast(statement.condition, BoundType.NullableBool);
        var step = BindExpression(statement.step);
        var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel);

        _scope.parent.CopyInlines(_scope);
        _scope = _scope.parent;
        return new BoundForStatement(initializer, condition, step, body, breakLabel, continueLabel);
    }

    private BoundStatement BindLoopBody(
        StatementSyntax body, out BoundLabel breakLabel, out BoundLabel continueLabel) {
        _labelCount++;
        breakLabel = new BoundLabel($"Break{_labelCount}");
        continueLabel = new BoundLabel($"Continue{_labelCount}");

        _loopStack.Push((breakLabel, continueLabel));
        var boundBody = BindStatement(body);
        _loopStack.Pop();

        return boundBody;
    }

    private BoundStatement BindIfStatement(IfStatementSyntax statement) {
        var condition = BindCast(statement.condition, BoundType.NullableBool);

        BoundLiteralExpression constant = null;

        if (condition.constantValue != null) {
            if (!(bool)condition.constantValue.value)
                diagnostics.Push(Warning.UnreachableCode(statement.then));
            else if (statement.elseClause != null)
                diagnostics.Push(Warning.UnreachableCode(statement.elseClause.body));

            constant = new BoundLiteralExpression(condition.constantValue.value);
        }

        var then = BindStatement(statement.then);
        var elseStatement = statement.elseClause == null
            ? null
            : BindStatement(statement.elseClause.body);

        if (constant != null)
            return new BoundIfStatement(constant, then, elseStatement);

        return new BoundIfStatement(condition, then, elseStatement);
    }

    private BoundStatement BindBlockStatement(BlockStatementSyntax statement) {
        var statements = ImmutableArray.CreateBuilder<BoundStatement>();
        _scope = new BoundScope(_scope, true);

        var frame = new List<string>();

        if (_localLocals.Count() > 0) {
            var lastFrame = _localLocals.Pop();
            frame.AddRange(lastFrame);
            _localLocals.Push(lastFrame);
        }

        foreach (var statementSyntax in statement.statements) {
            if (statementSyntax is LocalFunctionStatementSyntax fd) {
                var declaration = new MethodDeclarationSyntax(
                    fd.syntaxTree, fd.returnType, fd.identifier, fd.openParenthesis,
                    fd.parameters, fd.closeParenthesis, fd.body);

                BindMethodDeclaration(declaration);
                frame.Add(fd.identifier.text);
                _innerPrefix.Push(fd.identifier.text);

                if (!_unresolvedLocals.TryAdd(ConstructInnerName(), fd))
                    diagnostics.Push(Error.CannotOverloadNested(
                        declaration.identifier.location, declaration.identifier.text));

                _innerPrefix.Pop();
            }
        }

        _localLocals.Push(frame);

        foreach (var statementSyntax in statement.statements) {
            var state = BindStatement(statementSyntax);
            statements.Add(state);
        }

        _localLocals.Pop();
        _scope = _scope.parent;

        return new BoundBlockStatement(statements.ToImmutable());
    }

    private string ConstructInnerName() {
        var name = "<";

        for (int i=_innerPrefix.Count-1; i>0; i--) {
            name += _innerPrefix.ToArray()[i];

            if (i > 1)
                name += "::";
        }

        name += $">g__{_innerPrefix.Peek()}";
        return name;
    }

    /*
    * Temporary, inlines will be disabled until the StackFrameParser is added
    private BoundExpression BindInlineFunctionExpression(InlineFunctionExpression statement) {
        // Want to bind to resolve return type, then through away the binding result and bind later
        var statements = ImmutableArray.CreateBuilder<BoundStatement>();
        var block = new BlockStatement(null, null, statement.statements, null);

        var binderSaveState = StartEmulation();

        var returnType = new BoundType(TypeSymbol.Any);
        var tempFunction = new FunctionSymbol("$temp", ImmutableArray<ParameterSymbol>.Empty, returnType);
        var binder = new Binder(_isScript, _scope, tempFunction);
        binder._innerPrefix = new Stack<string>(_innerPrefix.Reverse());
        binder._innerPrefix.Push(tempFunction.name);

        foreach (var statementSyntax in statement.statements) {
            var boundStatement = binder.BindStatement(statementSyntax, insideInline: true);
            statements.Add(boundStatement);
        }

        EndEmulation(binderSaveState);

        var boundBlockStatement = new BoundBlockStatement(statements.ToImmutable());
        var loweredBlock = Lowerer.Lower(tempFunction, boundBlockStatement);

        returnType = null;

        foreach (var loweredStatement in loweredBlock.statements) {
            if (loweredStatement.type == BoundNodeType.ReturnStatement) {
                if (returnType == null) {
                    returnType = ((BoundReturnStatement)loweredStatement).expression.type;
                } else {
                    var type = ((BoundReturnStatement)loweredStatement).expression.type;

                    if (!BoundType.AboutEqual(returnType, type)) {
                        diagnostics.Push(Error.InconsistentReturnTypes(statement.closeBrace.location));
                        break;
                    }
                }
            }
        }

        var name = $"$Inline{_inlineCount++}";
        _innerPrefix.Push(name);
        var innerName = ConstructInnerName();
        _innerPrefix.Pop();

        var oldTypeClause = ReconstructTypeClause(returnType);
        var identifier = CreateToken(SyntaxType.IdentifierToken, name);

        var declaration = new FunctionDeclaration(
            null, oldTypeClause, identifier, null,
            new SeparatedSyntaxList<Parameter>(ImmutableArray<Node>.Empty), null, block);

        if (!_scope.TryDeclareFunction(
            new FunctionSymbol(name, ImmutableArray<ParameterSymbol>.Empty, returnType, declaration)))
            throw new BelteInternalException($"BindInlineFunctionExpression: failed to declare {innerName}");

        var localFunctionDeclaration = new LocalFunctionStatement(
            null, oldTypeClause, identifier, null,
            new SeparatedSyntaxList<Parameter>(ImmutableArray<Node>.Empty), null, block
        );

        _unresolvedLocals[innerName] = localFunctionDeclaration;

        var callExpression = new CallExpression(
            null, new NameExpression(null, identifier), null,
            new SeparatedSyntaxList<Expression>(ImmutableArray<Node>.Empty), null);

        return BindCallExpression(callExpression);
    }
    */

    private SyntaxToken CreateToken(SyntaxKind kind, string name = null, object value = null) {
        // TODO Make a syntax node factory
        return new SyntaxToken(
            null, kind, -1, name, value,
            ImmutableArray<SyntaxTrivia>.Empty, ImmutableArray<SyntaxTrivia>.Empty);
    }

    private TypeSyntax ReconstructTypeClause(BoundType type) {
        var attributes = ImmutableArray.CreateBuilder<(SyntaxToken, SyntaxToken, SyntaxToken)>();
        var brackets = ImmutableArray.CreateBuilder<(SyntaxToken, SyntaxToken)>();

        if (!type.isNullable)
            attributes.Add((null, CreateToken(SyntaxKind.IdentifierToken, "NotNull"), null));

        var constRefKeyword = type.isConstantReference
            ? CreateToken(SyntaxKind.ConstKeyword)
            : null;

        var refKeyword = type.isReference
            ? CreateToken(SyntaxKind.RefKeyword)
            : null;

        var constKeyword = type.isConstant
            ? CreateToken(SyntaxKind.ConstKeyword)
            : null;

        var typeName = CreateToken(SyntaxKind.IdentifierToken, type.typeSymbol.name);

        for (int i=0; i<type.dimensions; i++)
            brackets.Add((CreateToken(SyntaxKind.OpenBracketToken), CreateToken(SyntaxKind.CloseBracketToken)));

        return new TypeSyntax(
            null, attributes.ToImmutable(), constRefKeyword, refKeyword,
            constKeyword, typeName, brackets.ToImmutable());
    }

    private BoundStatement BindExpressionStatement(ExpressionStatementSyntax statement) {
        var expression = BindExpression(statement.expression, true, true);
        return new BoundExpressionStatement(expression);
    }

    private BoundExpression BindInitializerListExpression(
        InitializerListExpressionSyntax expression, BoundType type) {
        var boundItems = ImmutableArray.CreateBuilder<BoundExpression>();

        foreach (var item in expression.items) {
            BoundExpression tempItem = BindExpression(item);
            tempItem.type.isNullable = true;

            if (type == null || type.isImplicit) {
                var tempType = tempItem.type;

                type = new BoundType(
                    tempType.typeSymbol, false, tempType.isConstantReference, tempType.isReference,
                    tempType.isConstant, true, true, tempType.dimensions + 1);
            }

            var childType = type.ChildType();
            var boundItem = BindCast(item.location, tempItem, childType);
            boundItems.Add(boundItem);
        }

        return new BoundInitializerListExpression(boundItems.ToImmutable(), type.dimensions, type.ChildType());
    }

    private BoundExpression BindLiteralExpression(LiteralExpressionSyntax expression) {
        var value = expression.value;
        return new BoundLiteralExpression(value);
    }

    private BoundExpression BindUnaryExpression(UnaryExpressionSyntax expression) {
        var boundOperand = BindExpression(expression.operand);

        if (boundOperand.type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        var boundOp = BoundUnaryOperator.Bind(expression.op.kind, boundOperand.type);

        if (boundOp == null) {
            diagnostics.Push(
                Error.InvalidUnaryOperatorUse(expression.op.location, expression.op.text, boundOperand.type));
            return new BoundErrorExpression();
        }

        return new BoundUnaryExpression(boundOp, boundOperand);
    }

    private BoundExpression BindTernaryExpression(TernaryExpressionSyntax expression) {
        var boundLeft = BindExpression(expression.left);
        var boundCenter = BindExpression(expression.center);
        var boundRight = BindExpression(expression.right);

        if (boundLeft.type.typeSymbol == TypeSymbol.Error ||
            boundCenter.type.typeSymbol == TypeSymbol.Error ||
            boundRight.type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        var boundOp = BoundTernaryOperator.Bind(
            expression.leftOp.kind, expression.rightOp.kind, boundLeft.type,
            boundCenter.type, boundRight.type);

        if (boundOp == null) {
            diagnostics.Push(Error.InvalidTernaryOperatorUse(
                    expression.leftOp.location, expression.leftOp.text, boundLeft.type,
                    boundCenter.type, boundRight.type));
            return new BoundErrorExpression();
        }

        return new BoundTernaryExpression(boundLeft, boundOp, boundCenter, boundRight);
    }

    private BoundExpression BindBinaryExpression(BinaryExpressionSyntax expression) {
        var boundLeft = BindExpression(expression.left);
        var boundRight = BindExpression(expression.right);

        if (boundLeft.type.typeSymbol == TypeSymbol.Error || boundRight.type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        var boundOp = BoundBinaryOperator.Bind(expression.op.kind, boundLeft.type, boundRight.type);

        if (boundOp == null) {
            diagnostics.Push(Error.InvalidBinaryOperatorUse(
                    expression.op.location, expression.op.text, boundLeft.type, boundRight.type));
            return new BoundErrorExpression();
        }

        // Could possible move this to ComputeConstant
        if (boundOp.opType == BoundBinaryOperatorKind.EqualityEquals ||
            boundOp.opType == BoundBinaryOperatorKind.EqualityNotEquals ||
            boundOp.opType == BoundBinaryOperatorKind.LessThan ||
            boundOp.opType == BoundBinaryOperatorKind.LessOrEqual ||
            boundOp.opType == BoundBinaryOperatorKind.GreaterThan ||
            boundOp.opType == BoundBinaryOperatorKind.GreatOrEqual) {
            if (boundLeft.constantValue != null && boundRight.constantValue != null &&
                (boundLeft.constantValue.value == null) || (boundRight.constantValue.value == null)) {
                diagnostics.Push(Warning.AlwaysValue(expression.location, null));
                return new BoundLiteralExpression(null);
            }
        }

        return new BoundBinaryExpression(boundLeft, boundOp, boundRight);
    }

    /*
    * Temporary, inlines will be disabled until the StackFrameParser is added
    private BinderState StartEmulation() {
        var state = new BinderState();
        state.functionBodies = new List<(FunctionSymbol function, BoundBlockStatement body)>(_functionBodies);
        state.resolvedLocals = new List<string>(_resolvedLocals);
        state.unresolvedLocals = new Dictionary<string, LocalFunctionStatement>(_unresolvedLocals);
        state.diagnostics = new BelteDiagnosticQueue();
        state.diagnostics.Move(diagnostics);
        state.innerPrefix = new Stack<string>(_innerPrefix.Reverse());

        _scope = new BoundScope(_scope);
        _inlineCounts.Push(_inlineCount);
        _emulationDepth++;

        return state;
    }

    private void EndEmulation(BinderState oldState) {
        _scope = _scope.parent;
        _inlineCount = _inlineCounts.Pop();

        _resolvedLocals = new List<string>(oldState.resolvedLocals);
        _unresolvedLocals = new Dictionary<string, LocalFunctionStatement>(oldState.unresolvedLocals);
        _functionBodies.Clear();
        _functionBodies.AddRange(oldState.functionBodies);
        _innerPrefix.Clear();

        foreach (var item in oldState.innerPrefix.Reverse())
            _innerPrefix.Push(item);

        diagnostics.Clear();
        diagnostics.Move(oldState.diagnostics);
        _emulationDepth--;
    }
    */

    private BoundExpression BindParenExpression(ParenthesisExpressionSyntax expression) {
        return BindExpression(expression.expression);
    }

    private BoundExpression BindNameExpression(NameExpressionSyntax expression) {
        var name = expression.identifier.text;
        if (expression.identifier.isMissing)
            return new BoundErrorExpression();

        var variable = BindVariableReference(expression.identifier);
        if (variable == null)
            return new BoundErrorExpression();

        return new BoundVariableExpression(variable);
    }

    private BoundExpression BindEmptyExpression(EmptyExpressionSyntax expression) {
        return new BoundEmptyExpression();
    }

    private BoundExpression BindAssignmentExpression(AssignmentExpressionSyntax expression) {
        var left = BindExpression(expression.left);

        if (left is BoundErrorExpression)
            return left;

        if (!(left is BoundVariableExpression || left is BoundMemberAccessExpression || left is BoundIndexExpression)) {
            diagnostics.Push(Error.CannotAssign(expression.left.location));
            return new BoundErrorExpression();
        }

        var boundExpression = BindExpression(expression.right);
        var type = left.type;

        if (!type.isNullable && boundExpression is BoundLiteralExpression le && le.value == null) {
            diagnostics.Push(Error.NullAssignOnNotNull(expression.right.location));
            return boundExpression;
        }

        if ((type.isReference && type.isConstantReference &&
            boundExpression.kind == BoundNodeKind.ReferenceExpression) ||
            (type.isConstant && boundExpression.kind != BoundNodeKind.ReferenceExpression)) {
            string name = null;

            if (left is BoundVariableExpression v)
                name = v.variable.name;
            else if (left is BoundMemberAccessExpression m)
                name = m.member.name;

            diagnostics.Push(Error.ConstantAssignment(expression.assignmentToken.location, name));
        }

        if (expression.assignmentToken.kind != SyntaxKind.EqualsToken) {
            var equivalentOperatorTokenKind = SyntaxFacts.GetBinaryOperatorOfAssignmentOperator(
                expression.assignmentToken.kind);
            var boundOperator = BoundBinaryOperator.Bind(
                equivalentOperatorTokenKind, type, boundExpression.type);

            if (boundOperator == null) {
                diagnostics.Push(Error.InvalidBinaryOperatorUse(
                    expression.assignmentToken.location, expression.assignmentToken.text,
                    type, boundExpression.type));
                return new BoundErrorExpression();
            }

            var convertedExpression = BindCast(expression.right.location, boundExpression, type);
            return new BoundCompoundAssignmentExpression(left, boundOperator, convertedExpression);
        } else {
            var convertedExpression = BindCast(expression.right.location, boundExpression, type);
            return new BoundAssignmentExpression(left, convertedExpression);
        }
    }

    private VariableSymbol BindVariableReference(SyntaxToken identifier) {
        var name = identifier.text;
        VariableSymbol reference = null;

        switch (_scope.LookupSymbol(name)) {
            case VariableSymbol variable:
                reference = variable;
                break;
            case null:
                diagnostics.Push(Error.UndefinedName(identifier.location, name));
                break;
            default:
                diagnostics.Push(Error.NotAVariable(identifier.location, name));
                break;
        }

        if (reference != null && _trackSymbols)
            foreach (var frame in _trackedSymbols)
                frame.Add(reference);

        return reference;
    }

    private BoundStatement BindVariableDeclarationStatement(VariableDeclarationStatementSyntax expression) {
        var type = BindTypeClause(expression.type);

        if (type.isImplicit && expression.initializer == null) {
            diagnostics.Push(Error.NoInitOnImplicit(expression.identifier.location));
            return null;
        }

        if (type.isReference && expression.initializer == null) {
            diagnostics.Push(Error.ReferenceNoInitialization(expression.identifier.location));
            return null;
        }

        if (type.isReference && expression.initializer?.kind != SyntaxKind.RefExpression) {
            diagnostics.Push(Error.ReferenceWrongInitialization(expression.equals.location));
            return null;
        }

        if (expression.initializer is LiteralExpressionSyntax le) {
            if (le.token.kind == SyntaxKind.NullKeyword && type.isImplicit) {
                diagnostics.Push(Error.NullAssignOnImplicit(expression.initializer.location));
                return null;
            }
        }

        if (type.typeSymbol == TypeSymbol.Void) {
            diagnostics.Push(Error.VoidVariable(expression.type.typeName.location));
            return null;
        }

        if (!type.isReference && expression.initializer?.kind == SyntaxKind.RefExpression) {
            diagnostics.Push(Error.WrongInitializationReference(expression.equals.location));
            return null;
        }

        var nullable = type.isNullable;

        if (expression.initializer?.kind == SyntaxKind.RefExpression) {
            var initializer = BindReferenceExpression((ReferenceExpressionSyntax)expression.initializer);
            // References cant have implicit casts
            var variable = BindVariable(expression.identifier, type, initializer.constantValue);

            return new BoundVariableDeclarationStatement(variable, initializer);
        } else if (type.dimensions > 0 ||
            (type.isImplicit && expression.initializer is InitializerListExpressionSyntax)) {
            var initializer = expression.initializer.kind != SyntaxKind.NullKeyword
                ? BindInitializerListExpression(
                    (InitializerListExpressionSyntax)expression.initializer, type)
                : new BoundLiteralExpression(null);

            if (initializer is BoundInitializerListExpression il) {
                if (il.items.Length == 0 && type.isImplicit) {
                    diagnostics.Push(Error.EmptyInitializerListOnImplicit(expression.initializer.location));
                    return null;
                }
            }

            if (type.isImplicit && type.dimensions > 0) {
                diagnostics.Push(Error.ImpliedDimensions(expression.initializer.location));
                return null;
            }

            var variableType = type.isImplicit
                ? initializer.type
                : type;

            if (nullable)
                variableType = BoundType.Nullable(variableType);
            else
                variableType = BoundType.NonNullable(variableType);

            if (!variableType.isNullable && initializer is BoundLiteralExpression ble && ble.value == null) {
                diagnostics.Push(Error.NullAssignOnNotNull(expression.initializer.location));
                return null;
            }

            var itemType = variableType.BaseType();

            var castedInitializer = BindCast(expression.initializer?.location, initializer, variableType);
            var variable = BindVariable(expression.identifier,
                new BoundType(
                    itemType.typeSymbol, type.isImplicit, type.isConstantReference, type.isReference,
                    type.isConstant, type.isNullable, false, variableType.dimensions),
                    castedInitializer.constantValue);

            return new BoundVariableDeclarationStatement(variable, castedInitializer);
        } else {
            var initializer = expression.initializer != null
                ? BindExpression(expression.initializer)
                : new BoundLiteralExpression(null);

            var variableType = type.isImplicit
                ? initializer.type
                : type;

            if (nullable)
                variableType = BoundType.Nullable(variableType);
            else
                variableType = BoundType.NonNullable(variableType);

            if (!variableType.isNullable && initializer is BoundLiteralExpression ble && ble.value == null) {
                diagnostics.Push(Error.NullAssignOnNotNull(expression.initializer.location));
                return null;
            }

            var castedInitializer = BindCast(expression.initializer?.location, initializer, variableType);
            var variable = BindVariable(
                expression.identifier, variableType, castedInitializer.constantValue);

            return new BoundVariableDeclarationStatement(variable, castedInitializer);
        }
    }

    private BoundType BindTypeClause(TypeSyntax type) {
        bool isNullable = true;

        foreach (var attribute in type.attributes) {
            if (attribute.identifier.text == "NotNull")
                isNullable = false;
            else
                diagnostics.Push(Error.UnknownAttribute(attribute.identifier.location, attribute.identifier.text));
        }

        var isRef = type.refKeyword != null;
        var isConstRef = type.constRefKeyword != null && isRef;
        var isConst = type.constKeyword != null;
        var isImplicit = type.typeName.kind == SyntaxKind.VarKeyword;
        var dimensions = type.brackets.Length;

        var foundType = LookupType(type.typeName.text);
        if (foundType == null && !isImplicit)
            diagnostics.Push(Error.UnknownType(type.location, type.typeName.text));

        return new BoundType(
            foundType, isImplicit, isConstRef, isRef, isConst, isNullable, false, dimensions);
    }

    private VariableSymbol BindVariable(
        SyntaxToken identifier, BoundType type, BoundConstant constant = null, bool bindAsField = false) {
        var name = identifier.text ?? "?";
        var declare = !identifier.isMissing;
        var variable = bindAsField
            ? new FieldSymbol(name, type, constant)
            : _function == null
                ? (VariableSymbol) new GlobalVariableSymbol(name, type, constant)
                : new LocalVariableSymbol(name, type, constant);

        if (declare && !_scope.TryDeclareVariable(variable))
            diagnostics.Push(Error.AlreadyDeclared(identifier.location, name));

        if (_trackSymbols)
            foreach (var frame in _trackedDeclarations)
                frame.Add(variable);

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
            case "type":
                return TypeSymbol.Type;
            default:
                // If no type was found, we want to return null and type will be null if no type was found
                // So we just return type
                var type = _scope.LookupSymbol<TypeSymbol>(name);
                return type;
        }
    }
}
