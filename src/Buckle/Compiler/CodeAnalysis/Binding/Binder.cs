using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Buckle.CodeAnalysis.FlowAnalysis;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Binds a <see cref="Syntax.InternalSyntax.Parser" /> output into a immutable "bound" tree. This is where most error
/// checking happens. The <see cref="Lowerer" /> is also called here to simplify the code,
/// And convert control of flow into gotos and labels. Dead code is also removed here, as well as other optimizations.
/// </summary>
internal sealed class Binder {
    private readonly BoundType _enclosingType;
    private readonly List<(MethodSymbol method, BoundBlockStatement body)> _methodBodies =
        new List<(MethodSymbol, BoundBlockStatement)>();
    private readonly CompilationOptions _options;
    private readonly BinderFlags _flags;

    private OverloadResolution _overloadResolution;
    private BoundScope _scope;
    private Stack<(BoundLabel breakLabel, BoundLabel continueLabel)> _loopStack = new Stack<(BoundLabel, BoundLabel)>();
    private int _labelCount;
    private ImmutableArray<string> _peekedLocals = ImmutableArray<string>.Empty;
    private int _checkPeekedLocals = 0;
    // Methods should be available correctly, so only track variables
    private Stack<HashSet<VariableSymbol>> _trackedSymbols = new Stack<HashSet<VariableSymbol>>();
    private Stack<HashSet<VariableSymbol>> _trackedDeclarations = new Stack<HashSet<VariableSymbol>>();
    private Stack<string> _innerPrefix = new Stack<string>();
    private Stack<List<string>> _localLocals = new Stack<List<string>>();
    private List<string> _resolvedLocals = new List<string>();
    private Dictionary<string, LocalFunctionStatementSyntax> _unresolvedLocals =
        new Dictionary<string, LocalFunctionStatementSyntax>();
    private string _shadowingVariable;

    private Binder(CompilationOptions options, BinderFlags flags, BoundScope parent, MethodSymbol method) {
        diagnostics = new BelteDiagnosticQueue();
        _scope = new BoundScope(parent);
        _enclosingType = method?.type;
        _options = options;
        _flags = method is null ? flags : flags | BinderFlags.Method;
        _overloadResolution = new OverloadResolution(this);

        if (method != null) {
            foreach (var parameter in method.parameters)
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
    /// If being bound as a script, otherwise an application.
    /// </param>
    /// <param name="previous">Previous <see cref="BoundGlobalScope" /> (if applicable).</param>
    /// <param name="syntaxTrees">All SyntaxTrees, as files are bound together.</param>
    /// <param name="transpilerMode">
    /// If the compiler output mode is a transpiler. Affects certain optimizations.
    /// </param>
    /// <returns>A new <see cref="BoundGlobalScope" />.</returns>
    internal static BoundGlobalScope BindGlobalScope(
        CompilationOptions options, BoundGlobalScope previous, ImmutableArray<SyntaxTree> syntaxTrees) {
        var parentScope = CreateParentScope(previous);
        var binder = new Binder(options, options.topLevelBinderFlags, parentScope, null);

        if (binder.diagnostics.Errors().Any())
            return GlobalScope(previous, binder.diagnostics);

        var typeDeclarations = syntaxTrees.SelectMany(st => st.GetCompilationUnitRoot().members)
            .OfType<TypeDeclarationSyntax>();

        foreach (var @type in typeDeclarations)
            binder.BindTypeDeclaration(@type);

        var methodDeclarations = syntaxTrees.SelectMany(st => st.GetCompilationUnitRoot().members)
            .OfType<MethodDeclarationSyntax>();

        foreach (var method in methodDeclarations)
            binder.BindMethodDeclaration(method);

        var globalStatements = syntaxTrees.SelectMany(st => st.GetCompilationUnitRoot().members)
            .OfType<GlobalStatementSyntax>();

        var statements = ImmutableArray.CreateBuilder<BoundStatement>();

        binder._peekedLocals = PeekLocals(globalStatements.Select(s => s.statement), null);

        foreach (var globalStatement in globalStatements)
            statements.Add(binder.BindStatement(globalStatement.statement, true));

        var firstGlobalPerTree = syntaxTrees
            .Select(st => st.GetCompilationUnitRoot().members.OfType<GlobalStatementSyntax>().FirstOrDefault())
            .Where(g => g != null).ToArray();

        if (firstGlobalPerTree.Length > 1) {
            foreach (var globalStatement in firstGlobalPerTree)
                binder.diagnostics.Push(Error.GlobalStatementsInMultipleFiles(globalStatement.location));
        }

        var methods = binder._scope.GetDeclaredMethods();

        MethodSymbol entryPoint = null;

        if (binder._options.isScript) {
            if (globalStatements.Any()) {
                entryPoint = new MethodSymbol(
                    "<Eval>$", ImmutableArray<ParameterSymbol>.Empty, BoundType.NullableAny
                );
            }
        } else {
            entryPoint = methods.FirstOrDefault(f => f.name.ToLower() == "main");

            if (entryPoint != null) {
                if (entryPoint.type.typeSymbol != TypeSymbol.Void && entryPoint.type.typeSymbol != TypeSymbol.Int)
                    binder.diagnostics.Push(Error.InvalidMain(entryPoint.declaration.returnType.location));

                if (entryPoint.parameters.Any()) {
                    var span = TextSpan.FromBounds(
                        entryPoint.declaration.parameterList.openParenthesis.span.start + 1,
                        entryPoint.declaration.parameterList.closeParenthesis.span.end - 1
                    );

                    var location = new TextLocation(entryPoint.declaration.syntaxTree.text, span);
                    binder.diagnostics.Push(Error.InvalidMain(location));
                }
            }

            if (globalStatements.Any()) {
                if (entryPoint != null) {
                    binder.diagnostics.Push(Error.MainAndGlobals(entryPoint.declaration.identifier.location));

                    foreach (var globalStatement in firstGlobalPerTree)
                        binder.diagnostics.Push(Error.MainAndGlobals(globalStatement.location));
                } else {
                    entryPoint = new MethodSymbol(
                        "<Main>$", ImmutableArray<ParameterSymbol>.Empty, new BoundType(TypeSymbol.Void)
                    );
                }
            }
        }

        var variables = binder._scope.GetDeclaredVariables();
        var types = binder._scope.GetDeclaredTypes().Select(t => t as NamedTypeSymbol);

        if (previous != null)
            binder.diagnostics.CopyToFront(previous.diagnostics);

        var methodBodies = previous is null
            ? binder._methodBodies.ToImmutableArray()
            : previous.methodBodies.AddRange(binder._methodBodies);

        return new BoundGlobalScope(
            methodBodies, previous, binder.diagnostics,
            entryPoint, methods, variables, types.ToImmutableArray(), statements.ToImmutable()
        );
    }

    /// <summary>
    /// Binds a program.
    /// </summary>
    /// <param name="isScript">If being bound as a script, otherwise an application.</param>
    /// <param name="previous">Previous <see cref="BoundProgram" /> (if applicable).</param>
    /// <param name="globalScope">The already bound <see cref="BoundGlobalScope" />.</param>
    /// <param name="transpilerMode">
    /// If the compiler output mode is a transpiler. Affects certain optimizations.
    /// </param>
    /// <returns>A new <see cref="BoundProgram" /> (then either emitted or evaluated).</returns>
    internal static BoundProgram BindProgram(
        CompilationOptions options, BoundProgram previous, BoundGlobalScope globalScope) {
        var parentScope = CreateParentScope(globalScope);

        if (globalScope.diagnostics.Errors().Any())
            return Program(previous, globalScope.diagnostics);

        var methodBodies = ImmutableDictionary.CreateBuilder<MethodSymbol, BoundBlockStatement>();
        var diagnostics = new BelteDiagnosticQueue();
        diagnostics.Move(globalScope.diagnostics);

        foreach (var method in globalScope.methods) {
            // Some special methods (like default constructors) are bound earlier
            var existingBodies = globalScope.methodBodies.Where(t => t.method == method);

            if (existingBodies.Count() != 0) {
                // If there are multiple entries something went wrong
                var existingBody = existingBodies.ToArray().Single();
                methodBodies.Add(existingBody.method, existingBody.body);
                continue;
            }

            var binder = new Binder(options, options.topLevelBinderFlags, parentScope, method) {
                _innerPrefix = new Stack<string>()
            };

            binder._innerPrefix.Push(method.name);
            var body = binder.BindMethodBody(method.declaration.body, method.parameters);
            diagnostics.Move(binder.diagnostics);

            if (diagnostics.Errors().Any())
                return Program(previous, diagnostics);

            var loweredBody = Lowerer.Lower(method, body, options.isTranspiling);

            if (method.type.typeSymbol != TypeSymbol.Void && !ControlFlowGraph.AllPathsReturn(loweredBody))
                binder.diagnostics.Push(Error.NotAllPathsReturn(method.declaration.identifier.location));

            binder._methodBodies.Add((method, loweredBody));

            foreach (var methodBody in binder._methodBodies) {
                var newParameters = ImmutableArray.CreateBuilder<ParameterSymbol>();
                var parametersChanged = false;

                foreach (var parameter in methodBody.method.parameters) {
                    var name = parameter.name.StartsWith("$")
                        ? parameter.name.Substring(1)
                        : parameter.name;

                    if (name != parameter.name)
                        parametersChanged = true;

                    var newParameter = new ParameterSymbol(
                        name, parameter.type, parameter.ordinal, parameter.defaultValue
                    );

                    newParameters.Add(newParameter);
                }

                if (parametersChanged) {
                    var newMethod = methodBody.method.UpdateParameters(newParameters.ToImmutable());
                    methodBodies.Add(newMethod, methodBody.body);
                } else {
                    methodBodies.Add(methodBody.method, methodBody.body);
                }
            }

            diagnostics.Move(binder.diagnostics);
        }

        if (globalScope.entryPoint != null && globalScope.statements.Any() && !options.isScript) {
            var body = Lowerer.Lower(
                globalScope.entryPoint, new BoundBlockStatement(globalScope.statements), options.isTranspiling
            );

            methodBodies.Add(globalScope.entryPoint, body);
        } else if (globalScope.entryPoint != null && options.isScript) {
            var statements = globalScope.statements;

            if (statements.Length == 1 && statements[0] is BoundExpressionStatement es &&
                es.expression.type?.typeSymbol != TypeSymbol.Void) {
                statements = statements.SetItem(0, new BoundReturnStatement(es.expression));
            } else if (statements.Any() && statements.Last().kind != BoundNodeKind.ReturnStatement) {
                statements = statements.Add(new BoundReturnStatement(null));
            }

            var body = Lowerer.Lower(
                globalScope.entryPoint, new BoundBlockStatement(statements), options.isTranspiling
            );

            methodBodies.Add(globalScope.entryPoint, body);
        }

        return new BoundProgram(
            previous, diagnostics, globalScope.entryPoint, methodBodies.ToImmutable(), globalScope.types
        );
    }

    /// <summary>
    /// Binds a created cast.
    /// </summary>
    /// <param name="expression"><see cref="Expression" /> to bind and cast.</param>
    /// <param name="type">Type to cast the bound <param name="expression" /> to.</param>
    /// <param name="allowExplicit"></param>
    /// <param name="argument"></param>
    /// <param name="isImplicitNull"></param>
    /// <returns>Created bound cast.</returns>
    internal BoundExpression BindCast(
        ExpressionSyntax expression, BoundType type, bool allowExplicit = false,
        int argument = 0, bool isImplicitNull = false) {
        var boundExpression = BindExpression(expression);

        return BindCast(expression.location, boundExpression, type, allowExplicit, argument, isImplicitNull);
    }

    internal BoundExpression BindCast(
        TextLocation diagnosticLocation, BoundExpression expression, BoundType type,
        bool allowExplicit = false, int argument = 0, bool isImplicitNull = false) {
        return BindCast(diagnosticLocation, expression, type, out _, allowExplicit, argument, isImplicitNull);
    }

    internal BoundExpression BindCast(
        TextLocation diagnosticLocation, BoundExpression expression, BoundType type,
        out Cast castType, bool allowExplicit = false, int argument = 0, bool isImplicitNull = false) {
        var conversion = Cast.Classify(expression.type, type);
        castType = conversion;

        if (expression.type.typeSymbol == TypeSymbol.Error || type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        if (BoundConstant.IsNull(expression.constantValue) && !type.isNullable) {
            if (isImplicitNull)
                diagnostics.Push(Error.CannotImplyNull(diagnosticLocation));
            else
                diagnostics.Push(Error.CannotConvertNull(diagnosticLocation, type, argument));

            return new BoundErrorExpression();
        }

        if (!conversion.exists)
            diagnostics.Push(Error.CannotConvert(diagnosticLocation, expression.type, type, argument));

        if (!allowExplicit && conversion.isExplicit)
            diagnostics.Push(Error.CannotConvertImplicitly(diagnosticLocation, expression.type, type, argument));

        if (conversion.isIdentity) {
            if (expression.type.typeSymbol != null)
                return expression;
            else if (expression.constantValue != null)
                return new BoundTypeWrapper(type, expression.constantValue);
        }

        return new BoundCastExpression(type, expression);
    }

    private static ImmutableArray<string> PeekLocals(
        IEnumerable<StatementSyntax> statements, IEnumerable<ParameterSymbol> parameters) {
        var locals = ImmutableArray.CreateBuilder<string>();

        foreach (var statement in statements) {
            if (statement is VariableDeclarationStatementSyntax vd)
                locals.Add(vd.identifier.text);
        }

        if (parameters != null) {
            foreach (var parameter in parameters)
                locals.Add(parameter.name);
        }

        return locals.ToImmutable();
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

            foreach (var method in previous.methods)
                scope.TryDeclareMethod(method);

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

        foreach (var method in BuiltinMethods.GetAll())
            result.TryDeclareMethod(method);

        return result;
    }

    private string ConstructInnerName() {
        var name = "<";

        for (var i = _innerPrefix.Count - 1; i > 0; i--) {
            name += _innerPrefix.ToArray()[i];

            if (i > 1)
                name += "::";
        }

        name += $">g__{_innerPrefix.Peek()}";
        return name;
    }

    private ImmutableArray<Symbol> LookupTypes(string name, bool strict = false) {
        var types = _scope.LookupOverloads(name);

        if (strict)
            types = types.Where(t => t is TypeSymbol).ToImmutableArray();

        TypeSymbol type;

        switch (name) {
            case "any":
                type = TypeSymbol.Any;
                break;
            case "bool":
                type = TypeSymbol.Bool;
                break;
            case "int":
                type = TypeSymbol.Int;
                break;
            case "decimal":
                type = TypeSymbol.Decimal;
                break;
            case "string":
                type = TypeSymbol.String;
                break;
            case "void":
                type = TypeSymbol.Void;
                break;
            case "type":
                type = TypeSymbol.Type;
                break;
            default:
                type = null;
                break;
        }

        if (types.Where(t => t is TypeSymbol).Count() == 0 && type != null)
            return ImmutableArray.Create<Symbol>(type);

        return types;
    }

    private ImmutableArray<ParameterSymbol> BindParameterList(ParameterListSyntax parameterList) {
        return BindParameters(parameterList.parameters);
    }

    private ImmutableArray<ParameterSymbol> BindParameters(SeparatedSyntaxList<ParameterSyntax> parameters) {
        var parametersBuilder = ImmutableArray.CreateBuilder<ParameterSymbol>();
        var seenParameterNames = new HashSet<string>();

        for (var i = 0; i < parameters.Count; i++) {
            var parameter = parameters[i];
            var parameterName = parameter.identifier.text;
            var parameterType = BindType(parameter.type);
            var boundDefault = parameter.defaultValue is null
                ? null
                : BindExpression(parameter.defaultValue);

            if (boundDefault != null && boundDefault.constantValue is null) {
                diagnostics.Push(Error.DefaultMustBeConstant(parameter.defaultValue.location));
                continue;
            }

            if (boundDefault != null && i < parameters.Count - 1 && parameters[i + 1].defaultValue is null) {
                diagnostics.Push(Error.DefaultBeforeNoDefault(parameter.location));
                continue;
            }

            if (!seenParameterNames.Add(parameterName)) {
                diagnostics.Push(Error.ParameterAlreadyDeclared(parameter.location, parameter.identifier.text));
            } else {
                var boundParameter = new ParameterSymbol(parameterName, parameterType, parameters.Count, boundDefault);
                parametersBuilder.Add(boundParameter);
            }
        }

        return parametersBuilder.ToImmutable();
    }

    private BoundStatement BindMethodBody(BlockStatementSyntax syntax, ImmutableArray<ParameterSymbol> parameters) {
        _peekedLocals = PeekLocals(syntax.statements, parameters);

        return BindStatement(syntax);
    }

    private MethodSymbol BindMethodDeclaration(MethodDeclarationSyntax method, string name = null) {
        var type = BindType(method.returnType);
        var parameters = BindParameterList(method.parameterList);
        var newMethod = new MethodSymbol(name ?? method.identifier.text, parameters, type, method);

        if (newMethod.declaration.identifier.text != null && !_scope.TryDeclareMethod(newMethod))
            diagnostics.Push(Error.MethodAlreadyDeclared(method.identifier.location, name ?? newMethod.name));

        return newMethod;
    }

    private TypeSymbol BindTypeDeclaration(TypeDeclarationSyntax @type) {
        if (@type is StructDeclarationSyntax s)
            return BindStructDeclaration(s);
        else if (@type is ClassDeclarationSyntax c)
            return BindClassDeclaration(c);
        else
            throw new BelteInternalException($"BindTypeDeclaration: unexpected type '{@type.identifier.text}'");
    }

    private StructSymbol BindStructDeclaration(StructDeclarationSyntax @struct) {
        var builder = ImmutableList.CreateBuilder<Symbol>();
        _scope = new BoundScope(_scope);

        foreach (var fieldDeclaration in @struct.members.OfType<FieldDeclarationSyntax>()) {
            var field = BindFieldDeclaration(fieldDeclaration);
            builder.Add(field);
        }

        _scope = _scope.parent;

        var newStruct = new StructSymbol(ImmutableArray<ParameterSymbol>.Empty, builder.ToImmutableArray(), @struct);

        if (!_scope.TryDeclareType(newStruct))
            diagnostics.Push(Error.TypeAlreadyDeclared(@struct.identifier.location, @struct.identifier.text, false));
        else
            diagnostics.Push(Error.CannotUseStruct(@struct.keyword.location));

        return newStruct;
    }

    private ClassSymbol BindClassDeclaration(ClassDeclarationSyntax @class) {
        var builder = ImmutableList.CreateBuilder<Symbol>();
        var templateBuilder = ImmutableList.CreateBuilder<ParameterSymbol>();
        _scope = new BoundScope(_scope);

        if (@class.templateParameterList != null) {
            var templateParameters = BindParameters(@class.templateParameterList.parameters);

            foreach (var templateParameter in templateParameters) {
                builder.Add(templateParameter);
                templateBuilder.Add(templateParameter);
            }
        }

        foreach (var fieldDeclaration in @class.members.OfType<FieldDeclarationSyntax>()) {
            var field = BindFieldDeclaration(fieldDeclaration);
            builder.Add(field);
        }

        var defaultConstructor = new MethodSymbol(".ctor", ImmutableArray<ParameterSymbol>.Empty, null);
        builder.Add(defaultConstructor);
        _methodBodies.Add((defaultConstructor, new BoundBlockStatement(ImmutableArray<BoundStatement>.Empty)));
        // This should never fail
        _scope.TryDeclareMethod(defaultConstructor);

        foreach (var methodDeclaration in @class.members.OfType<MethodDeclarationSyntax>()) {
            var method = BindMethodDeclaration(methodDeclaration);
            builder.Add(method);
            _methodBodies.Add(
                (method, BindMethodBody(methodDeclaration.body, method.parameters) as BoundBlockStatement)
            );
        }

        foreach (var typeDeclaration in @class.members.OfType<TypeDeclarationSyntax>()) {
            var type = BindTypeDeclaration(typeDeclaration);
            builder.Add(type);
        }

        // This allows the methods to be seen by the global scope
        foreach (var method in _scope.GetDeclaredMethods())
            _scope.parent.DeclareMethodStrict(method);

        _scope = _scope.parent;

        var newClass = new ClassSymbol(templateBuilder.ToImmutableArray(), builder.ToImmutableArray(), @class);

        if (!_scope.TryDeclareType(newClass))
            diagnostics.Push(Error.TypeAlreadyDeclared(@class.identifier.location, @class.identifier.text, true));

        return newClass;
    }

    private BoundStatement BindLocalFunctionDeclaration(LocalFunctionStatementSyntax statement) {
        _innerPrefix.Push(statement.identifier.text);
        var functionSymbol = (MethodSymbol)_scope.LookupSymbol(ConstructInnerName());
        _innerPrefix.Pop();

        var binder = new Binder(_options, (_flags | BinderFlags.LocalFunction), _scope, functionSymbol) {
            _innerPrefix = new Stack<string>(_innerPrefix.Reverse()),
            _trackedSymbols = _trackedSymbols,
            _trackedDeclarations = _trackedDeclarations
        };

        binder._trackedSymbols.Push(new HashSet<VariableSymbol>());
        binder._trackedDeclarations.Push(new HashSet<VariableSymbol>());
        binder._innerPrefix.Push(functionSymbol.name);
        var body = binder.BindMethodBody(functionSymbol.declaration.body, functionSymbol.parameters);

        var usedVariables = binder._trackedSymbols.Pop();
        var declaredVariables = binder._trackedDeclarations.Pop();
        var ordinal = functionSymbol.parameters.Count();
        var parameters = ImmutableArray.CreateBuilder<ParameterSymbol>();

        foreach (var parameter in functionSymbol.parameters)
            parameters.Add(parameter);

        var parametersChanged = false;

        foreach (var variable in usedVariables) {
            if (declaredVariables.Contains(variable) || parameters.Contains(variable))
                continue;

            parametersChanged = true;
            var parameter = new ParameterSymbol(
                $"${variable.name}",
                BoundType.CopyWith(variable.type, isReference: true, isExplicitReference: true),
                ordinal++,
                null
            );

            parameters.Add(parameter);
        }

        var newFunctionSymbol = parametersChanged ?
            functionSymbol.UpdateParameters(parameters.ToImmutable())
            : functionSymbol;

        var loweredBody = Lowerer.Lower(newFunctionSymbol, body, _options.isTranspiling);

        if (newFunctionSymbol.type.typeSymbol != TypeSymbol.Void && !ControlFlowGraph.AllPathsReturn(loweredBody))
            diagnostics.Push(Error.NotAllPathsReturn(newFunctionSymbol.declaration.identifier.location));

        _methodBodies.Add((newFunctionSymbol, loweredBody));
        diagnostics.Move(binder.diagnostics);
        _methodBodies.AddRange(binder._methodBodies);

        if (!_scope.TryReplaceSymbol(functionSymbol, newFunctionSymbol))
            throw new BelteInternalException($"BindLocalFunction: failed to set function '{functionSymbol.name}'");

        return new BoundBlockStatement(ImmutableArray<BoundStatement>.Empty);
    }

    private FieldSymbol BindFieldDeclaration(FieldDeclarationSyntax fieldDeclaration) {
        var type = BindType(fieldDeclaration.declaration.type);

        return BindVariable(
            fieldDeclaration.declaration.identifier, type, bindAsField: true) as FieldSymbol;
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

    private BoundType BindType(TypeSyntax type) {
        var isNullable = true;

        foreach (var attribute in type.attributes) {
            if (attribute.identifier.text == "NotNull") {
                if (isNullable) {
                    isNullable = false;
                } else {
                    diagnostics.Push(
                        Error.DuplicateAttribute(attribute.identifier.location, attribute.identifier.text)
                    );
                }
            } else {
                diagnostics.Push(Error.UnknownAttribute(attribute.identifier.location, attribute.identifier.text));
            }
        }

        var name = type.typeName?.text;
        var isReference = type.refKeyword != null;
        var isConstantReference = type.constRefKeyword != null && isReference;
        var isConstant = type.constKeyword != null;
        var isVariable = type.varKeyword != null;
        var isImplicit = type.typeName is null;
        var dimensions = type.rankSpecifiers.Count;
        var arity = type.templateArgumentList?.arguments?.Count ?? 0;

        if (isImplicit && isReference) {
            diagnostics.Push(Error.ImpliedReference(type.refKeyword.location, isConstant));
            return null;
        }

        if (isImplicit && dimensions > 0) {
            var span = TextSpan.FromBounds(
                type.rankSpecifiers.First().openBracket.location.span.start,
                type.rankSpecifiers.Last().closeBracket.location.span.end
            );

            var location = new TextLocation(type.location.text, span);
            diagnostics.Push(Error.ImpliedDimensions(location, isConstant));

            return null;
        }

        if (isConstant && isVariable) {
            var span = TextSpan.FromBounds(
                type.constKeyword.location.span.start,
                type.varKeyword.location.span.end
            );

            var location = new TextLocation(type.location.text, span);
            diagnostics.Push(Error.ConstantAndVariable(location));

            return null;
        }

        TypeSymbol foundType = TypeSymbol.Error;
        ImmutableArray<BoundExpression>? arguments = null;

        if (!isImplicit) {
            var foundTypes = LookupTypes(name);
            var namedCount = foundTypes.Where(t => t is NamedTypeSymbol).Count();

            if (foundTypes.Length == 0) {
                diagnostics.Push(Error.UnknownType(type.location, type.typeName.text));
            } else if (namedCount == 0 &&
                foundTypes.Where(t => t is TypeSymbol).Count() > 0) {
                foundType = foundTypes.Where(t => t is TypeSymbol).First() as TypeSymbol;
            } else if (namedCount == 0) {
                // ? Maybe add a new diagnostic here like 'cannot use `x` as a type'
                diagnostics.Push(Error.UnknownType(type.location, type.typeName.text));
            } else {
                if (PartiallyBindTemplateArgumentList(type.templateArgumentList, out var boundArguments)) {
                    var result = _overloadResolution.TemplateOverloadResolution(
                        foundTypes.Where(t => t is NamedTypeSymbol)
                            .Select(t => t as NamedTypeSymbol).ToImmutableArray(),
                        boundArguments,
                        name,
                        type.typeName,
                        type.templateArgumentList
                    );

                    if (result.succeeded) {
                        foundType = result.bestOverload;
                        arguments = result.arguments;
                    }
                }
            }
        }

        return new BoundType(
            foundType,
            isImplicit,
            isConstantReference,
            isReference,
            false,
            isConstant,
            isNullable,
            false,
            dimensions,
            arguments
        );
    }

    private VariableSymbol BindVariableReference(SyntaxToken identifier) {
        var name = identifier.text;
        VariableSymbol reference = null;

        switch (name == _shadowingVariable ? null : _scope.LookupSymbol(name)) {
            case VariableSymbol variable:
                reference = variable;
                break;
            case null:
                diagnostics.Push(Error.UndefinedSymbol(identifier.location, name));
                break;
            default:
                diagnostics.Push(Error.NotAVariable(identifier.location, name));
                break;
        }

        if (reference != null && _flags.Includes(BinderFlags.LocalFunction)) {
            foreach (var frame in _trackedSymbols)
                frame.Add(reference);
        }

        return reference;
    }

    private VariableSymbol BindVariable(
        SyntaxToken identifier, BoundType type, BoundConstant constant = null, bool bindAsField = false) {
        var name = identifier.text ?? "?";
        var declare = !identifier.isFabricated;
        var variable = bindAsField
            ? new FieldSymbol(name, type, constant)
            : _flags.Includes(BinderFlags.Method)
                ? new LocalVariableSymbol(name, type, constant)
                : (VariableSymbol)new GlobalVariableSymbol(name, type, constant);

        if (LookupTypes(name, true).Length > 0) {
            diagnostics.Push(Error.VariableUsingTypeName(identifier.location, name, type.isConstant));
            return variable;
        }

        if (declare && !_scope.TryDeclareVariable(variable))
            diagnostics.Push(Error.VariableAlreadyDeclared(identifier.location, name, type.isConstant));

        if (_flags.Includes(BinderFlags.LocalFunction)) {
            foreach (var frame in _trackedDeclarations)
                frame.Add(variable);
        }

        return variable;
    }

    private BoundStatement BindStatement(StatementSyntax syntax, bool isGlobal = false) {
        var result = BindStatementInternal(syntax);

        if (!_options.isScript || !isGlobal) {
            if (result is BoundExpressionStatement es) {
                var isAllowedExpression = es.expression.kind == BoundNodeKind.CallExpression ||
                    es.expression.kind == BoundNodeKind.AssignmentExpression ||
                    es.expression.kind == BoundNodeKind.ErrorExpression ||
                    es.expression.kind == BoundNodeKind.EmptyExpression ||
                    es.expression.kind == BoundNodeKind.CompoundAssignmentExpression ||
                    es.expression.kind == BoundNodeKind.PrefixExpression ||
                    es.expression.kind == BoundNodeKind.PostfixExpression;

                if (!isAllowedExpression)
                    diagnostics.Push(Error.InvalidExpressionStatement(syntax.location));
            }
        }

        return result;
    }

    private BoundStatement BindStatementInternal(StatementSyntax syntax) {
        switch (syntax.kind) {
            case SyntaxKind.BlockStatement:
                return BindBlockStatement((BlockStatementSyntax)syntax);
            case SyntaxKind.ExpressionStatement:
                return BindExpressionStatement((ExpressionStatementSyntax)syntax);
            case SyntaxKind.VariableDeclarationStatement:
                var statement = BindVariableDeclarationStatement((VariableDeclarationStatementSyntax)syntax);
                _shadowingVariable = null;
                return statement;
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

    private BoundStatement BindTryStatement(TryStatementSyntax expression) {
        var body = (BoundBlockStatement)BindBlockStatement(expression.body);
        var catchBody = expression.catchClause is null
            ? null
            : (BoundBlockStatement)BindBlockStatement(expression.catchClause.body);

        var finallyBody = expression.finallyClause is null
            ? null
            : (BoundBlockStatement)BindBlockStatement(expression.finallyClause.body);

        return new BoundTryStatement(body, catchBody, finallyBody);
    }

    private BoundStatement BindReturnStatement(ReturnStatementSyntax expression) {
        var boundExpression = expression.expression is null ? null : BindExpression(expression.expression);

        if (_flags.Includes(BinderFlags.Method)) {
            if (_enclosingType.typeSymbol == TypeSymbol.Void) {
                if (boundExpression != null)
                    diagnostics.Push(Error.UnexpectedReturnValue(expression.keyword.location));
            } else {
                if (boundExpression is null)
                    diagnostics.Push(Error.MissingReturnValue(expression.keyword.location));
                else
                    boundExpression = BindCast(expression.expression.location, boundExpression, _enclosingType);
            }
        } else {
            if (!_options.isScript && boundExpression != null)
                diagnostics.Push(Error.Unsupported.GlobalReturnValue(expression.keyword.location));
        }

        return new BoundReturnStatement(boundExpression);
    }

    private BoundStatement BindErrorStatement() {
        return new BoundExpressionStatement(new BoundErrorExpression());
    }

    private BoundStatement BindContinueStatement(ContinueStatementSyntax syntax) {
        if (_loopStack.Count == 0) {
            diagnostics.Push(Error.InvalidBreakOrContinue(syntax.keyword.location, syntax.keyword.text));
            return BindErrorStatement();
        }

        if (!_options.isTranspiling) {
            var continueLabel = _loopStack.Peek().continueLabel;
            return new BoundGotoStatement(continueLabel);
        } else {
            return new BoundContinueStatement();
        }
    }

    private BoundStatement BindBreakStatement(BreakStatementSyntax syntax) {
        if (_loopStack.Count == 0) {
            diagnostics.Push(Error.InvalidBreakOrContinue(syntax.keyword.location, syntax.keyword.text));
            return BindErrorStatement();
        }

        if (!_options.isTranspiling) {
            var breakLabel = _loopStack.Peek().breakLabel;
            return new BoundGotoStatement(breakLabel);
        } else {
            return new BoundBreakStatement();
        }
    }

    private BoundStatement BindWhileStatement(WhileStatementSyntax statement) {
        var condition = BindCast(statement.condition, BoundType.NullableBool);

        if (BoundConstant.IsNotNull(condition.constantValue) && !(bool)condition.constantValue.value)
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
        _checkPeekedLocals++;

        var initializer = BindStatement(statement.initializer);
        var condition = BindCast(statement.condition, BoundType.NullableBool);
        var step = BindExpression(statement.step);
        var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel);

        _scope = _scope.parent;
        _checkPeekedLocals--;

        return new BoundForStatement(initializer, condition, step, body, breakLabel, continueLabel);
    }

    private BoundStatement BindIfStatement(IfStatementSyntax statement) {
        var condition = BindCast(statement.condition, BoundType.NullableBool);

        BoundLiteralExpression constant = null;

        if (BoundConstant.IsNotNull(condition.constantValue)) {
            if (!(bool)condition.constantValue.value)
                diagnostics.Push(Warning.UnreachableCode(statement.then));
            else if (statement.elseClause != null)
                diagnostics.Push(Warning.UnreachableCode(statement.elseClause.body));

            constant = new BoundLiteralExpression(condition.constantValue.value);
        }

        var then = BindStatement(statement.then);
        var elseStatement = statement.elseClause is null
            ? null
            : BindStatement(statement.elseClause.body);

        if (constant != null)
            return new BoundIfStatement(constant, then, elseStatement);

        return new BoundIfStatement(condition, then, elseStatement);
    }

    private BoundStatement BindBlockStatement(BlockStatementSyntax statement) {
        _checkPeekedLocals++;

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
                frame.Add(fd.identifier.text);
                _innerPrefix.Push(fd.identifier.text);
                var innerName = ConstructInnerName();

                var declaration = SyntaxFactory.MethodDeclaration(
                    fd.returnType,
                    fd.identifier,
                    fd.parameterList,
                    fd.body,
                    fd.parent,
                    fd.position
                );

                BindMethodDeclaration(declaration, innerName);

                if (!_unresolvedLocals.TryAdd(ConstructInnerName(), fd)) {
                    diagnostics.Push(Error.CannotOverloadNested(
                        declaration.identifier.location, declaration.identifier.text)
                    );
                }

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

        _checkPeekedLocals--;

        return new BoundBlockStatement(statements.ToImmutable());
    }

    private BoundStatement BindVariableDeclarationStatement(VariableDeclarationStatementSyntax expression) {
        var currentCount = diagnostics.Errors().Count;
        var type = BindType(expression.type);

        if (diagnostics.Errors().Count > currentCount)
            return null;

        if (type.isImplicit && expression.initializer is null) {
            diagnostics.Push(Error.NoInitOnImplicit(expression.identifier.location));
            return null;
        }

        if (type.isReference && expression.initializer is null) {
            diagnostics.Push(Error.ReferenceNoInitialization(expression.identifier.location, type.isConstant));
            return null;
        }

        if (type.isReference && expression.initializer?.kind != SyntaxKind.ReferenceExpression) {
            diagnostics.Push(Error.ReferenceWrongInitialization(expression.equals.location, type.isConstant));
            return null;
        }

        if (expression.initializer is LiteralExpressionSyntax le) {
            if (le.token.kind == SyntaxKind.NullKeyword && type.isImplicit) {
                diagnostics.Push(Error.NullAssignOnImplicit(expression.initializer.location, type.isConstant));
                return null;
            }
        }

        if (type.typeSymbol == TypeSymbol.Void) {
            diagnostics.Push(Error.VoidVariable(expression.type.typeName.location));
            return null;
        }

        var isNullable = type.isNullable;
        _shadowingVariable = expression.identifier.text;

        if (_peekedLocals.Contains(expression.identifier.text) && _checkPeekedLocals > 1) {
            diagnostics.Push(
                Error.NameUsedInEnclosingScope(expression.identifier.location, expression.identifier.text)
            );
        }

        if (type.isReference || (type.isImplicit && expression.initializer?.kind == SyntaxKind.ReferenceExpression)) {
            var initializer = BindReferenceExpression((ReferenceExpressionSyntax)expression.initializer);

            if (diagnostics.Errors().Count > currentCount)
                return null;

            var tempType = type.isImplicit ? initializer.type : type;
            var variableType = BoundType.CopyWith(
                tempType,
                isConstant: (type.isConstant && !type.isImplicit) ? true : null,
                isConstantReference: ((type.isConstant && type.isImplicit) || type.isConstantReference) ? true : null,
                isNullable: isNullable,
                isLiteral: false
            );

            if (initializer.type.isConstant && !variableType.isConstant) {
                diagnostics.Push(Error.ReferenceToConstant(
                    expression.equals.location, variableType.isConstantReference)
                );

                return null;
            }

            if (!initializer.type.isConstant && variableType.isConstant) {
                diagnostics.Push(Error.ConstantToNonConstantReference(
                    expression.equals.location, variableType.isConstantReference)
                );

                return null;
            }

            if (diagnostics.Errors().Count > currentCount)
                return null;

            // References cant have implicit casts
            var variable = BindVariable(expression.identifier, variableType, initializer.constantValue);

            return new BoundVariableDeclarationStatement(variable, initializer);
        } else if (type.dimensions > 0 ||
            (type.isImplicit && expression.initializer is InitializerListExpressionSyntax)) {
            var initializer = (expression.initializer is null ||
                (expression.initializer is LiteralExpressionSyntax l && l.token.kind == SyntaxKind.NullKeyword))
                ? new BoundTypeWrapper(type, new BoundConstant(null))
                : BindExpression(expression.initializer, initializerListType: type);

            if (initializer is BoundInitializerListExpression il && type.isImplicit) {
                if (il.items.Length == 0) {
                    diagnostics.Push(
                        Error.EmptyInitializerListOnImplicit(expression.initializer.location, type.isConstant)
                    );

                    return null;
                } else {
                    var allNull = true;

                    foreach (var item in il.items) {
                        if (!BoundConstant.IsNull(item.constantValue))
                            allNull = false;
                    }

                    if (allNull) {
                        diagnostics.Push(
                            Error.NullInitializerListOnImplicit(expression.initializer.location, type.isConstant)
                        );

                        return null;
                    }
                }
            }

            var tempType = type.isImplicit ? initializer.type : type;
            var variableType = BoundType.CopyWith(
                tempType, isConstant: type.isConstant ? true : null, isNullable: isNullable, isLiteral: false
            );

            if (!variableType.isNullable && initializer is BoundLiteralExpression ble && ble.value is null) {
                diagnostics.Push(Error.NullAssignOnNotNull(expression.initializer.location, variableType.isConstant));
                return null;
            }

            var itemType = variableType.BaseType();

            var castedInitializer = BindCast(expression.initializer?.location, initializer, variableType);
            var variable = BindVariable(expression.identifier,
                BoundType.CopyWith(
                    type, typeSymbol: itemType.typeSymbol, isExplicitReference: false,
                    isLiteral: false, dimensions: variableType.dimensions
                ),
                castedInitializer.constantValue
            );

            if (diagnostics.Errors().Count > currentCount)
                return null;

            return new BoundVariableDeclarationStatement(variable, castedInitializer);
        } else {
            var initializer = expression.initializer != null
                ? BindExpression(expression.initializer)
                : new BoundTypeWrapper(type, new BoundConstant(null));

            var tempType = type.isImplicit ? initializer.type : type;
            var variableType = BoundType.CopyWith(
                tempType, isConstant: type.isConstant ? true : null, isNullable: isNullable, isLiteral: false
            );

            // If this is null, it means OverloadResolution failed and no method could be assumed, meaning that either a
            // non-method was called or no overload could be assumed in the initializer. If this is the case, the type
            // cannot be assumed an there is no easy way to stop cascading errors, so we don't.
            if (variableType is null)
                return null;

            if (!variableType.isNullable && initializer is BoundLiteralExpression ble && ble.value is null) {
                diagnostics.Push(Error.NullAssignOnNotNull(expression.initializer.location, variableType.isConstant));
                return null;
            }

            if (!variableType.isReference && expression.initializer?.kind == SyntaxKind.ReferenceExpression) {
                diagnostics.Push(
                    Error.WrongInitializationReference(expression.equals.location, variableType.isConstant)
                );

                return null;
            }

            var castedInitializer = BindCast(expression.initializer?.location, initializer, variableType);
            var variable = BindVariable(expression.identifier, variableType, castedInitializer.constantValue);

            if (initializer.constantValue is null || initializer.constantValue.value != null)
                _scope.NoteAssignment(variable);

            if (diagnostics.Errors().Count > currentCount)
                return null;

            return new BoundVariableDeclarationStatement(variable, castedInitializer);
        }
    }

    private BoundStatement BindExpressionStatement(ExpressionStatementSyntax statement) {
        var expression = BindExpression(statement.expression, true, true);

        return new BoundExpressionStatement(expression);
    }

    private BoundExpression BindExpression(
        ExpressionSyntax expression, bool canBeVoid = false,
        bool ownStatement = false, BoundType initializerListType = null) {
        var result = BindExpressionInternal(expression, ownStatement, initializerListType);

        if (!canBeVoid && result.type?.typeSymbol == TypeSymbol.Void) {
            diagnostics.Push(Error.NoValue(expression.location));
            return new BoundErrorExpression();
        }

        return result;
    }

    private BoundExpression BindExpressionInternal(
        ExpressionSyntax expression, bool ownStatement, BoundType initializerListType) {
        switch (expression.kind) {
            case SyntaxKind.LiteralExpression:
                if (expression is InitializerListExpressionSyntax il)
                    return BindInitializerListExpression(il, initializerListType);
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
            case SyntaxKind.TemplateNameExpression:
            case SyntaxKind.IdentifierNameExpression:
                return BindNameExpression((NameExpressionSyntax)expression);
            case SyntaxKind.AssignmentExpression:
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
            case SyntaxKind.ReferenceExpression:
                return BindReferenceExpression((ReferenceExpressionSyntax)expression);
            case SyntaxKind.CastExpression:
                return BindCastExpression((CastExpressionSyntax)expression);
            case SyntaxKind.TypeOfExpression:
                return BindTypeOfExpression((TypeOfExpressionSyntax)expression);
            case SyntaxKind.MemberAccessExpression:
                return BindMemberAccessExpression((MemberAccessExpressionSyntax)expression);
            case SyntaxKind.ObjectCreationExpression:
                return BindObjectCreationExpression((ObjectCreationExpressionSyntax)expression);
            default:
                throw new BelteInternalException($"BindExpressionInternal: unexpected syntax '{expression.kind}'");
        }
    }

    private BoundExpression BindObjectCreationExpression(ObjectCreationExpressionSyntax expression) {
        var type = BindType(expression.type);

        if (type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        if (type.typeSymbol is not NamedTypeSymbol) {
            diagnostics.Push(Error.CannotConstructPrimitive(expression.type.typeName.location, type.typeSymbol.name));
            return new BoundErrorExpression();
        }

        if (!PartiallyBindArgumentList(expression.argumentList, out var arguments))
            return new BoundErrorExpression();

        var result = _overloadResolution.MethodOverloadResolution(
            (type.typeSymbol as NamedTypeSymbol).constructors,
            arguments,
            type.typeSymbol.name,
            expression.type,
            expression.argumentList
        );

        if (!result.succeeded)
            return new BoundErrorExpression();

        return new BoundObjectCreationExpression(type, result.bestOverload, result.arguments);
    }

    private BoundExpression BindMemberAccessExpression(MemberAccessExpressionSyntax expression) {
        var operand = BindExpression(expression.operand);

        if (operand is BoundErrorExpression)
            return operand;

        if (operand.type.typeSymbol is not ITypeSymbolWithMembers) {
            diagnostics.Push(
                Error.PrimitivesDoNotHaveMembers(expression.op.location)
            );

            return new BoundErrorExpression();
        }

        var type = operand.type.typeSymbol as ITypeSymbolWithMembers;

        FieldSymbol symbol = null;

        foreach (var field in @type.members.Where(f => f is FieldSymbol)) {
            if (field.name == expression.identifier.text)
                symbol = field as FieldSymbol;
        }

        if (symbol is null) {
            diagnostics.Push(
                Error.NoSuchMember(expression.identifier.location, operand.type, expression.identifier.text)
            );

            return new BoundErrorExpression();
        }

        if (operand.type.isNullable && operand is BoundVariableExpression ve &&
            !_scope.GetAssignedVariables().Contains(ve.variable)) {
            diagnostics.Push(Warning.NullDeference(expression.op.location));
        }

        return new BoundMemberAccessExpression(operand, symbol, expression.op.kind == SyntaxKind.QuestionPeriodToken);
    }

    private BoundExpression BindTypeOfExpression(TypeOfExpressionSyntax expression) {
        var type = BindType(expression.type);

        return new BoundTypeOfExpression(type);
    }

    private BoundExpression BindReferenceExpression(ReferenceExpressionSyntax expression) {
        var variable = BindVariableReference(expression.identifier);

        return new BoundReferenceExpression(variable);
    }

    private BoundExpression BindPostfixExpression(PostfixExpressionSyntax expression, bool ownStatement = false) {
        var boundOperand = BindExpression(expression.operand);

        if (boundOperand is not BoundVariableExpression &&
            boundOperand is not BoundMemberAccessExpression &&
            boundOperand is not BoundIndexExpression) {
            diagnostics.Push(Error.CannotIncrement(expression.operand.location));
            return new BoundErrorExpression();
        }

        var type = boundOperand.type;

        if (type.isConstant) {
            string name = null;

            if (boundOperand is BoundVariableExpression v)
                name = v.variable.name;
            else if (boundOperand is BoundMemberAccessExpression m)
                name = m.member.name;

            diagnostics.Push(Error.ConstantAssignment(expression.op.location, name, false));

            return new BoundErrorExpression();
        }

        var boundOp = BoundPostfixOperator.Bind(expression.op.kind, boundOperand.type);

        if (boundOp is null) {
            diagnostics.Push(Error.InvalidPostfixUse(expression.op.location, expression.op.text, boundOperand.type));
            return new BoundErrorExpression();
        }

        return new BoundPostfixExpression(boundOperand, boundOp, ownStatement);
    }

    private BoundExpression BindPrefixExpression(PrefixExpressionSyntax expression) {
        var boundOperand = BindExpression(expression.operand);

        if (boundOperand is not BoundVariableExpression &&
            boundOperand is not BoundMemberAccessExpression &&
            boundOperand is not BoundIndexExpression) {
            diagnostics.Push(Error.CannotIncrement(expression.operand.location));
            return new BoundErrorExpression();
        }

        var type = boundOperand.type;

        if (type.isConstant) {
            string name = null;

            if (boundOperand is BoundVariableExpression v)
                name = v.variable.name;
            else if (boundOperand is BoundMemberAccessExpression m)
                name = m.member.name;

            diagnostics.Push(Error.ConstantAssignment(expression.op.location, name, false));

            return new BoundErrorExpression();
        }

        var boundOp = BoundPrefixOperator.Bind(expression.op.kind, boundOperand.type);

        if (boundOp is null) {
            diagnostics.Push(Error.InvalidPrefixUse(expression.op.location, expression.op.text, boundOperand.type));
            return new BoundErrorExpression();
        }

        return new BoundPrefixExpression(boundOp, boundOperand);
    }

    private BoundExpression BindIndexExpression(IndexExpressionSyntax expression) {
        var boundExpression = BindExpression(expression.operand);

        if (boundExpression.type.dimensions > 0) {
            var index = BindCast(
                expression.index.location, BindExpression(expression.index), new BoundType(TypeSymbol.Int)
            );

            return new BoundIndexExpression(
                boundExpression, index, expression.openBracket.kind == SyntaxKind.QuestionOpenBracketToken
            );
        } else {
            diagnostics.Push(Error.CannotApplyIndexing(expression.location, boundExpression.type));
            return new BoundErrorExpression();
        }
    }

    private BoundExpression BindCallExpression(CallExpressionSyntax expression) {
        var name = ((NameExpressionSyntax)expression.operand).identifier.text;

        _innerPrefix.Push(name);
        var innerName = ConstructInnerName();
        _innerPrefix.Pop();

        var symbols = _scope.LookupOverloads(name, innerName);

        if (symbols.Length == 0) {
            diagnostics.Push(Error.UndefinedMethod(
                ((NameExpressionSyntax)expression.operand).location,
                name,
                _options.buildMode == BuildMode.Interpret
            ));

            return new BoundErrorExpression();
        } else if (symbols.Where(m => m is MethodSymbol).Count() == 0) {
            diagnostics.Push(Error.CannotCallNonMethod(expression.operand.location, name));
            return new BoundErrorExpression();
        }

        var isInner = false;

        if (_unresolvedLocals.ContainsKey(innerName) && !_resolvedLocals.Contains(innerName)) {
            BindLocalFunctionDeclaration(_unresolvedLocals[innerName]);
            _resolvedLocals.Add(innerName);
            isInner = true;

            if (symbols.Length > 1) {
                throw new BelteInternalException(
                    "BindCallExpression: overloaded generated function"
                );
            }
        }

        ImmutableArray<MethodSymbol> methods;

        if (isInner)
            methods = ImmutableArray.Create(_scope.LookupSymbol<MethodSymbol>(innerName));
        else
            methods = symbols.Where(s => s is MethodSymbol).Select(s => s as MethodSymbol).ToImmutableArray();

        if (!PartiallyBindArgumentList(expression.argumentList, out var arguments))
            return new BoundErrorExpression();

        var result = _overloadResolution.MethodOverloadResolution(
            methods,
            arguments,
            name,
            expression.operand,
            expression.argumentList
        );

        if (!result.succeeded)
            return new BoundErrorExpression();

        return new BoundCallExpression(result.bestOverload as MethodSymbol, result.arguments);
    }

    private bool PartiallyBindArgumentList(
        ArgumentListSyntax argumentList, out ImmutableArray<(string, BoundExpression)> arguments) {
        if (argumentList is null) {
            arguments = ImmutableArray<(string, BoundExpression)>.Empty;
            return true;
        } else {
            return PartiallyBindArguments(argumentList.arguments, out arguments);
        }
    }

    private bool PartiallyBindTemplateArgumentList(
        TemplateArgumentListSyntax argumentList, out ImmutableArray<(string, BoundExpression)> arguments) {
        if (argumentList is null) {
            arguments = ImmutableArray<(string, BoundExpression)>.Empty;
            return true;
        } else {
            return PartiallyBindArguments(argumentList.arguments, out arguments);
        }
    }

    private bool PartiallyBindArguments(
        SeparatedSyntaxList<ArgumentSyntax> arguments, out ImmutableArray<(string, BoundExpression)> boundArguments) {
        var argumentsBuilder = ImmutableArray.CreateBuilder<(string name, BoundExpression expression)>();
        var seenNames = new HashSet<string>();

        for (var i = 0; i < arguments.Count; i++) {
            var argumentName = arguments[i].identifier;

            if (i < arguments.Count - 1 &&
                argumentName != null &&
                arguments[i + 1].identifier is null) {
                diagnostics.Push(Error.NamedBeforeUnnamed(argumentName.location));
                boundArguments = ImmutableArray<(string, BoundExpression)>.Empty;

                return false;
            }

            if (argumentName != null && !seenNames.Add(argumentName.text)) {
                diagnostics.Push(Error.NamedArgumentTwice(argumentName.location, argumentName.text));
                boundArguments = ImmutableArray<(string, BoundExpression)>.Empty;

                return false;
            }

            var boundExpression = BindExpression(arguments[i].expression);

            if (boundExpression is BoundEmptyExpression)
                boundExpression = new BoundLiteralExpression(null, true);

            argumentsBuilder.Add((argumentName?.text, boundExpression));
        }

        boundArguments = argumentsBuilder.ToImmutable();

        return true;
    }

    private BoundExpression BindCastExpression(CastExpressionSyntax expression) {
        var toType = BindType(expression.type);
        var boundExpression = BindExpression(expression.expression);

        return BindCast(expression.location, boundExpression, toType, true);
    }

    private BoundExpression BindInitializerListExpression(InitializerListExpressionSyntax expression, BoundType type) {
        var boundItems = ImmutableArray.CreateBuilder<BoundExpression>();

        foreach (var item in expression.items) {
            var tempItem = BindExpression(item);
            tempItem = tempItem is BoundEmptyExpression ? new BoundLiteralExpression(null) : tempItem;

            // If the type is incomplete in any way, get a new one
            if (type is null || type.isImplicit || type.typeSymbol is null) {
                var tempType = tempItem.type;

                type = BoundType.CopyWith(
                    tempType, isImplicit: false, isNullable: type?.isNullable,
                    isLiteral: true, dimensions: tempType.dimensions + 1
                );
            }

            var childType = type.ChildType();
            var boundItem = BindCast(item.location, tempItem, childType);
            boundItems.Add(boundItem);
        }

        return new BoundInitializerListExpression(boundItems.ToImmutable(), type);
    }

    private BoundExpression BindLiteralExpression(LiteralExpressionSyntax expression) {
        var value = expression.token.value;
        return new BoundLiteralExpression(value);
    }

    private BoundExpression BindUnaryExpression(UnaryExpressionSyntax expression) {
        var boundOperand = BindExpression(expression.operand);

        if (boundOperand.type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        var boundOp = BoundUnaryOperator.Bind(expression.op.kind, boundOperand.type);

        if (boundOp is null) {
            diagnostics.Push(
                Error.InvalidUnaryOperatorUse(expression.op.location, expression.op.text, boundOperand.type)
            );

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
            boundRight.type.typeSymbol == TypeSymbol.Error) {
            return new BoundErrorExpression();
        }

        var boundOp = BoundTernaryOperator.Bind(
            expression.leftOp.kind, expression.rightOp.kind, boundLeft.type,
            boundCenter.type, boundRight.type
        );

        if (boundOp is null) {
            diagnostics.Push(Error.InvalidTernaryOperatorUse(
                expression.leftOp.location, $"{expression.leftOp.text}{expression.rightOp.text}",
                boundLeft.type, boundCenter.type, boundRight.type)
            );

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

        if (boundOp is null) {
            diagnostics.Push(Error.InvalidBinaryOperatorUse(
                expression.op.location, expression.op.text, boundLeft.type, boundRight.type, false)
            );

            return new BoundErrorExpression();
        }

        if (boundOp.opKind != BoundBinaryOperatorKind.NullCoalescing &&
            boundOp.opKind != BoundBinaryOperatorKind.Is &&
            boundOp.opKind != BoundBinaryOperatorKind.Isnt &&
            boundOp.opKind != BoundBinaryOperatorKind.ConditionalAnd &&
            boundOp.opKind != BoundBinaryOperatorKind.ConditionalOr) {
            if (BoundConstant.IsNull(boundLeft.constantValue) || BoundConstant.IsNull(boundRight.constantValue)) {
                diagnostics.Push(Warning.AlwaysValue(expression.location, null));
                return new BoundTypeWrapper(boundOp.type, new BoundConstant(null));
            }
        }

        if (boundOp.opKind == BoundBinaryOperatorKind.Division &&
            boundRight.constantValue != null && boundRight.constantValue.value.Equals(0)) {
            diagnostics.Push(Error.DivideByZero(expression.location));
            return new BoundErrorExpression();
        }

        return new BoundBinaryExpression(boundLeft, boundOp, boundRight);
    }

    private BoundExpression BindParenExpression(ParenthesisExpressionSyntax expression) {
        return BindExpression(expression.expression);
    }

    private BoundExpression BindNameExpression(NameExpressionSyntax expression) {
        if (expression.identifier.isFabricated)
            return new BoundErrorExpression();

        var variable = BindVariableReference(expression.identifier);

        if (variable is null)
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

        if (left is not BoundVariableExpression &&
            left is not BoundMemberAccessExpression &&
            left is not BoundIndexExpression) {
            diagnostics.Push(Error.CannotAssign(expression.left.location));
            return new BoundErrorExpression();
        }

        var boundExpression = BindExpression(expression.right);
        var type = left.type;

        if (!type.isNullable && boundExpression is BoundLiteralExpression le && le.value is null) {
            diagnostics.Push(Error.NullAssignOnNotNull(expression.right.location, false));
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

            diagnostics.Push(Error.ConstantAssignment(
                expression.assignmentToken.location, name, type.isConstantReference && boundExpression.type.isReference
            ));
        }

        if (expression.assignmentToken.kind != SyntaxKind.EqualsToken) {
            var equivalentOperatorTokenKind = SyntaxFacts.GetBinaryOperatorOfAssignmentOperator(
                expression.assignmentToken.kind
            );

            var boundOperator = BoundBinaryOperator.Bind(
                equivalentOperatorTokenKind, type, boundExpression.type
            );

            if (boundOperator is null) {
                diagnostics.Push(Error.InvalidBinaryOperatorUse(
                    expression.assignmentToken.location, expression.assignmentToken.text,
                    type, boundExpression.type, true)
                );

                return new BoundErrorExpression();
            }

            var convertedExpression = BindCast(expression.right.location, boundExpression, type);

            return new BoundCompoundAssignmentExpression(left, boundOperator, convertedExpression);
        } else {
            var convertedExpression = BindCast(expression.right.location, boundExpression, type);

            if (left is BoundVariableExpression ve) {
                if (ve.variable.constantValue is null || BoundConstant.IsNotNull(ve.variable.constantValue))
                    _scope.NoteAssignment(ve.variable);
            }

            return new BoundAssignmentExpression(left, convertedExpression);
        }
    }
}
