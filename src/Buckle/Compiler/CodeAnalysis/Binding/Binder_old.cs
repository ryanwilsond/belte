using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Linq.Expressions;
using Buckle.CodeAnalysis.FlowAnalysis;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries.Standard;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Binds a <see cref="Syntax.InternalSyntax.LanguageParser" /> output into a immutable "bound" tree. This is where most
/// error checking happens. The <see cref="Lowerer" /> is also called here to simplify the code and convert control of
/// flow into gotos and labels. Dead code is also removed here, as well as other optimizations.
/// </summary>
internal sealed class Binder {
    private readonly MethodSymbol _containingMethod;
    private readonly NamedTypeSymbol _containingType;
    private readonly CompilationOptions _options;
    private readonly OverloadResolution _overloadResolution;
    private readonly Dictionary<string, NamedTypeSymbol> _wellKnownTypes;
    private readonly List<(MethodSymbol method, BoundBlockStatement body)> _methodBodies = [];
    private readonly Stack<(BoundLabel breakLabel, BoundLabel continueLabel)> _loopStack = [];
    private readonly Stack<List<string>> _localLocals = [];
    private readonly List<string> _resolvedLocals = [];
    private readonly Dictionary<string, LocalFunctionStatementSyntax> _unresolvedLocals = [];
    private readonly HashSet<NamedTypeSymbol> _usedLibraryTypes = [];

    private BinderFlags _flags;
    private BoundScope _scope;
    private int _labelCount;
    private int _checkPeekedLocals = 0;
    private ImmutableArray<string> _peekedLocals = [];
    private Stack<HashSet<VariableSymbol>> _trackedSymbols = [];
    private Stack<HashSet<VariableSymbol>> _trackedDeclarations = [];
    private Stack<string> _innerPrefix = [];
    private string _shadowingVariable;

    private Binder(
        CompilationOptions options,
        BinderFlags flags,
        BoundScope parent,
        MethodSymbol method,
        Dictionary<string, NamedTypeSymbol> wellKnownTypes) {
        diagnostics = new BelteDiagnosticQueue();
        _scope = new BoundScope(parent);
        _containingMethod = method;
        _containingType = method?.containingType;
        _options = options;
        _flags = flags;
        _overloadResolution = new OverloadResolution(this);
        _wellKnownTypes = wellKnownTypes ?? [];

        var needsNewScope = false;

        if (_containingMethod != null && _containingMethod.isLowLevel)
            _flags |= BinderFlags.LowLevelContext;

        if (_containingType != null) {
            needsNewScope = true;
            _flags |= BinderFlags.Class;

            if (_containingType.isLowLevel)
                _flags |= BinderFlags.LowLevelContext;
        }

        var currentContainingType = _containingType;

        while (currentContainingType != null) {
            foreach (var member in currentContainingType.GetMembersPublic()) {
                if (member is FieldSymbol or ParameterSymbol)
                    _scope.TryDeclareVariable(member as VariableSymbol);
                else if (member is NamedTypeSymbol n)
                    _scope.TryDeclareType(n);
            }

            currentContainingType = currentContainingType.containingType;
        }

        if (method != null) {
            _flags |= BinderFlags.Method;

            if (needsNewScope)
                _scope = new BoundScope(_scope);

            foreach (var parameter in method.parameters)
                _scope.TryDeclareVariable(parameter);

            foreach (var templateParameter in method.templateParameters) {
                if (templateParameter.type.typeSymbol == TypeSymbol.Type) {
                    var templateType = new TemplateParameterSymbol(templateParameter);
                    _scope.TryDeclareType(templateType);
                } else {
                    _scope.TryDeclareVariable(templateParameter);
                }
            }
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
        CompilationOptions options,
        BoundGlobalScope previous,
        ImmutableArray<SyntaxTree> syntaxTrees) {
        var parentScope = CreateParentScope(previous, options.projectType);
        var binder = new Binder(options, options.topLevelBinderFlags, parentScope, null, previous?.libraryTypes);

        if (!binder._wellKnownTypes.ContainsKey(WellKnownTypeNames.Object)) {
            binder._wellKnownTypes.Add(WellKnownTypeNames.Object, StandardLibrary.Object);

            foreach (var member in StandardLibrary.Object.members)
                binder._scope.TryDeclareMethod(member as MethodSymbol);
        }

        if (binder.diagnostics.Errors().Any())
            return GlobalScope(previous, binder.diagnostics);

        var members = syntaxTrees.SelectMany(st => st.GetCompilationUnitRoot().members);

        foreach (var member in members) {
            if (member is TypeDeclarationSyntax ts) {
                var symbol = binder.PreBindTypeDeclaration(ts, DeclarationModifiers.None);

                if (options.isLibrary) {
                    if (symbol.name == WellKnownTypeNames.List)
                        binder._wellKnownTypes.Add(WellKnownTypeNames.List, symbol);
                    else if (symbol.name == WellKnownTypeNames.Dictionary)
                        binder._wellKnownTypes.Add(WellKnownTypeNames.Dictionary, symbol);
                    else if (symbol.name == WellKnownTypeNames.Exception)
                        binder._wellKnownTypes.Add(WellKnownTypeNames.Exception, symbol);
                }
            }
        }

        StandardLibrary.UpdateLibraries(binder._wellKnownTypes);

        foreach (var member in members) {
            if (member is TypeDeclarationSyntax ts)
                binder.BindTypeDeclaration(ts);
            else if (member is MethodDeclarationSyntax ms)
                binder.BindMethodDeclaration(ms, DeclarationModifiers.None);
        }

        var globalStatements = members.OfType<GlobalStatementSyntax>();
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
        MethodSymbol graphicsStart = null;
        MethodSymbol graphicsUpdate = null;

        if (binder._options.isScript) {
            if (globalStatements.Any())
                entryPoint = new MethodSymbol("<Eval>$", [], [], [], BoundType.NullableAny);
        } else {
            entryPoint = ResolveEntryPoint(methods, binder, globalStatements, firstGlobalPerTree);

            if (options.projectType == ProjectType.Graphics) {
                graphicsStart = methods
                    .Where(f => f.name.ToLower() == WellKnownMethodNames.GraphicsStart && f.parameters.Length == 0)
                    .FirstOrDefault();
                graphicsUpdate = methods
                    .Where(f => f.name.ToLower() == WellKnownMethodNames.GraphicsUpdate &&
                                f.parameters.Length == 1 &&
                                (f.parameters[0].type == BoundType.Decimal ||
                                f.parameters[0].type == BoundType.NullableDecimal))
                    .FirstOrDefault();
            }
        }

        var variables = binder._scope.GetDeclaredVariables();
        var types = binder._scope.GetDeclaredTypes().Select(t => t as NamedTypeSymbol);

        if (previous != null)
            binder.diagnostics.CopyToFront(previous.diagnostics);

        var methodBodies = previous is null
            ? binder._methodBodies.ToImmutableArray()
            : previous.methodBodies.AddRange(binder._methodBodies);

        var wellKnownMethods = new Dictionary<string, MethodSymbol> {
            { WellKnownMethodNames.EntryPoint, entryPoint },
            { WellKnownMethodNames.GraphicsStart, graphicsStart },
            { WellKnownMethodNames.GraphicsUpdate, graphicsUpdate }
        };

        return new BoundGlobalScope(
            methodBodies,
            previous,
            binder.diagnostics,
            wellKnownMethods,
            methods,
            variables,
            types.ToImmutableArray(),
            statements.ToImmutable(),
            binder._wellKnownTypes,
            binder._usedLibraryTypes
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
        CompilationOptions options,
        BoundProgram previous,
        BoundGlobalScope globalScope) {
        var parentScope = CreateParentScope(globalScope, options.projectType);

        if (globalScope.diagnostics.Errors().Any())
            return Program(previous, globalScope.diagnostics);

        var methodBodies = ImmutableDictionary.CreateBuilder<MethodSymbol, BoundBlockStatement>();
        var diagnostics = new BelteDiagnosticQueue();
        var usedLibraryTypes = previous is null
            ? globalScope.usedLibraryTypes
            : globalScope.usedLibraryTypes.Union(previous.usedLibraryTypes).ToHashSet();
        diagnostics.Move(globalScope.diagnostics);

        foreach (var method in globalScope.methods) {
            var binder = new Binder(
                options,
                options.topLevelBinderFlags,
                parentScope,
                method,
                globalScope.libraryTypes
            );

            binder._innerPrefix.Push(method.name);
            var body = binder.BindMethodBody(
                method.declaration,
                method.parameters,
                method.containingType?.declaration?.identifier
            );

            diagnostics.Move(binder.diagnostics);

            if (diagnostics.Errors().Any())
                return Program(previous, diagnostics);

            var (flattenedBody, loweredBody) = Lowerer.Lower(
                method,
                body,
                options.isTranspiling
            );

            if (!method.isAbstract &&
                method.type.typeSymbol != TypeSymbol.Void &&
                !ControlFlowGraph.AllPathsReturn(flattenedBody)) {
                binder.diagnostics.Push(Error.NotAllPathsReturn(GetIdentifierLocation(method.declaration)));
            }

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

                    var newParameter = ParameterSymbol.CreateWithNewName(parameter, name);
                    newParameters.Add(newParameter);
                }

                if (parametersChanged) {
                    var newMethod = methodBody.method.UpdateParameters(newParameters.ToImmutable());
                    methodBodies.Add(newMethod, methodBody.body);
                } else {
                    methodBodies.Add(methodBody.method, methodBody.body);
                }
            }

            usedLibraryTypes.UnionWith(binder._usedLibraryTypes);
            diagnostics.Move(binder.diagnostics);
        }

        var entryPoint = globalScope.wellKnownMethods[WellKnownMethodNames.EntryPoint];

        if (entryPoint != null && globalScope.statements.Any() && !options.isScript) {
            var (_, body) = Lowerer.Lower(
                entryPoint,
                new BoundBlockStatement(globalScope.statements),
                options.isTranspiling
            );

            methodBodies.Add(entryPoint, body);
        } else if (entryPoint != null && options.isScript) {
            var statements = globalScope.statements;

            if (statements.Length == 1 && statements[0] is BoundExpressionStatement es &&
                es.expression.type?.typeSymbol != TypeSymbol.Void) {
                statements = statements.SetItem(0, new BoundReturnStatement(es.expression));
            } else if (statements.Any() && statements.Last().kind != BoundNodeKind.ReturnStatement) {
                statements = statements.Add(new BoundReturnStatement(null));
            }

            var (_, body) = Lowerer.Lower(
                entryPoint,
                new BoundBlockStatement(statements),
                options.isTranspiling
            );

            methodBodies.Add(entryPoint, body);
        }

        return new BoundProgram(
            previous,
            diagnostics,
            globalScope.wellKnownMethods,
            methodBodies.ToImmutable(),
            globalScope.types,
            usedLibraryTypes.ToImmutableArray()
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
        ExpressionSyntax expression,
        BoundType type,
        bool allowExplicit = false,
        int argument = 0,
        bool isImplicitNull = false,
        BoundType receiverType = null,
        ImmutableArray<TypeOrConstant>? templateArguments = null) {
        var boundExpression = BindExpression(expression);

        return BindCast(
            expression.location,
            boundExpression,
            type,
            allowExplicit,
            argument,
            isImplicitNull,
            receiverType: receiverType,
            templateArguments: templateArguments
        );
    }

    internal BoundExpression BindCast(
        TextLocation diagnosticLocation,
        BoundExpression expression,
        BoundType type,
        bool allowExplicit = false,
        int argument = 0,
        bool isImplicitNull = false,
        BoundType receiverType = null,
        ImmutableArray<TypeOrConstant>? templateArguments = null) {
        return BindCast(
            diagnosticLocation,
            expression,
            type,
            out _,
            allowExplicit,
            argument,
            isImplicitNull,
            receiverType: receiverType,
            templateArguments: templateArguments
        );
    }

    internal BoundExpression BindCast(
        TextLocation diagnosticLocation,
        BoundExpression expression,
        BoundType type,
        out Cast castType,
        bool allowExplicit = false,
        int argument = 0,
        bool isImplicitNull = false,
        bool isTemplate = false,
        BoundType receiverType = null,
        ImmutableArray<TypeOrConstant>? templateArguments = null) {
        var fromType = expression.type;
        var toType = type;

        if (expression is BoundType)
            fromType = BoundType.Type;

        if (receiverType is not null)
            toType = BoundType.Compound(receiverType, toType);
        else if (templateArguments is not null)
            toType = BoundType.Compound(templateArguments.Value, toType);

        var conversion = Cast.Classify(fromType, toType);
        castType = conversion;

        if (expression.type.typeSymbol == TypeSymbol.Error || toType.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        if (ConstantValue.IsNull(expression.constantValue) && !toType.isNullable) {
            if (isImplicitNull)
                diagnostics.Push(Error.CannotImplyNull(diagnosticLocation));
            else
                diagnostics.Push(Error.CannotConvertNull(diagnosticLocation, toType, argument));

            return new BoundErrorExpression();
        }

        if (isTemplate && expression is BoundType && toType.typeSymbol == TypeSymbol.Type) {
            conversion = Cast.Identity;
            castType = conversion;
        }

        if (!conversion.exists)
            diagnostics.Push(Error.CannotConvert(diagnosticLocation, expression.type, toType, argument));

        if (!allowExplicit && conversion.isExplicit) {
            var canAssert = false;

            if (expression.type.typeSymbol.kind == toType.typeSymbol.kind &&
                expression.type.isNullable && !toType.isNullable) {
                canAssert = true;
            }

            diagnostics.Push(
                Error.CannotConvertImplicitly(diagnosticLocation, expression.type, toType, argument, canAssert)
            );
        }

        if (conversion.isIdentity) {
            if (expression.type.typeSymbol != null)
                return expression;
            else if (expression.constantValue != null)
                return new BoundLiteralExpression(expression.constantValue, toType);
        }

        return new BoundCastExpression(toType, expression);
    }

    private static MethodSymbol ResolveEntryPoint(
        ImmutableArray<MethodSymbol> methods,
        Binder binder,
        IEnumerable<GlobalStatementSyntax> globalStatements,
        GlobalStatementSyntax[] firstGlobalPerTree) {
        MethodSymbol entryPoint = null;
        var mains = methods.Where(f => f.name.ToLower() == WellKnownMethodNames.EntryPoint).ToArray();

        if (mains.Length > 1) {
            foreach (var main in mains) {
                var span = TextSpan.FromBounds(
                    main.declaration.span.start,
                    main.declaration.parameterList.closeParenthesis.span.end
                );

                var location = new TextLocation(main.declaration.syntaxTree.text, span);
                binder.diagnostics.Push(Error.MultipleMains(location));
            }
        }

        entryPoint = mains.Length == 0 ? null : mains[0];

        if (entryPoint != null) {
            if (entryPoint.type.typeSymbol != TypeSymbol.Void && entryPoint.type.typeSymbol != TypeSymbol.Int) {
                binder.diagnostics.Push(
                    Error.InvalidMain((entryPoint.declaration as MethodDeclarationSyntax).returnType.location)
                );
            }

            var argsType = new BoundType(
                binder._scope.LookupSymbol<ClassSymbol>(WellKnownTypeNames.List),
                isNullable: false,
                templateArguments: [new TypeOrConstant(BoundType.String)],
                arity: 1
            );

            if (entryPoint.parameters.Any()) {
                if (entryPoint.parameters.Length != 1 ||
                    entryPoint.parameters[0].name != "args" ||
                    !(entryPoint.parameters[0].type?.Equals(argsType) ?? false)) {
                    var span = TextSpan.FromBounds(
                        entryPoint.declaration.parameterList.openParenthesis.span.start + 1,
                        entryPoint.declaration.parameterList.closeParenthesis.span.end - 1
                    );

                    var location = new TextLocation(entryPoint.declaration.syntaxTree.text, span);
                    binder.diagnostics.Push(Error.InvalidMain(location));
                }
            }
        }

        if (globalStatements.Any()) {
            if (entryPoint != null) {
                binder.diagnostics.Push(Error.MainAndGlobals(GetIdentifierLocation(entryPoint.declaration)));

                foreach (var globalStatement in firstGlobalPerTree)
                    binder.diagnostics.Push(Error.MainAndGlobals(globalStatement.location));
            } else {
                entryPoint = new MethodSymbol("<Main>$", [], [], [], BoundType.Void);
            }
        }

        return entryPoint;
    }

    private static TextLocation GetIdentifierLocation(BaseMethodDeclarationSyntax syntax) {
        return GetIdentifierToken(syntax).location;
    }

    private static SyntaxToken GetIdentifierToken(BaseMethodDeclarationSyntax syntax) {
        if (syntax is ConstructorDeclarationSyntax c)
            return c.constructorKeyword;
        if (syntax is MethodDeclarationSyntax m)
            return m.identifier;
        if (syntax is OperatorDeclarationSyntax o)
            return o.operatorToken;

        throw ExceptionUtilities.Unreachable();
    }

    private static TextLocation GetOperatorTokenLocation(OperatorDeclarationSyntax syntax) {
        if (syntax.rightOperatorToken is null)
            return syntax.operatorToken.location;

        var span = new TextSpan(syntax.operatorToken.span.start, 2);
        return new TextLocation(syntax.location.text, span);
    }

    private static ImmutableArray<string> PeekLocals(
        IEnumerable<StatementSyntax> statements,
        IEnumerable<ParameterSymbol> parameters) {
        var locals = ImmutableArray.CreateBuilder<string>();

        foreach (var statement in statements) {
            if (statement is LocalDeclarationStatementSyntax vd)
                locals.Add(vd.declaration.identifier.text);
        }

        if (parameters != null) {
            foreach (var parameter in parameters)
                locals.Add(parameter.name);
        }

        return locals.ToImmutable();
    }

    private static BoundScope CreateParentScope(BoundGlobalScope previous, ProjectType projectType) {
        var stack = new Stack<BoundGlobalScope>();

        while (previous != null) {
            stack.Push(previous);
            previous = previous.previous;
        }

        var parent = CreateRootScope(projectType);

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

    private static BoundScope CreateRootScope(ProjectType projectType) {
        var result = new BoundScope(null);

        foreach (var method in BuiltinMethods.GetAll())
            result.TryDeclareMethod(method);

        LoadLibraries(result, projectType);

        return result;
    }

    private static void LoadLibraries(BoundScope scope, ProjectType projectType) {
        void DeclareSymbols(IEnumerable<Symbol> symbols) {
            foreach (var symbol in symbols) {
                if (symbol is MethodSymbol m)
                    scope.TryDeclareMethod(m);

                if (symbol is NamedTypeSymbol t) {
                    scope.TryDeclareType(t);
                    DeclareSymbols(t.members);
                }
            }
        }

        StandardLibrary.Load();
        DeclareSymbols(StandardLibrary.GetSymbols());
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

    private TypeSymbol LookupPrimitive(string name) {
        return name switch {
            "any" => TypeSymbol.Any,
            "bool" => TypeSymbol.Bool,
            "int" => TypeSymbol.Int,
            "decimal" => TypeSymbol.Decimal,
            "string" => TypeSymbol.String,
            "char" => TypeSymbol.Char,
            "void" => TypeSymbol.Void,
            "type" => TypeSymbol.Type,
            _ => null,
        };
    }

    private ImmutableArray<TypeSymbol> LookupTypes(string name) {
        var types = _scope.LookupOverloads(name)
            .Where(t => t is TypeSymbol)
            .Select(t => t as TypeSymbol).ToImmutableArray();

        var type = LookupPrimitive(name);

        if (!types.Where(t => t is TypeSymbol).Any() && type != null)
            return [type];

        return types;
    }

    private ImmutableArray<ParameterSymbol> BindParameterList(ParameterListSyntax parameterList) {
        return BindParameters(parameterList.parameters, false);
    }

    private ImmutableArray<ParameterSymbol> BindParameters(
        SeparatedSyntaxList<ParameterSyntax> parameters,
        bool isTemplate) {
        var parametersBuilder = ImmutableArray.CreateBuilder<ParameterSymbol>();
        var seenParameterNames = new HashSet<string>();

        for (var i = 0; i < parameters.Count; i++) {
            var parameter = parameters[i];
            var parameterName = parameter.identifier.text;
            var parameterType = BindType(parameter.type);

            if (isTemplate)
                parameterType = BoundType.CopyWith(parameterType, isConstantExpression: true);

            var boundDefault = parameter.defaultValue is null
                ? null
                : BindExpression(parameter.defaultValue, allowTypes: isTemplate);

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
                if (isTemplate &&
                    _options.buildMode == BuildMode.CSharpTranspile &&
                    !parameterType.Equals(BoundType.Type, isTypeCheck: true)) {
                    diagnostics.Push(Error.Unsupported.NonTypeTemplate(parameter.type.location));
                }

                var boundParameter = new ParameterSymbol(
                    parameterName,
                    parameterType,
                    parametersBuilder.Count + 1,
                    boundDefault,
                    isTemplate: isTemplate
                );

                parametersBuilder.Add(boundParameter);
            }
        }

        return parametersBuilder.ToImmutable();
    }

    private BoundBlockStatement BindMethodBody(
        BaseMethodDeclarationSyntax syntax,
        ImmutableArray<ParameterSymbol> parameters,
        SyntaxToken fallbackLocation) {
        var body = syntax?.body;
        BoundBlockStatement boundBody;

        if (body != null) {
            _peekedLocals = PeekLocals(body.statements, parameters);
            boundBody = BindStatement(body) as BoundBlockStatement;
        } else {
            boundBody = Block();
        }

        if (_containingMethod.name == WellKnownMemberNames.InstanceConstructorName &&
            _containingType is ClassSymbol cs) {
            BoundExpression initializer = new BoundEmptyExpression();

            if (_containingType != _wellKnownTypes[WellKnownTypeNames.Object]) {
                initializer = BindConstructorInitializer(
                    (syntax as ConstructorDeclarationSyntax)?.constructorInitializer,
                    syntax is null ? fallbackLocation : GetIdentifierToken(syntax)
                );
            }

            return Block(
                [new BoundExpressionStatement(initializer)],
                BindDefaultFieldAssignments(cs.defaultFieldAssignments),
                boundBody.statements
            );
        } else {
            return boundBody;
        }
    }

    private ImmutableArray<BoundStatement> BindDefaultFieldAssignments(
        ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments) {
        var boundAssignmentsBuilder = ImmutableArray.CreateBuilder<BoundStatement>();

        foreach (var (field, fieldAssignment) in defaultFieldAssignments) {
            var initializer = BindExpression(fieldAssignment);

            if (initializer != null) {
                boundAssignmentsBuilder.Add(
                    Statement(
                        Assignment(
                            MemberAccess(
                                BindThisExpressionInternal(),
                                new BoundVariableExpression(field)
                            ),
                            initializer
                        )
                    )
                );
            }
        }

        return boundAssignmentsBuilder.ToImmutable();
    }

    private BoundExpression BindConstructorInitializer(
        ConstructorInitializerSyntax syntax,
        SyntaxToken receiverName) {
        BoundExpression receiver = (syntax is null || syntax.thisOrBaseKeyword.kind == SyntaxKind.BaseKeyword)
            ? new BoundBaseExpression((_containingType as ClassSymbol).baseType)
            : new BoundThisExpression(new BoundType(_containingType));

        var constructors = (syntax is null || syntax.thisOrBaseKeyword.kind == SyntaxKind.BaseKeyword)
            ? ((_containingType as ClassSymbol).baseType.typeSymbol as NamedTypeSymbol).constructors
            : _containingType.constructors;

        ImmutableArray<(string, BoundExpression)> arguments;

        if (syntax is null) {
            arguments = [];
        } else {
            if (!PartiallyBindArgumentList(syntax.argumentList, out arguments))
                return new BoundErrorExpression();
        }

        var result = _overloadResolution.MethodOverloadResolution(
            constructors,
            arguments,
            WellKnownMemberNames.InstanceConstructorName,
            syntax?.thisOrBaseKeyword ?? receiverName,
            syntax?.argumentList,
            null,
            receiver.type
        );

        if (!result.succeeded)
            return new BoundErrorExpression();

        if (_options.buildMode != BuildMode.Repl && result.bestOverload.accessibility == Accessibility.Private) {
            if (_containingType is null ||
                _containingType != result.bestOverload.containingType) {
                diagnostics.Push(Error.MemberIsInaccessible(
                    syntax is null ? receiverName.location : syntax.thisOrBaseKeyword.location,
                    $"{receiver.type.typeSymbol.name}.{result.bestOverload.Signature()}",
                    result.bestOverload.containingType.name
                ));
            }
        }

        return new BoundCallExpression(receiver, result.bestOverload, result.arguments, []);
    }

    private bool ModifierAlreadyApplied(
        DeclarationModifiers modifiers,
        DeclarationModifiers modifier,
        SyntaxToken syntax) {
        if ((modifiers & modifier) != 0) {
            diagnostics.Push(Error.ModifierAlreadyApplied(syntax.location, syntax.text));
            return true;
        }

        return false;
    }

    private bool ConflictingAccessibilityModifier(
        DeclarationModifiers modifiers,
        DeclarationModifiers modifier,
        SyntaxToken syntax) {
        if ((modifier == DeclarationModifiers.Public && (modifiers & DeclarationModifiers.Private) != 0) ||
            (modifier == DeclarationModifiers.Private && (modifiers & DeclarationModifiers.Public) != 0)) {
            diagnostics.Push(Error.ConflictingModifiers(syntax.location, "public", "private"));
            return true;
        }

        if ((modifier == DeclarationModifiers.Public && (modifiers & DeclarationModifiers.Protected) != 0) ||
            (modifier == DeclarationModifiers.Protected && (modifiers & DeclarationModifiers.Public) != 0)) {
            diagnostics.Push(Error.ConflictingModifiers(syntax.location, "public", "protected"));
            return true;
        }

        if ((modifier == DeclarationModifiers.Private && (modifiers & DeclarationModifiers.Protected) != 0) ||
            (modifier == DeclarationModifiers.Protected && (modifiers & DeclarationModifiers.Private) != 0)) {
            diagnostics.Push(Error.ConflictingModifiers(syntax.location, "protected", "private"));
            return true;
        }

        return false;
    }

    private bool ConflictingDerivationModifier(
        DeclarationModifiers modifiers,
        DeclarationModifiers modifier,
        SyntaxToken syntax) {
        if ((modifier == DeclarationModifiers.Virtual && (modifiers & DeclarationModifiers.Override) != 0) ||
            (modifier == DeclarationModifiers.Override && (modifiers & DeclarationModifiers.Virtual) != 0) ||
            (modifier == DeclarationModifiers.Override && (modifiers & DeclarationModifiers.New) != 0) ||
            (modifier == DeclarationModifiers.Virtual && (modifiers & DeclarationModifiers.Abstract) != 0) ||
            (modifier == DeclarationModifiers.Abstract && (modifiers & DeclarationModifiers.Virtual) != 0) ||
            (modifier == DeclarationModifiers.Override && (modifiers & DeclarationModifiers.Abstract) != 0) ||
            (modifier == DeclarationModifiers.Abstract && (modifiers & DeclarationModifiers.Override) != 0)) {
            diagnostics.Push(Error.ConflictingOverrideModifiers(syntax.location));
            return true;
        }

        return false;
    }

    private bool ConflictingOverrideModifier(
        DeclarationModifiers modifiers,
        DeclarationModifiers modifier,
        SyntaxToken syntax) {
        if ((modifier == DeclarationModifiers.Sealed && (modifiers & DeclarationModifiers.Static) != 0) ||
            (modifier == DeclarationModifiers.Static && (modifiers & DeclarationModifiers.Sealed) != 0)) {
            diagnostics.Push(Error.ConflictingModifiers(syntax.location, "sealed", "static"));
            return true;
        }

        if ((modifier == DeclarationModifiers.Sealed && (modifiers & DeclarationModifiers.Abstract) != 0) ||
            (modifier == DeclarationModifiers.Abstract && (modifiers & DeclarationModifiers.Sealed) != 0)) {
            diagnostics.Push(Error.ConflictingModifiers(syntax.location, "sealed", "abstract"));
            return true;
        }

        if ((modifier == DeclarationModifiers.Abstract && (modifiers & DeclarationModifiers.Static) != 0) ||
            (modifier == DeclarationModifiers.Static && (modifiers & DeclarationModifiers.Abstract) != 0)) {
            diagnostics.Push(Error.ConflictingModifiers(syntax.location, "abstract", "static"));
            return true;
        }

        return false;
    }

    private MethodSymbol BindMethodDeclaration(
        MethodDeclarationSyntax method,
        DeclarationModifiers inheritedModifiers,
        string name = null,
        List<MethodSymbol> overridableMethods = null,
        string containingTypeName = null) {
        // ? This will return eventually
        BindAttributeLists(method.attributeLists);

        var modifiers = BindMethodDeclarationModifiers(method.modifiers);
        // If name is not null that means we are binding a local function
        // in which case accessibility is not applicable
        var accessibility = name is null ? BindAccessibilityFromModifiers(modifiers) : Accessibility.NotApplicable;

        if ((modifiers & (DeclarationModifiers.Virtual | DeclarationModifiers.Abstract)) != 0 &&
            accessibility == Accessibility.Private) {
            diagnostics.Push(Error.CannotBePrivateAndVirtualOrAbstract(method.identifier.location));
        }

        var saved = _flags;

        if ((modifiers & DeclarationModifiers.LowLevel) != 0)
            _flags |= BinderFlags.LowLevelContext;

        name ??= method.identifier.text;

        // Temporary scope so constraint clauses can see templates
        _scope = new BoundScope(_scope);

        var templates = BindTemplateParameters(method.templateParameterList);
        var templateTypes = new Dictionary<string, TemplateParameterSymbol>();

        foreach (var template in templates) {
            if (template.type.typeSymbol == TypeSymbol.Type) {
                var templateType = new TemplateParameterSymbol(template);
                _scope.TryDeclareType(templateType);
                templateTypes.Add(templateType.name, templateType);
            } else {
                _scope.TryDeclareVariable(template);
            }
        }

        var type = BindType(method.returnType, modifiers, true);

        if (type?.typeSymbol?.isStatic ?? false)
            diagnostics.Push(Error.CannotReturnStatic(method.returnType.location));

        var constraints = BindConstraintClauseList(name, method.constraintClauseList, templates);
        ApplyExtensionConstraints(constraints, templateTypes);

        var parameters = BindParameterList(method.parameterList);

        _scope = _scope.parent;

        var newMethod = new MethodSymbol(
            name,
            templates,
            constraints,
            parameters,
            type,
            method,
            modifiers: modifiers | inheritedModifiers,
            accessibility: accessibility
        );

        var parent = method.parent;
        var className = (parent is ClassDeclarationSyntax c) ? c.identifier.text : null;

        if ((newMethod.declaration as MethodDeclarationSyntax).identifier.text != null) {
            MethodSymbol overrideTarget = null;

            if (overridableMethods is not null) {
                foreach (var target in overridableMethods) {
                    if (target is MethodSymbol m) {
                        if (MethodUtilities.MethodsMatch(newMethod, m)) {
                            overrideTarget = m;
                            overridableMethods.Remove(target);
                            break;
                        }
                    }
                }
            }

            if (newMethod.isOverride) {
                if (overrideTarget is null || !_scope.TryReplaceSymbol(overrideTarget, newMethod)) {
                    diagnostics.Push(Error.NoSuitableOverrideTarget(method.identifier.location));
                } else if (overrideTarget.accessibility != newMethod.accessibility) {
                    diagnostics.Push(Error.OverrideCannotChangeAccessibility(
                        method.identifier.location,
                        overrideTarget.accessibility.ToString().ToLower(),
                        accessibility.ToString().ToLower()
                    ));
                } else if (!overrideTarget.isVirtual && !overrideTarget.isAbstract && !overrideTarget.isOverride) {
                    diagnostics.Push(Error.CannotOverride(method.identifier.location, newMethod.Signature()));
                }
            } else {
                if (overrideTarget is not null && (modifiers & DeclarationModifiers.New) == 0) {
                    diagnostics.Push(Error.MemberShadowsParent(
                        method.identifier.location,
                        newMethod.Signature(),
                        containingTypeName,
                        overrideTarget.Signature(),
                        overrideTarget.containingType.name
                    ));
                } else if (overrideTarget is null && (modifiers & DeclarationModifiers.New) != 0) {
                    diagnostics.Push(Warning.MemberShadowsNothing(
                        method.identifier.location,
                        newMethod.Signature(),
                        containingTypeName
                    ));
                }
            }

            if (method.body is null && !newMethod.isAbstract)
                diagnostics.Push(Error.NonAbstractMustHaveBody(method.identifier.location, newMethod.Signature()));

            if (method.body is not null && newMethod.isAbstract)
                diagnostics.Push(Error.AbstractCannotHaveBody(method.identifier.location, newMethod.Signature()));

            if (!_scope.TryDeclareMethod(newMethod)) {
                diagnostics.Push(
                    Error.MethodAlreadyDeclared(method.identifier.location, newMethod.Signature(), className)
                );
            }
        }

        _flags = saved;
        return newMethod;
    }

    private DeclarationModifiers BindMethodDeclarationModifiers(SyntaxTokenList modifiers) {
        var declarationModifiers = DeclarationModifiers.None;

        if (modifiers is null)
            return declarationModifiers;

        foreach (var modifier in modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.StaticKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Static, modifier))
                        break;

                    if ((declarationModifiers & DeclarationModifiers.Const) != 0) {
                        diagnostics.Push(Error.ConflictingModifiers(modifier.location, "static", "constant"));
                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Static;
                    break;
                case SyntaxKind.ConstKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Const, modifier))
                        break;

                    if ((declarationModifiers & DeclarationModifiers.Static) != 0) {
                        diagnostics.Push(Error.ConflictingModifiers(modifier.location, "static", "constant"));
                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Const;
                    break;
                case SyntaxKind.LowlevelKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.LowLevel, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.LowLevel;
                    break;
                case SyntaxKind.PublicKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Public;
                    break;
                case SyntaxKind.PrivateKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Private;
                    break;
                case SyntaxKind.ProtectedKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Protected, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(
                        declarationModifiers,
                        DeclarationModifiers.Protected,
                        modifier)) {
                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Protected;
                    break;
                case SyntaxKind.VirtualKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Virtual, modifier))
                        break;

                    ConflictingDerivationModifier(declarationModifiers, DeclarationModifiers.Virtual, modifier);
                    declarationModifiers |= DeclarationModifiers.Virtual;
                    break;
                case SyntaxKind.OverrideKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Override, modifier))
                        break;

                    ConflictingDerivationModifier(declarationModifiers, DeclarationModifiers.Override, modifier);
                    declarationModifiers |= DeclarationModifiers.Override;
                    break;
                case SyntaxKind.NewKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.New, modifier))
                        break;

                    ConflictingDerivationModifier(declarationModifiers, DeclarationModifiers.New, modifier);
                    declarationModifiers |= DeclarationModifiers.New;
                    break;
                case SyntaxKind.AbstractKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Abstract, modifier))
                        break;

                    ConflictingDerivationModifier(declarationModifiers, DeclarationModifiers.Abstract, modifier);
                    declarationModifiers |= DeclarationModifiers.Abstract;
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));
                    break;
            }
        }

        return declarationModifiers;
    }

    private MethodSymbol BindConstructorDeclaration(
        ConstructorDeclarationSyntax constructor,
        DeclarationModifiers inheritedModifiers) {
        // ? This will return eventually
        BindAttributeLists(constructor.attributeLists);

        var modifiers = BindConstructorDeclarationModifiers(constructor.modifiers);
        var accessibility = BindAccessibilityFromModifiers(modifiers);
        var parameters = BindParameterList(constructor.parameterList);
        var method = new MethodSymbol(
            WellKnownMemberNames.InstanceConstructorName,
            [],
            [],
            parameters,
            BoundType.Void,
            constructor,
            modifiers: modifiers | inheritedModifiers,
            accessibility: accessibility
        );

        var parent = constructor.parent as ClassDeclarationSyntax;
        var className = parent.identifier.text;

        if (!_scope.TryDeclareMethod(method)) {
            diagnostics.Push(Error.MethodAlreadyDeclared(
                constructor.constructorKeyword.location,
                method.Signature(),
                className
            ));
        }

        return method;
    }

    private DeclarationModifiers BindConstructorDeclarationModifiers(SyntaxTokenList modifiers) {
        var declarationModifiers = DeclarationModifiers.None;

        if (modifiers is null)
            return declarationModifiers;

        foreach (var modifier in modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.LowlevelKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.LowLevel, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.LowLevel;
                    break;
                case SyntaxKind.PublicKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Public;
                    break;
                case SyntaxKind.PrivateKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Private;
                    break;
                case SyntaxKind.ProtectedKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Protected, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(
                        declarationModifiers,
                        DeclarationModifiers.Protected,
                        modifier)) {
                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Protected;
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));
                    break;
            }
        }

        return declarationModifiers;
    }

    private MethodSymbol BindOperatorDeclaration(
        OperatorDeclarationSyntax @operator,
        DeclarationModifiers inheritedModifiers) {
        // ? This will return eventually
        BindAttributeLists(@operator.attributeLists);

        var modifiers = BindOperatorDeclarationModifiers(@operator.modifiers);
        var accessibility = BindAccessibilityFromModifiers(modifiers);
        var type = BindType(@operator.returnType, modifiers, true);
        var parameters = BindParameterList(@operator.parameterList);
        var name = SyntaxFacts.GetOperatorMemberName(@operator.operatorToken.kind, parameters.Length);

        var expectedArity = SyntaxFacts.GetOperatorArity(name);

        if (expectedArity != parameters.Length) {
            diagnostics.Push(Error.IncorrectOperatorParameterCount(
                GetOperatorTokenLocation(@operator),
                @operator.rightOperatorToken is null
                    ? @operator.operatorToken.text
                    : @operator.operatorToken.text + @operator.rightOperatorToken.text,
                expectedArity
            ));
        }

        if ((modifiers & DeclarationModifiers.Static) == 0 || accessibility != Accessibility.Public)
            diagnostics.Push(Error.OperatorMustBePublicAndStatic(GetOperatorTokenLocation(@operator)));

        var parent = @operator.parent as ClassDeclarationSyntax;
        var className = parent.identifier.text;

        var atLeastOneClassParameter = false;

        foreach (var parameter in parameters) {
            if (parameter.type?.typeSymbol?.name == className) {
                atLeastOneClassParameter = true;
                break;
            }
        }

        if (!atLeastOneClassParameter)
            diagnostics.Push(Error.OperatorAtLeastOneClassParameter(GetOperatorTokenLocation(@operator)));

        if ((name == WellKnownMemberNames.IncrementOperatorName || name == WellKnownMemberNames.DecrementOperatorName)
            && type.typeSymbol.name != className) {
            diagnostics.Push(Error.OperatorMustReturnClass(GetOperatorTokenLocation(@operator)));
        }

        if ((name == WellKnownMemberNames.IndexOperatorName || name == WellKnownMemberNames.IndexAssignName) &&
            parameters.Length > 0 &&
            parameters[0].type.typeSymbol.name != className) {
            diagnostics.Push(Error.IndexOperatorFirstParameter(GetOperatorTokenLocation(@operator)));
        }

        var method = new MethodSymbol(
            name,
            [],
            [],
            parameters,
            type,
            @operator,
            modifiers: modifiers | inheritedModifiers,
            accessibility: accessibility
        );

        if ((method.declaration as OperatorDeclarationSyntax).operatorToken.text != null &&
            !_scope.TryDeclareMethod(method)) {
            diagnostics.Push(
                Error.MethodAlreadyDeclared(GetOperatorTokenLocation(@operator), method.Signature(), className)
            );
        }

        return method;
    }

    private DeclarationModifiers BindOperatorDeclarationModifiers(SyntaxTokenList modifiers) {
        var declarationModifiers = DeclarationModifiers.None;

        if (modifiers is null)
            return declarationModifiers;

        foreach (var modifier in modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.StaticKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Static, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Static;
                    break;
                case SyntaxKind.LowlevelKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.LowLevel, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.LowLevel;
                    break;
                case SyntaxKind.PublicKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Public;
                    break;
                case SyntaxKind.PrivateKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Private;
                    break;
                case SyntaxKind.ProtectedKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Protected, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(
                        declarationModifiers,
                        DeclarationModifiers.Protected,
                        modifier)) {
                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Protected;
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));
                    break;
            }
        }

        return declarationModifiers;
    }

    private NamedTypeSymbol PreBindTypeDeclaration(
        TypeDeclarationSyntax type,
        DeclarationModifiers inheritedModifiers) {
        // Binds everything about the type except the members and the constraints
        NamedTypeSymbol symbol;
        var templates = BindTemplateParameters(type.templateParameterList);

        if (type is StructDeclarationSyntax s) {
            var modifiers = BindStructDeclarationModifiers(s.modifiers);
            var accessibility = BindAccessibilityFromModifiers(modifiers);

            symbol = new StructSymbol(
                    templates,
                    [],
                    [],
                    s,
                    modifiers | inheritedModifiers,
                    accessibility
                );
        } else if (type is ClassDeclarationSyntax c) {
            var modifiers = BindClassDeclarationModifiers(c.modifiers);
            var accessibility = BindAccessibilityFromModifiers(modifiers);

            symbol = new ClassSymbol(
                    templates,
                    [],
                    [],
                    [],
                    c,
                    modifiers | inheritedModifiers,
                    accessibility,
                    null
                );
        } else {
            throw new BelteInternalException($"BindTypeDeclaration: unexpected type '{type.identifier.text}'");
        }

        _scope.TryDeclareType(symbol);
        return symbol;
    }

    private TypeSymbol BindTypeDeclaration(TypeDeclarationSyntax @type) {
        if (@type is StructDeclarationSyntax s)
            return BindStructDeclaration(s);
        else if (@type is ClassDeclarationSyntax c)
            return BindClassDeclaration(c);
        else
            throw new BelteInternalException($"BindTypeDeclaration: unexpected type '{@type.identifier.text}'");
    }

    private Accessibility BindAccessibilityFromModifiers(DeclarationModifiers modifiers) {
        if (!_flags.Includes(BinderFlags.Class))
            return Accessibility.NotApplicable;

        if ((modifiers & DeclarationModifiers.Public) != 0)
            return Accessibility.Public;
        else if ((modifiers & DeclarationModifiers.Protected) != 0)
            return Accessibility.Protected;

        return Accessibility.Private;
    }

    private BoundType BindBaseType(BaseTypeSyntax syntax) {
        var type = BindType(syntax.type);

        if (type?.typeSymbol is PrimitiveTypeSymbol) {
            diagnostics.Push(Error.CannotDerivePrimitive(syntax.type.location, type.typeSymbol.ToString()));
            return new BoundType(_wellKnownTypes[WellKnownTypeNames.Object]);
        }

        return type ?? new BoundType(_wellKnownTypes[WellKnownTypeNames.Object]);
    }

    private ImmutableArray<ParameterSymbol> BindTemplateParameters(TemplateParameterListSyntax templateParameterList) {
        var builder = ImmutableList.CreateBuilder<ParameterSymbol>();

        if (templateParameterList != null) {
            var templateParameters = BindParameters(templateParameterList.parameters, true);

            foreach (var templateParameter in templateParameters)
                builder.Add(templateParameter);
        }

        return builder.ToImmutableArray();
    }

    private ImmutableArray<BoundExpression> BindConstraintClauseList(
        string memberName,
        TemplateParameterConstraintClauseListSyntax constraintClauseList,
        ImmutableArray<ParameterSymbol> templates) {
        // TODO Should the clauses be able to see globals? e.g. constexpr GlobalMin
        var constraintsBuilder = ImmutableList.CreateBuilder<BoundExpression>();

        if (constraintClauseList != null) {
            foreach (var constraintClause in constraintClauseList.constraintClauses) {
                if (constraintClause.expressionStatement is null) {
                    var name = constraintClause.name.identifier.text;
                    var possibleTemplate = templates
                        .Where(t => t.name == name)
                        .ToArray();

                    if (possibleTemplate.Length != 1) {
                        diagnostics.Push(
                            Error.UnknownTemplate(constraintClause.name.location, memberName, name)
                        );

                        continue;
                    }

                    var template = possibleTemplate[0];

                    if (template.type.typeSymbol != TypeSymbol.Type) {
                        diagnostics.Push(Error.CannotExtendCheckNonType(constraintClause.location, template.name));
                        continue;
                    }

                    var extension = BindType(constraintClause.type);
                    var constraint = new BoundExtendExpression(template, extension);
                    constraintsBuilder.Add(constraint);
                } else {
                    _scope = new BoundScope(_scope);

                    var expressionStatement =
                        (BoundExpressionStatement)BindExpressionStatement(constraintClause.expressionStatement);

                    _scope = _scope.parent;

                    var expression = BindCast(
                        constraintClause.expressionStatement.expression.location,
                        expressionStatement.expression,
                        BoundType.NullableBool
                    );

                    if (!IsCompilerComputable(expression, templates)) {
                        diagnostics.Push(Error.ConstraintIsNotConstant(constraintClause.location));
                        continue;
                    }

                    constraintsBuilder.Add(expression);
                }
            }
        }

        return constraintsBuilder.ToImmutableArray();
    }

    private void ApplyExtensionConstraints(
        ImmutableArray<BoundExpression> constraints,
        Dictionary<string, TemplateParameterSymbol> templates) {
        foreach (var constraint in constraints) {
            if (constraint is BoundExtendExpression e)
                templates[e.template.name].AddBaseType(e.extension);
        }
    }

    private StructSymbol BindStructDeclaration(StructDeclarationSyntax @struct) {
        // ? This will return eventually
        BindAttributeLists(@struct.attributeLists);

        if (_scope.LookupSymbolDirect(@struct) is not StructSymbol oldStruct) {
            diagnostics.Push(Error.TypeAlreadyDeclared(@struct.identifier.location, @struct.identifier.text, false));

            return new StructSymbol(
                [],
                [],
                [],
                @struct,
                DeclarationModifiers.None,
                Accessibility.NotApplicable
            );
        }

        var builder = ImmutableList.CreateBuilder<Symbol>();
        _scope = new BoundScope(_scope);

        var saved = _flags;
        _flags |= BinderFlags.Struct;

        if (oldStruct.isLowLevel)
            _flags |= BinderFlags.LowLevelContext;

        var templates = ImmutableArray.CreateBuilder<ParameterSymbol>();
        var templateTypes = new Dictionary<string, TemplateParameterSymbol>();

        foreach (var templateParameter in oldStruct.templateParameters) {
            if (templateParameter.type.typeSymbol == TypeSymbol.Type) {
                var templateType = new TemplateParameterSymbol(templateParameter);
                builder.Add(templateType);
                _scope.TryDeclareType(templateType);
                templateTypes.Add(templateType.name, templateType);
            } else {
                builder.Add(templateParameter);
                _scope.TryDeclareVariable(templateParameter);
            }

            templates.Add(templateParameter);
        }

        var constraints = BindConstraintClauseList(
            oldStruct.name,
            @struct.constraintClauseList,
            templates.ToImmutable()
        );

        ApplyExtensionConstraints(constraints, templateTypes);

        foreach (var fieldDeclaration in @struct.members.OfType<FieldDeclarationSyntax>()) {
            var field = BindFieldDeclaration(fieldDeclaration, true);
            builder.Add(field);
        }

        var newStruct = new StructSymbol(
            oldStruct.templateParameters,
            constraints,
            builder.ToImmutableArray(),
            @struct,
            oldStruct.isLowLevel ? DeclarationModifiers.LowLevel : DeclarationModifiers.None,
            oldStruct.accessibility
        );

        _scope = _scope.parent;

        if (oldStruct.members.Length == 0)
            _scope.TryReplaceSymbol(oldStruct, newStruct);
        else if (!_scope.TryDeclareType(newStruct))
            diagnostics.Push(Error.TypeAlreadyDeclared(@struct.identifier.location, @struct.identifier.text, false));

        if (!_flags.Includes(BinderFlags.LowLevelContext))
            diagnostics.Push(Error.CannotUseStruct(@struct.keyword.location));

        _flags = saved;

        return oldStruct;
    }

    private DeclarationModifiers BindStructDeclarationModifiers(SyntaxTokenList modifiers) {
        var declarationModifiers = DeclarationModifiers.None;

        if (modifiers is null)
            return declarationModifiers;

        foreach (var modifier in modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.LowlevelKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.LowLevel, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.LowLevel;
                    break;
                case SyntaxKind.PublicKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Public;
                    break;
                case SyntaxKind.PrivateKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Private;
                    break;
                case SyntaxKind.ProtectedKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Protected, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(
                        declarationModifiers,
                        DeclarationModifiers.Protected,
                        modifier)) {
                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Protected;
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));
                    break;
            }
        }

        return declarationModifiers;
    }

    private void BindAttributeLists(SyntaxList<AttributeListSyntax> attributeLists) {
        if (attributeLists is null)
            return;

        foreach (var attributeList in attributeLists) {
            foreach (var attribute in attributeList.attributes) {
                diagnostics.Push(Error.UnknownAttribute(attribute.location, attribute.identifier.text));
            }
        }
    }

    private ClassSymbol BindClassDeclaration(ClassDeclarationSyntax @class) {
        // ? This will return eventually
        BindAttributeLists(@class.attributeLists);

        if (_scope.LookupSymbolDirect(@class) is not ClassSymbol oldClass) {
            diagnostics.Push(Error.TypeAlreadyDeclared(@class.identifier.location, @class.identifier.text, true));

            return new ClassSymbol(
                [],
                [],
                [],
                [],
                @class,
                DeclarationModifiers.None,
                Accessibility.NotApplicable,
                null
            );
        }

        var builder = ImmutableList.CreateBuilder<Symbol>();
        var inheritedMethods = new List<MethodSymbol>();
        var isStatic = oldClass.isStatic;
        _scope = new BoundScope(_scope);

        var saved = _flags;
        var inheritModifiers = DeclarationModifiers.None;
        _flags |= BinderFlags.Class;

        if (oldClass.isLowLevel) {
            _flags |= BinderFlags.LowLevelContext;
            inheritModifiers = DeclarationModifiers.LowLevel;
        }

        var templates = ImmutableArray.CreateBuilder<ParameterSymbol>();
        var templateTypes = new Dictionary<string, TemplateParameterSymbol>();

        foreach (var templateParameter in oldClass.templateParameters) {
            if (templateParameter.type.typeSymbol == TypeSymbol.Type) {
                var templateType = new TemplateParameterSymbol(templateParameter);
                builder.Add(templateType);
                _scope.TryDeclareType(templateType);
                templateTypes.Add(templateType.name, templateType);
            } else {
                builder.Add(templateParameter);
                _scope.TryDeclareVariable(templateParameter);
            }

            templates.Add(templateParameter);
        }

        var constraints = BindConstraintClauseList(oldClass.name, @class.constraintClauseList, templates.ToImmutable());
        ApplyExtensionConstraints(constraints, templateTypes);

        BoundType baseType = null;

        if (!oldClass.isStatic) {
            baseType = @class.baseType is null
                ? new BoundType(_wellKnownTypes[WellKnownTypeNames.Object])
                : BindBaseType(@class.baseType);

            if (baseType.typeSymbol.isSealed)
                diagnostics.Push(Error.CannotDeriveSealed(@class.baseType.type.location, baseType.typeSymbol.name));

            if (baseType.typeSymbol.isStatic)
                diagnostics.Push(Error.CannotDeriveStatic(@class.baseType.type.location, baseType.typeSymbol.name));

            foreach (var member in (baseType.typeSymbol as ClassSymbol).GetMembersPublic()) {
                switch (member.kind) {
                    case SymbolKind.Field:
                        _scope.TryDeclareVariable(member as FieldSymbol);
                        break;
                    case SymbolKind.Type:
                        _scope.TryDeclareType(member as NamedTypeSymbol);
                        break;
                    case SymbolKind.Method when member.name != WellKnownMemberNames.InstanceConstructorName:
                        _scope.TryDeclareMethod(member as MethodSymbol);
                        inheritedMethods.Add(member as MethodSymbol);
                        break;
                    default:
                        continue;
                }
            }
        }

        if (@class.members.Count == 0) {
            var defaultConstructor = new MethodSymbol(
                WellKnownMemberNames.InstanceConstructorName,
                [],
                [],
                [],
                BoundType.Void,
                modifiers: inheritModifiers,
                accessibility: Accessibility.Public
            );

            builder.Add(defaultConstructor);
            _scope.TryDeclareMethod(defaultConstructor);
            _scope = _scope.parent;
            _scope.DeclareMethodStrict(defaultConstructor);

            EnsureAbstractsAreImplemented();

            var emptyClass = new ClassSymbol(
                oldClass.templateParameters,
                constraints,
                builder.ToImmutableArray(),
                [],
                @class,
                inheritModifiers,
                oldClass.accessibility,
                baseType
            );

            DeclareNewClass(emptyClass);
            _flags = saved;

            return oldClass;
        }

        _scope = new BoundScope(_scope);

        foreach (var member in @class.members.OfType<TypeDeclarationSyntax>())
            PreBindTypeDeclaration(member, inheritModifiers);

        var defaultFieldAssignmentsBuilder = ImmutableArray.CreateBuilder<(FieldSymbol, ExpressionSyntax)>();

        foreach (var fieldDeclaration in @class.members.OfType<FieldDeclarationSyntax>()) {
            var field = BindFieldDeclaration(fieldDeclaration, false);

            if (isStatic && !field.isStatic) {
                diagnostics.Push(Error.MemberMustBeStatic(fieldDeclaration.declaration.identifier.location));
            } else {
                builder.Add(field);

                if (field.type is not null && !field.isConstant && fieldDeclaration.declaration.initializer != null)
                    defaultFieldAssignmentsBuilder.Add((field, fieldDeclaration.declaration.initializer.value));
            }
        }

        var defaultFieldAssignments = defaultFieldAssignmentsBuilder.ToImmutable();
        var hasConstructor = false;

        foreach (var constructorDeclaration in @class.members.OfType<ConstructorDeclarationSyntax>()) {
            var constructor = BindConstructorDeclaration(constructorDeclaration, inheritModifiers);

            if (isStatic) {
                diagnostics.Push(Error.StaticConstructor(constructorDeclaration.constructorKeyword.location));
            } else {
                builder.Add(constructor);
                hasConstructor = true;
            }
        }

        if (!hasConstructor && !isStatic) {
            var defaultConstructor = new MethodSymbol(
                WellKnownMemberNames.InstanceConstructorName,
                [],
                [],
                [],
                BoundType.Void,
                modifiers: inheritModifiers,
                accessibility: Accessibility.Public
            );

            builder.Add(defaultConstructor);
            // This should never fail
            _scope.TryDeclareMethod(defaultConstructor);
        }

        foreach (var methodDeclaration in @class.members.OfType<MethodDeclarationSyntax>()) {
            var method = BindMethodDeclaration(
                methodDeclaration,
                inheritModifiers,
                overridableMethods: inheritedMethods,
                containingTypeName: oldClass.name
            );

            if (isStatic && !method.isStatic)
                diagnostics.Push(Error.MemberMustBeStatic(methodDeclaration.identifier.location));

            if (method.isAbstract && !oldClass.isAbstract) {
                diagnostics.Push(
                    Error.AbstractMemberInNonAbstractType(methodDeclaration.identifier.location, method.Signature())
                );
            }

            builder.Add(method);
        }

        var operators = new List<(OperatorDeclarationSyntax, MethodSymbol)>();

        foreach (var operatorDeclaration in @class.members.OfType<OperatorDeclarationSyntax>()) {
            var @operator = BindOperatorDeclaration(operatorDeclaration, inheritModifiers);

            if (isStatic)
                diagnostics.Push(Error.StaticOperator(operatorDeclaration.operatorToken.location));
            else
                builder.Add(@operator);

            operators.Add((operatorDeclaration, @operator));
        }

        EnsurePairedOperatorsAreDeclared();

        foreach (var typeDeclaration in @class.members.OfType<TypeDeclarationSyntax>()) {
            var type = BindTypeDeclaration(typeDeclaration);
            builder.Add(type);
        }

        EnsureAbstractsAreImplemented();

        var newClass = new ClassSymbol(
            oldClass.templateParameters,
            constraints,
            builder.ToImmutableArray(),
            defaultFieldAssignments,
            @class,
            inheritModifiers,
            oldClass.accessibility,
            baseType
        );

        // This allows the methods to be seen by the global scope
        foreach (var method in _scope.GetDeclaredMethods().Where(m => m.containingType == newClass))
            _scope.parent.parent.DeclareMethodStrict(method);

        _scope = _scope.parent.parent;
        DeclareNewClass(newClass);
        _flags = saved;

        return oldClass;

        void EnsurePairedOperatorsAreDeclared() {
            // TODO This is faster than doing multiple List.Contains, but there should be a faster way to do this
            TextLocation hasEquality = null;
            TextLocation hasInequality = null;
            TextLocation hasLessThan = null;
            TextLocation hasGreaterThan = null;
            TextLocation hasLessThanEqual = null;
            TextLocation hasGreaterThanEqual = null;

            foreach (var (operatorDeclaration, @operator) in operators) {
                switch (@operator.name) {
                    case WellKnownMemberNames.EqualityOperatorName:
                        hasEquality = operatorDeclaration.operatorToken.location;
                        break;
                    case WellKnownMemberNames.InequalityOperatorName:
                        hasInequality = operatorDeclaration.operatorToken.location;
                        break;
                    case WellKnownMemberNames.LessThanOperatorName:
                        hasLessThan = operatorDeclaration.operatorToken.location;
                        break;
                    case WellKnownMemberNames.GreaterThanOperatorName:
                        hasGreaterThan = operatorDeclaration.operatorToken.location;
                        break;
                    case WellKnownMemberNames.LessThanOrEqualOperatorName:
                        hasLessThanEqual = operatorDeclaration.operatorToken.location;
                        break;
                    case WellKnownMemberNames.GreaterThanOrEqualOperatorName:
                        hasGreaterThanEqual = operatorDeclaration.operatorToken.location;
                        break;
                    default:
                        break;
                }
            }

            if (hasEquality is not null && hasInequality is null) {
                diagnostics.Push(Error.MissingOperatorPair(
                    hasEquality,
                    SyntaxKind.EqualsEqualsToken,
                    SyntaxKind.ExclamationEqualsToken
                ));
            } else if (hasEquality is null && hasInequality is not null) {
                diagnostics.Push(Error.MissingOperatorPair(
                    hasInequality,
                    SyntaxKind.ExclamationEqualsToken,
                    SyntaxKind.EqualsEqualsToken
                ));
            }

            if (hasLessThan is not null && hasGreaterThan is null) {
                diagnostics.Push(Error.MissingOperatorPair(
                    hasLessThan,
                    SyntaxKind.LessThanToken,
                    SyntaxKind.GreaterThanToken
                ));
            } else if (hasLessThan is null && hasGreaterThan is not null) {
                diagnostics.Push(Error.MissingOperatorPair(
                    hasGreaterThan,
                    SyntaxKind.GreaterThanToken,
                    SyntaxKind.LessThanToken
                ));
            }

            if (hasLessThanEqual is not null && hasGreaterThanEqual is null) {
                diagnostics.Push(Error.MissingOperatorPair(
                    hasLessThanEqual,
                    SyntaxKind.LessThanEqualsToken,
                    SyntaxKind.GreaterThanEqualsToken
                ));
            } else if (hasLessThanEqual is null && hasGreaterThanEqual is not null) {
                diagnostics.Push(Error.MissingOperatorPair(
                    hasGreaterThanEqual,
                    SyntaxKind.GreaterThanEqualsToken,
                    SyntaxKind.LessThanEqualsToken
                ));
            }
        }

        void EnsureAbstractsAreImplemented() {
            foreach (var method in inheritedMethods) {
                if (method.isAbstract) {
                    diagnostics.Push(Error.TypeDoesNotImplementAbstract(
                        @class.identifier.location,
                        oldClass.name,
                        method.Signature(),
                        method.containingType.name
                    ));
                }
            }
        }

        void DeclareNewClass(ClassSymbol replacingClass) {
            // If no members, the default .ctor has yet to be built by the compiler, meaning this instance is a temporary
            // symbol that needs to be replaced
            if (oldClass.members.Length == 0)
                _scope.TryReplaceSymbol(oldClass, replacingClass);
            else if (!_scope.TryDeclareType(replacingClass))
                diagnostics.Push(Error.TypeAlreadyDeclared(@class.identifier.location, @class.identifier.text, true));
        }
    }

    private DeclarationModifiers BindClassDeclarationModifiers(SyntaxTokenList modifiers) {
        var declarationModifiers = DeclarationModifiers.None;

        if (modifiers is null)
            return declarationModifiers;

        foreach (var modifier in modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.SealedKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Sealed, modifier))
                        break;

                    if (ConflictingOverrideModifier(declarationModifiers, DeclarationModifiers.Sealed, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Sealed;
                    break;
                case SyntaxKind.AbstractKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Abstract, modifier))
                        break;

                    if (ConflictingOverrideModifier(declarationModifiers, DeclarationModifiers.Abstract, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Abstract;
                    break;
                case SyntaxKind.StaticKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Static, modifier))
                        break;

                    if (ConflictingOverrideModifier(declarationModifiers, DeclarationModifiers.Static, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Static;
                    break;
                case SyntaxKind.LowlevelKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.LowLevel, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.LowLevel;
                    break;
                case SyntaxKind.PublicKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Public;
                    break;
                case SyntaxKind.PrivateKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Private;
                    break;
                case SyntaxKind.ProtectedKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Protected, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(
                        declarationModifiers,
                        DeclarationModifiers.Protected,
                        modifier)) {
                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Protected;
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));
                    break;
            }
        }

        return declarationModifiers;
    }

    private BoundStatement BindLocalFunctionDeclaration(LocalFunctionStatementSyntax statement) {
        // ? This will return eventually
        BindAttributeLists(statement.attributeLists);
        BindLocalFunctionDeclarationModifiers(statement.modifiers);

        _innerPrefix.Push(statement.identifier.text);
        var functionSymbol = (MethodSymbol)_scope.LookupSymbol(ConstructInnerName());
        _innerPrefix.Pop();

        var binder = new Binder(_options, _flags | BinderFlags.LocalFunction, _scope, functionSymbol, _wellKnownTypes) {
            _innerPrefix = new Stack<string>(_innerPrefix.Reverse()),
            _trackedSymbols = _trackedSymbols,
            _trackedDeclarations = _trackedDeclarations
        };

        binder._trackedSymbols.Push(new HashSet<VariableSymbol>());
        binder._trackedDeclarations.Push(new HashSet<VariableSymbol>());
        binder._innerPrefix.Push(functionSymbol.name);
        var body = binder.BindMethodBody(
            functionSymbol.declaration,
            functionSymbol.parameters,
            null
        );

        var usedVariables = binder._trackedSymbols.Pop();
        var declaredVariables = binder._trackedDeclarations.Pop();
        var ordinal = functionSymbol.parameters.Length;
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

        var newFunctionSymbol = parametersChanged
            ? functionSymbol.UpdateParameters(parameters.ToImmutable())
            : functionSymbol;

        var (flattenedBody, loweredBody) = Lowerer.Lower(
            newFunctionSymbol,
            body,
            _options.isTranspiling
        );

        if (newFunctionSymbol.type.typeSymbol != TypeSymbol.Void && !ControlFlowGraph.AllPathsReturn(flattenedBody))
            diagnostics.Push(Error.NotAllPathsReturn(GetIdentifierLocation(newFunctionSymbol.declaration)));

        _methodBodies.Add((newFunctionSymbol, loweredBody));
        diagnostics.Move(binder.diagnostics);
        _methodBodies.AddRange(binder._methodBodies);
        _usedLibraryTypes.UnionWith(binder._usedLibraryTypes);

        if (!_scope.TryReplaceSymbol(functionSymbol, newFunctionSymbol))
            throw new BelteInternalException($"BindLocalFunction: failed to set function '{functionSymbol.name}'");

        return new BoundBlockStatement([]);
    }

    private DeclarationModifiers BindLocalFunctionDeclarationModifiers(SyntaxTokenList modifiers) {
        if (modifiers is null)
            return DeclarationModifiers.None;

        foreach (var modifier in modifiers)
            diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));

        return DeclarationModifiers.None;
    }

    private FieldSymbol BindFieldDeclaration(FieldDeclarationSyntax fieldDeclaration, bool isStructField) {
        // ? This will return eventually
        BindAttributeLists(fieldDeclaration.attributeLists);

        var modifiers = BindFieldDeclarationModifiers(fieldDeclaration.modifiers);
        return BindField(fieldDeclaration.declaration, modifiers, isStructField);
    }

    private DeclarationModifiers BindFieldDeclarationModifiers(SyntaxTokenList modifiers) {
        var declarationModifiers = DeclarationModifiers.None;

        if (modifiers is null)
            return declarationModifiers;

        foreach (var modifier in modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.ConstKeyword:
                    if (_flags.Includes(BinderFlags.Struct))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Const, modifier))
                        break;

                    if ((declarationModifiers & DeclarationModifiers.ConstExpr) != 0) {
                        diagnostics.Push(
                            Error.ConflictingModifiers(modifier.location, "constant", "constant expression")
                        );

                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Const;
                    break;
                case SyntaxKind.ConstexprKeyword:
                    if (_flags.Includes(BinderFlags.Struct))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.ConstExpr, modifier))
                        break;

                    if ((declarationModifiers & DeclarationModifiers.Const) != 0) {
                        diagnostics.Push(
                            Error.ConflictingModifiers(modifier.location, "constant", "constant expression")
                        );

                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.ConstExpr;
                    break;
                case SyntaxKind.PublicKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Public, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Public;
                    break;
                case SyntaxKind.PrivateKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(declarationModifiers, DeclarationModifiers.Private, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.Private;
                    break;
                case SyntaxKind.ProtectedKeyword:
                    if (!_flags.Includes(BinderFlags.Class))
                        goto default;

                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Protected, modifier))
                        break;

                    if (ConflictingAccessibilityModifier(
                        declarationModifiers,
                        DeclarationModifiers.Protected,
                        modifier)) {
                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Protected;
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));
                    break;
            }
        }

        return declarationModifiers;
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

    private BoundType BindType(
        TypeSyntax type,
        DeclarationModifiers modifiers = DeclarationModifiers.None,
        bool explicitly = false,
        DeclarationModifiers handled = DeclarationModifiers.None) {
        if ((modifiers & DeclarationModifiers.ConstExpr) != 0 && (handled & DeclarationModifiers.ConstExpr) == 0) {
            var coreType = BindType(type, modifiers, explicitly, handled | DeclarationModifiers.ConstExpr);
            return BoundType.CopyWith(coreType, isConstantExpression: true);
        }

        if ((modifiers & DeclarationModifiers.Const) != 0 && (handled & DeclarationModifiers.Const) == 0) {
            var coreType = BindType(type, modifiers, explicitly, handled | DeclarationModifiers.Const);

            // Prevent raising this error if we have nested const keywords
            if ((coreType?.isImplicit ?? false) && !coreType.isConstant)
                diagnostics.Push(Error.ConstantAndVariable(type.location));

            return BoundType.CopyWith(coreType, isConstant: true);
        }

        if (type is ReferenceTypeSyntax rt) {
            if ((modifiers & DeclarationModifiers.ConstExpr) != 0) {
                diagnostics.Push(Error.CannotBeRefAndConstexpr(rt.refKeyword.location));
                return null;
            }

            var coreType = BindType(rt.type);

            if (coreType?.isImplicit ?? false)
                diagnostics.Push(Error.ImpliedReference(rt.refKeyword.location));

            return BoundType.CopyWith(coreType, isReference: true, isConstantReference: rt.constKeyword != null);
        } else if (type is NonNullableTypeSyntax nnt) {
            var coreType = BindType(nnt.type);
            return BoundType.CopyWith(coreType, isNullable: false);
        } else if (type is ArrayTypeSyntax at) {
            var coreType = BindType(at.elementType);
            var builder = ImmutableArray.CreateBuilder<BoundExpression>();

            if (coreType?.isImplicit ?? false) {
                var span = TextSpan.FromBounds(
                    at.rankSpecifiers[0].openBracket.location.span.start,
                    at.rankSpecifiers[^1].closeBracket.location.span.end
                );

                var location = new TextLocation(type.location.text, span);
                diagnostics.Push(Error.ImpliedDimensions(location));
            }

            foreach (var rankSpecifier in at.rankSpecifiers) {
                if (rankSpecifier.size is null) {
                    builder.Add(new BoundLiteralExpression(0));
                } else {
                    var casted = BindCast(rankSpecifier.size, BoundType.Int);
                    builder.Add(casted);
                }
            }

            if (!_flags.Includes(BinderFlags.LowLevelContext))
                diagnostics.Push(Error.ArrayOutsideOfLowLevelContext(at.location));

            return BoundType.CopyWith(coreType, dimensions: at.rankSpecifiers.Count, sizes: builder.ToImmutable());
        }

        var name = type as NameSyntax;

        if (name is QualifiedNameSyntax qn) {
            var rightType = qn.right;
            var leftType = BindType(qn.left);

            if (leftType.typeSymbol is PrimitiveTypeSymbol) {
                diagnostics.Push(Error.PrimitivesDoNotHaveMembers(qn.location));
                return null;
            } else {
                var namedLeft = leftType.typeSymbol as NamedTypeSymbol;
                var symbols = namedLeft.GetMembersPublic()
                    .Where(m => m is NamedTypeSymbol && m.name == rightType.identifier.text)
                    .Select(n => n as NamedTypeSymbol);

                if (!symbols.Any()) {
                    diagnostics.Push(Error.UnknownType(rightType.location, rightType.identifier.text));
                    return null;
                }

                if (rightType is IdentifierNameSyntax id) {
                    return BindIdentifierNameCore(id, symbols);
                } else if (rightType is TemplateNameSyntax tn) {
                    return BindTemplateNameCore(tn, symbols, null);
                }
            }
        } else if (name is EmptyNameSyntax) {
            return new BoundType(null, isImplicit: true, isConstant: true, isNullable: true);
        } else if (name is SimpleNameSyntax sn) {
            var symbols = LookupTypes(sn.identifier.text);

            if (!symbols.Any()) {
                if (sn.identifier.text == "var") {
                    if (explicitly)
                        diagnostics.Push(Error.CannotUseImplicit(sn.location));

                    if (sn is TemplateNameSyntax templateName) {
                        diagnostics.Push(Error.TemplateNotExpected(
                            templateName.templateArgumentList.location,
                            templateName.identifier.text
                        ));
                    }

                    return new BoundType(null, isImplicit: true, isNullable: true);
                }

                diagnostics.Push(Error.UnknownType(sn.location, sn.identifier.text));
                return null;
            }

            if (symbols.Length == 1 && symbols[0] is PrimitiveTypeSymbol) {
                if (sn is TemplateNameSyntax templateName) {
                    diagnostics.Push(Error.TemplateNotExpected(
                        templateName.templateArgumentList.location,
                        templateName.identifier.text
                    ));
                }

                return new BoundType(symbols[0], isNullable: true);
            }

            var namedSymbols = symbols.Select(s => s as NamedTypeSymbol);

            if (sn is IdentifierNameSyntax id)
                return BindIdentifierNameCore(id, namedSymbols);
            else if (sn is TemplateNameSyntax tn)
                return BindTemplateNameCore(tn, namedSymbols, null);
        }

        throw ExceptionUtilities.Unreachable();
    }

    private BoundType BindIdentifierNameCore(
        IdentifierNameSyntax name,
        IEnumerable<NamedTypeSymbol> symbols) {
        var identifierSymbols = symbols.Where(s => s.arity == 0);

        if (!identifierSymbols.Any()) {
            var result = _overloadResolution.TemplateOverloadResolution(
                symbols.ToImmutableArray<ISymbolWithTemplates>(),
                ImmutableArray<(string, TypeOrConstant)>.Empty,
                name.identifier.text,
                name.identifier,
                null
            );

            var constantArguments = ImmutableArray.CreateBuilder<TypeOrConstant>();

            foreach (var argument in result.arguments) {
                if (argument is BoundType t)
                    constantArguments.Add(new TypeOrConstant(t));
                else
                    constantArguments.Add(new BoundTypeOrConstant(argument.constantValue, argument.type, argument));
            }

            if (result.succeeded) {
                var bestOverload = result.bestOverload as NamedTypeSymbol;

                if (!_options.isLibrary && _wellKnownTypes.ContainsValue(bestOverload))
                    _usedLibraryTypes.Add(bestOverload);

                return new BoundType(
                    bestOverload,
                    templateArguments: constantArguments.ToImmutable(),
                    arity: bestOverload.arity
                );
            }

            return null;
        }

        var selected = identifierSymbols.First();

        if (!_options.isLibrary && _wellKnownTypes.ContainsValue(selected))
            _usedLibraryTypes.Add(selected);

        return new BoundType(selected, isNullable: true);
    }

    private BoundType BindTemplateNameCore(
        TemplateNameSyntax name,
        IEnumerable<NamedTypeSymbol> symbols,
        ImmutableArray<(string, TypeOrConstant)>? templateArguments) {
        var templateSymbols = symbols.Where(s => s.arity > 0);

        if (!templateSymbols.Any()) {
            diagnostics.Push(
                Error.TemplateNotExpected(name.templateArgumentList.location, name.identifier.text)
            );

            return null;
        }

        ImmutableArray<(string, TypeOrConstant)> arguments;

        if (!templateArguments.HasValue)
            BindTemplateArgumentList(name.templateArgumentList, out arguments);
        else
            arguments = templateArguments.Value;

        var result = _overloadResolution.TemplateOverloadResolution(
            templateSymbols.ToImmutableArray<ISymbolWithTemplates>(),
            arguments,
            name.identifier.text,
            name.identifier,
            name.templateArgumentList
        );

        if (!result.succeeded)
            return null;

        var constantArgumentsBuilder = ImmutableArray.CreateBuilder<TypeOrConstant>();

        foreach (var argument in result.arguments) {
            if (argument is BoundType t)
                constantArgumentsBuilder.Add(new TypeOrConstant(t));
            else
                constantArgumentsBuilder.Add(new BoundTypeOrConstant(argument.constantValue, argument.type, argument));
        }

        var bestOverload = result.bestOverload as NamedTypeSymbol;

        var constantArguments = constantArgumentsBuilder.ToImmutable();
        VerifyConstraintClauses(
            name.templateArgumentList.location,
            result.bestOverload.templateConstraints,
            constantArguments
        );

        if (!_options.isLibrary && _wellKnownTypes.ContainsValue(bestOverload))
            _usedLibraryTypes.Add(bestOverload);

        return new BoundType(
            bestOverload,
            templateArguments: constantArguments,
            arity: bestOverload.arity,
            isNullable: true
        );
    }

    private void VerifyConstraintClauses(
        TextLocation location,
        ImmutableArray<BoundExpression> constraints,
        ImmutableArray<TypeOrConstant> constantArguments) {
        for (var i = 0; i < constraints.Length; i++) {
            var constraint = constraints[i];

            if (constraint is BoundExtendExpression e) {
                var replacement = constantArguments[e.template.ordinal - 1];

                if (!TypeUtilities.TypeInheritsFrom(replacement.type, e.extension)) {
                    diagnostics.Push(Error.ExtendConstraintFailed(
                        location,
                        constraint.ToString(),
                        i + 1,
                        e.template.name,
                        e.extension.ToString()
                    ));
                }
            } else {
                var constraintResult = EvaluateConstraint(constraint, constantArguments);

                if (constraintResult is null) {
                    diagnostics.Push(Error.ConstraintWasNull(
                        location,
                        constraint.ToString(),
                        i + 1
                    ));
                } else if (!constraintResult.Value) {
                    diagnostics.Push(Error.ConstraintFailed(
                        location,
                        constraint.ToString(),
                        i + 1
                    ));
                }
            }
        }
    }

    private BoundExpression BindIdentifier(SimpleNameSyntax syntax, bool called, bool allowed) {
        var name = syntax.identifier.text;

        if (called) {
            _innerPrefix.Push(name);
            var innerName = ConstructInnerName();
            _innerPrefix.Pop();

            var potentialMethods = _scope.LookupOverloads(name, innerName);
            var builder = ImmutableArray.CreateBuilder<Symbol>();

            foreach (var potential in potentialMethods) {
                if (potential.containingType is null ||
                    (_containingType is not null &&
                    TypeUtilities.TypeInheritsFrom(_containingType, potential.containingType))) {
                    builder.Add(potential);
                }
            }

            var actualMethods = builder.ToImmutable();

            var isInner = false;

            if (_unresolvedLocals.TryGetValue(innerName, out var value) && !_resolvedLocals.Contains(innerName)) {
                BindLocalFunctionDeclaration(value);
                _resolvedLocals.Add(innerName);
                isInner = true;

                if (actualMethods.Length > 1) {
                    throw new BelteInternalException(
                        "BindIdentifier: overloaded generated function"
                    );
                }
            }

            if (isInner)
                actualMethods = [_scope.LookupSymbol<MethodSymbol>(innerName)];

            return BindCalledIdentifierInScope(syntax, actualMethods);
        }

        var symbolsList = _scope.LookupOverloads(name);
        var symbolsBuilder = ImmutableArray.CreateBuilder<Symbol>();

        foreach (var symbol in symbolsList) {
            if (!(symbol is ParameterSymbol p && p.isTemplate && p.type.typeSymbol == TypeSymbol.Type))
                symbolsBuilder.Add(symbol);
        }

        var symbols = symbolsBuilder.ToImmutable();

        if (symbols.Length == 0) {
            var primitive = LookupPrimitive(name);

            if (primitive is not null)
                symbols = [primitive];
        }

        if (symbols.Length > 0) {
            var containingTypesEqual = (_containingType is not null) &&
                (symbols[0].containingType is not null) &&
                (_containingType == symbols[0].containingType);

            if ((symbols[0] is not MethodSymbol) &&
                containingTypesEqual &&
                (_containingMethod?.isStatic ?? false) &&
                !symbols[0].isStatic &&
                symbols[0] is not TemplateParameterSymbol) {
                diagnostics.Push(Error.InvalidStaticReference(syntax.location, name));
                return new BoundErrorExpression();
            }

            if (symbols[0] is VariableSymbol && name == _shadowingVariable) {
                diagnostics.Push(Error.UndefinedSymbol(syntax.location, name));
                return new BoundErrorExpression();
            }
        }

        var result = BindNonCalledIdentifierInScope(syntax, symbols);

        if (!allowed && result is BoundType t)
            diagnostics.Push(Error.CannotUseType(syntax.location, t));

        if (_flags.Includes(BinderFlags.Class) &&
            result is BoundVariableExpression gv &&
            gv.variable is GlobalVariableSymbol) {
            diagnostics.Push(Error.CannotUseGlobalInClass(syntax.location, name));
        }

        if (_options.buildMode != BuildMode.Repl &&
            _containingType is not null &&
            result is BoundVariableExpression v &&
            v.variable.accessibility == Accessibility.Private &&
            _containingType != v.variable.containingType) {
            diagnostics.Push(Error.MemberIsInaccessible(
                syntax.location,
                v.variable.name,
                v.variable.containingType.name
            ));
        }

        return result;
    }

    private BoundExpression BindIdentifierInScope(
        SimpleNameSyntax syntax,
        bool called, ImmutableArray<Symbol> symbols) {
        if (called)
            return BindCalledIdentifierInScope(syntax, symbols);

        return BindNonCalledIdentifierInScope(syntax, symbols);
    }

    private BoundExpression BindCalledIdentifierInScope(SimpleNameSyntax syntax, ImmutableArray<Symbol> symbols) {
        var name = syntax.identifier.text;

        if (symbols.Length == 0) {
            diagnostics.Push(
                Error.UndefinedMethod(syntax.location, name, _options.buildMode == BuildMode.Interpret)
            );

            return new BoundErrorExpression();
        }

        var methods = symbols
            .Where(s => s is MethodSymbol)
            .Select(s => s as MethodSymbol).ToArray();

        if (methods.Length == 0) {
            diagnostics.Push(Error.CannotCallNonMethod(syntax.location, name));
            return new BoundErrorExpression();
        }

        var templateArguments = ImmutableArray<(string, TypeOrConstant)>.Empty;
        var templateName = syntax as TemplateNameSyntax;

        if (templateName is not null) {
            if (!BindTemplateArgumentList(templateName.templateArgumentList, out templateArguments))
                return new BoundErrorExpression();
        }

        var templateMethods = methods.Where(m => m.arity > 0);

        if (!templateMethods.Any() && templateName is not null) {
            diagnostics.Push(
                Error.TemplateNotExpected(templateName.templateArgumentList.location, syntax.identifier.text)
            );

            return new BoundErrorExpression();
        }

        var result = _overloadResolution.TemplateOverloadResolution(
            methods.ToImmutableArray<ISymbolWithTemplates>(),
            templateArguments,
            name,
            syntax.identifier,
            templateName?.templateArgumentList
        );

        if (!result.succeeded)
            return new BoundErrorExpression();

        var constantArgumentsBuilder = ImmutableArray.CreateBuilder<TypeOrConstant>();

        foreach (var argument in result.arguments) {
            if (argument is BoundType t)
                constantArgumentsBuilder.Add(new TypeOrConstant(t));
            else
                constantArgumentsBuilder.Add(new BoundTypeOrConstant(argument.constantValue, argument.type, argument));
        }

        var constantArguments = constantArgumentsBuilder.ToImmutable();
        VerifyConstraintClauses(
            templateName?.templateArgumentList?.location ?? syntax.location,
            result.bestOverload.templateConstraints,
            constantArguments
        );

        var bestOverload = result.bestOverload as MethodSymbol;

        // Hard coding in the methods that use the List type
        if (bestOverload == StandardLibrary.DirectoryMembers.GetDirectories ||
            bestOverload == StandardLibrary.DirectoryMembers.GetFiles ||
            bestOverload == StandardLibrary.FileMembers.AppendLines ||
            bestOverload == StandardLibrary.FileMembers.ReadLines ||
            bestOverload == StandardLibrary.FileMembers.WriteLines) {
            _usedLibraryTypes.Add(_wellKnownTypes[WellKnownTypeNames.List]);
        }

        return new BoundMethodGroup(
            name,
            result.bestOverloads.Select(m => m as MethodSymbol).ToImmutableArray(),
            constantArguments
        );
    }

    private BoundExpression BindNonCalledIdentifierInScope(SimpleNameSyntax syntax, ImmutableArray<Symbol> symbols) {
        var name = syntax.identifier.text;
        var arity = 0;

        var templateArguments = ImmutableArray<(string, TypeOrConstant)>.Empty;
        TextLocation templateLocation = null;

        if (syntax is TemplateNameSyntax tn) {
            arity = tn.templateArgumentList.arguments.Count;
            templateLocation = tn.templateArgumentList.location;

            if (!BindTemplateArgumentList(tn.templateArgumentList, out templateArguments))
                return new BoundErrorExpression();
        }

        if (!symbols.Any()) {
            diagnostics.Push(Error.UndefinedSymbol(syntax.location, name));
            return new BoundErrorExpression();
        }

        if (symbols[0] is MethodSymbol) {
            diagnostics.Push(Error.NotAVariable(syntax.location, name, true));
            return new BoundErrorExpression();
        }

        if (symbols[0] is VariableSymbol v) {
            if (arity > 0) {
                diagnostics.Push(Error.TemplateNotExpected(templateLocation, name));
                return new BoundErrorExpression();
            }

            if (_flags.Includes(BinderFlags.LocalFunction) && (!(v as ParameterSymbol)?.isTemplate ?? true)) {
                foreach (var frame in _trackedSymbols)
                    frame.Add(v);
            }

            return new BoundVariableExpression(v);
        } else if (symbols[0] is TypeSymbol) {
            if (symbols[0] is PrimitiveTypeSymbol p) {
                if (syntax is TemplateNameSyntax t) {
                    diagnostics.Push(
                        Error.TemplateNotExpected(t.templateArgumentList.location, t.identifier.text)
                    );

                    return new BoundErrorExpression();
                }

                return new BoundType(p, isNullable: true);
            }

            var namedSymbols = symbols.Select(s => s as NamedTypeSymbol);

            if (syntax is IdentifierNameSyntax i)
                return BindIdentifierNameCore(i, namedSymbols);
            else if (syntax is TemplateNameSyntax t)
                return BindTemplateNameCore(t, namedSymbols, templateArguments);
        }

        throw ExceptionUtilities.Unreachable();
    }

    private BoundExpression BindQualifiedName(QualifiedNameSyntax syntax, bool called) {
        var boundLeft = BindExpression(syntax.left, allowTypes: true);
        return BindMemberAccessWithBoundLeft(syntax, boundLeft, syntax.right, syntax.period, called);
    }

    private BoundExpression BindMemberAccessWithBoundLeft(
        ExpressionSyntax node,
        BoundExpression boundLeft,
        SimpleNameSyntax right,
        SyntaxToken operatorToken,
        bool called) {
        if (boundLeft is BoundErrorExpression || boundLeft.type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        var furthestRight = boundLeft;

        while (furthestRight is BoundMemberAccessExpression m)
            furthestRight = m.right;

        if (boundLeft.type.typeSymbol is PrimitiveTypeSymbol) {
            diagnostics.Push(Error.PrimitivesDoNotHaveMembers(node.location));
            return new BoundErrorExpression();
        }

        var namedType = boundLeft.type.typeSymbol as NamedTypeSymbol;
        var name = right.identifier.text;
        var symbols = namedType.GetMembers(name);

        if (!symbols.Any()) {
            diagnostics.Push(Error.NoSuchMember(right.location, boundLeft.type, name));
            return new BoundErrorExpression();
        }

        var isNullConditional = operatorToken.kind == SyntaxKind.QuestionPeriodToken;

        if (boundLeft.type.isNullable && boundLeft is BoundVariableExpression ve &&
            !_scope.GetAssignedVariables().Contains(ve.variable) && !isNullConditional) {
            diagnostics.Push(Warning.NullDeference(operatorToken.location));
        }

        var isStaticAccess = furthestRight is BoundType;
        var staticSymbols = symbols.Where(s => s.isStatic);
        var instanceSymbols = symbols.Where(s => !s.isStatic || ((s as ParameterSymbol)?.isTemplate ?? false));

        if (!isStaticAccess && !instanceSymbols.Any()) {
            diagnostics.Push(Error.InvalidInstanceReference(node.location, name, boundLeft.type.typeSymbol.name));
            return new BoundErrorExpression();
        }

        if (isStaticAccess && !staticSymbols.Any()) {
            diagnostics.Push(Error.InvalidStaticReference(node.location, name));
            return new BoundErrorExpression();
        }

        var selectedSymbols = isStaticAccess ? staticSymbols : instanceSymbols;
        var boundRight = BindIdentifierInScope(right, called, selectedSymbols.ToImmutableArray());

        CheckVariableAccessibility(boundLeft, boundRight, right.location);

        return new BoundMemberAccessExpression(boundLeft, boundRight, isNullConditional, isStaticAccess);
    }

    private void CheckVariableAccessibility(BoundExpression left, BoundExpression right, TextLocation textLocation) {
        if (left is BoundMemberAccessExpression m)
            CheckVariableAccessibility(m.left, m.right, textLocation);

        if (right is BoundVariableExpression v) {
            var insideSameType = _containingType == v.variable.containingType;
            var insideChildType = TypeUtilities.TypeInheritsFrom(_containingType, v.variable.containingType);

            if (_options.buildMode != BuildMode.Repl && left.type.typeSymbol is ClassSymbol) {
                if ((v.variable.accessibility == Accessibility.Private && !insideSameType) ||
                    (v.variable.accessibility == Accessibility.Protected && !insideChildType)) {
                    diagnostics.Push(Error.MemberIsInaccessible(
                        textLocation,
                        v.variable.name,
                        left.type.typeSymbol.name
                    ));
                }
            }
        }
    }

    private VariableSymbol BindVariable(
        SyntaxToken identifier,
        BoundType type,
        ConstantValue constant,
        DeclarationModifiers modifiers) {
        var name = identifier.text ?? "?";
        var declare = !identifier.isFabricated;
        var variable = _flags.Includes(BinderFlags.Method)
            ? new LocalVariableSymbol(name, type, constant, modifiers)
            : (VariableSymbol)new GlobalVariableSymbol(name, type, constant, modifiers);

        if (LookupTypes(name).Length > 0) {
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

    private FieldSymbol BindField(
        VariableDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        bool isStructField) {
        var name = declaration.identifier.text;
        BindAndVerifyType(declaration, modifiers, true, isStructField, out var type);
        ConstantValue constant = null;

        if ((modifiers & DeclarationModifiers.ConstExpr) != 0) {
            var initializer = declaration.initializer?.value is null
                ? new BoundTypeWrapper(type, new ConstantValue(null))
                : BindExpression(declaration.initializer.value);

            constant = initializer.constantValue;

            if (constant is null)
                diagnostics.Push(Error.NotConstantExpression(declaration.initializer.value.location));
            else if (type.isImplicit)
                type = BoundType.CopyWith(type, typeSymbol: BoundType.Assume(constant.value).typeSymbol);
        }

        var accessibility = BindAccessibilityFromModifiers(modifiers);

        var field = new FieldSymbol(
            name,
            type,
            constant,
            modifiers,
            accessibility
        );

        if (LookupTypes(name).Length > 0) {
            diagnostics.Push(
                Error.VariableUsingTypeName(declaration.identifier.location, name, type?.isConstant ?? false)
            );

            return field;
        }

        if (!_scope.TryDeclareVariable(field))
            diagnostics.Push(Error.VariableAlreadyDeclared(declaration.identifier.location, name, type.isConstant));

        return field;
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
                    es.expression.kind == BoundNodeKind.PostfixExpression ||
                    es.expression.kind == BoundNodeKind.ThrowExpression;

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
            case SyntaxKind.LocalDeclarationStatement:
                var statement = BindLocalDeclarationStatement((LocalDeclarationStatementSyntax)syntax);
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
                return new BoundBlockStatement([]);
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
            if (_containingMethod.type.typeSymbol == TypeSymbol.Void) {
                if (boundExpression != null)
                    diagnostics.Push(Error.UnexpectedReturnValue(expression.keyword.location));
            } else {
                if (boundExpression is null)
                    diagnostics.Push(Error.MissingReturnValue(expression.keyword.location));
                else
                    boundExpression = BindCast(expression.expression.location, boundExpression, _containingMethod.type);
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

        if (ConstantValue.IsNotNull(condition.constantValue) && !(bool)condition.constantValue.value)
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
        var step = BindExpression(statement.step, ownStatement: true);
        var body = BindLoopBody(statement.body, out var breakLabel, out var continueLabel);

        _scope = _scope.parent;
        _checkPeekedLocals--;

        return new BoundForStatement(initializer, condition, step, body, breakLabel, continueLabel);
    }

    private BoundStatement BindIfStatement(IfStatementSyntax statement) {
        var condition = BindCast(statement.condition, BoundType.NullableBool);

        BoundLiteralExpression constant = null;

        if (ConstantValue.IsNotNull(condition.constantValue)) {
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
        var inheritModifiers = BindBlockStatementModifiers(statement.modifiers);
        _checkPeekedLocals++;

        var statements = ImmutableArray.CreateBuilder<BoundStatement>();
        _scope = new BoundScope(_scope, true);
        var saved = _flags;

        if ((inheritModifiers & DeclarationModifiers.LowLevel) != 0)
            _flags |= BinderFlags.LowLevelContext;

        var frame = new List<string>();

        if (_localLocals.Count > 0) {
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
                    fd.attributeLists,
                    fd.modifiers,
                    fd.returnType,
                    fd.identifier,
                    fd.templateParameterList ?? SyntaxFactory.TemplateParameterList(),
                    fd.parameterList,
                    fd.constraintClauseList ?? SyntaxFactory.ConstraintClauseList(),
                    fd.body,
                    SyntaxFactory.Token(SyntaxKind.SemicolonToken),
                    fd.parent,
                    fd.position
                );

                var modifiers = _flags.Includes(BinderFlags.LowLevelContext)
                    ? DeclarationModifiers.LowLevel
                    : DeclarationModifiers.None;

                BindMethodDeclaration(declaration, modifiers | inheritModifiers, innerName);

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
        _flags = saved;
        _checkPeekedLocals--;

        return new BoundBlockStatement(statements.ToImmutable());
    }

    private DeclarationModifiers BindBlockStatementModifiers(SyntaxTokenList modifiers) {
        var declarationModifiers = DeclarationModifiers.None;

        if (modifiers is null)
            return declarationModifiers;

        foreach (var modifier in modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.LowlevelKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.LowLevel, modifier))
                        break;

                    declarationModifiers |= DeclarationModifiers.LowLevel;
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));
                    break;
            }
        }

        return declarationModifiers;
    }

    private DeclarationModifiers BindLocalDeclarationModifiers(SyntaxTokenList modifiers) {
        var declarationModifiers = DeclarationModifiers.None;

        if (modifiers is null)
            return declarationModifiers;

        foreach (var modifier in modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.ConstKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.Const, modifier))
                        break;

                    if ((declarationModifiers & DeclarationModifiers.ConstExpr) != 0) {
                        diagnostics.Push(
                            Error.ConflictingModifiers(modifier.location, "constant", "constant expression")
                        );

                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Const;
                    break;
                case SyntaxKind.ConstexprKeyword:
                    if (ModifierAlreadyApplied(declarationModifiers, DeclarationModifiers.ConstExpr, modifier))
                        break;

                    if ((declarationModifiers & DeclarationModifiers.Const) != 0) {
                        diagnostics.Push(
                            Error.ConflictingModifiers(modifier.location, "constant", "constant expression")
                        );

                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.ConstExpr;
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));
                    break;
            }
        }

        return declarationModifiers;
    }

    private bool BindAndVerifyType(
        VariableDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        bool isField,
        bool isStructField,
        out BoundType type) {
        var currentCount = diagnostics.Errors().Count;
        type = BindType(declaration.type, modifiers, isField);

        if (type?.typeSymbol?.isStatic ?? false)
            diagnostics.Push(Error.StaticVariable(declaration.type.location));

        if (diagnostics.Errors().Count > currentCount)
            return false;

        var value = declaration.initializer?.value;

        if (type.isImplicit && value is null) {
            diagnostics.Push(Error.NoInitOnImplicit(declaration.identifier.location));
            return false;
        }

        if (type.isReference && value is not null && value?.kind != SyntaxKind.ReferenceExpression) {
            diagnostics.Push(
                Error.ReferenceWrongInitialization(declaration.initializer.equalsToken.location, type.isConstant)
            );

            return false;
        }

        if (value is LiteralExpressionSyntax le) {
            if (le.token.kind == SyntaxKind.NullKeyword && type.isImplicit) {
                diagnostics.Push(Error.NullAssignOnImplicit(value.location, type.isConstant));
                return false;
            }
        }

        if (type.typeSymbol == TypeSymbol.Void) {
            diagnostics.Push(Error.VoidVariable(declaration.type.location));
            return false;
        }

        if (!type.isNullable && declaration.initializer is null && !isStructField) {
            diagnostics.Push(Error.NoInitOnNonNullable(declaration.identifier.location));
            return false;
        }

        return true;
    }

    private BoundVariableDeclaration BindVariableDeclaration(
        VariableDeclarationSyntax declaration,
        DeclarationModifiers modifiers) {
        var currentCount = diagnostics.Errors().Count;

        if (!BindAndVerifyType(declaration, modifiers, false, false, out var type))
            return null;

        var value = declaration.initializer?.value;
        var isNullable = type.isNullable;
        var isConstantExpression = (modifiers & DeclarationModifiers.ConstExpr) != 0;
        _shadowingVariable = declaration.identifier.text;

        if (_peekedLocals.Contains(declaration.identifier.text) && _checkPeekedLocals > 1) {
            diagnostics.Push(
                Error.NameUsedInEnclosingScope(declaration.identifier.location, declaration.identifier.text)
            );
        }

        if (type.isReference || (type.isImplicit && value?.kind == SyntaxKind.ReferenceExpression)) {
            var initializer = value != null
                ? BindReferenceExpression((ReferenceExpressionSyntax)value)
                : new BoundTypeWrapper(type, new ConstantValue(null));

            if (isConstantExpression && type.isImplicit)
                diagnostics.Push(Error.CannotBeRefAndConstexpr(value.location));

            if (diagnostics.Errors().Count > currentCount)
                return null;

            var tempType = type.isImplicit ? initializer.type : type;
            var variableType = BoundType.CopyWith(
                tempType,
                isConstant: type.isConstant ? true : null,
                isConstantReference: type.isConstantReference ? true : null,
                isExplicitReference: false,
                isNullable: isNullable,
                isLiteral: false
            );

            if (initializer.type.isConstantReference && !variableType.isConstantReference) {
                diagnostics.Push(Error.ReferenceToConstant(
                    declaration.initializer.equalsToken.location, variableType.isConstant)
                );

                return null;
            }

            if (!initializer.type.isConstant && variableType.isConstantReference) {
                diagnostics.Push(Error.ConstantToNonConstantReference(
                    declaration.initializer.equalsToken.location, variableType.isConstant)
                );

                return null;
            }

            if (diagnostics.Errors().Count > currentCount)
                return null;

            // References cant have implicit casts
            var variable = BindVariable(declaration.identifier, variableType, initializer.constantValue, modifiers);

            return new BoundVariableDeclaration(variable, initializer);
        } else if (type.dimensions > 0 ||
            (type.isImplicit && value is InitializerListExpressionSyntax)) {
            var initializer = (value is null ||
                (value is LiteralExpressionSyntax l && l.token.kind == SyntaxKind.NullKeyword))
                ? new BoundTypeWrapper(type, new ConstantValue(null))
                : BindExpression(value, initializerListType: type);

            if (initializer is BoundInitializerListExpression il && type.isImplicit) {
                if (il.items.Length == 0) {
                    diagnostics.Push(
                        Error.EmptyInitializerListOnImplicit(value.location, type.isConstant)
                    );

                    return null;
                } else {
                    var allNull = true;

                    foreach (var item in il.items) {
                        if (!ConstantValue.IsNull(item.constantValue))
                            allNull = false;
                    }

                    if (allNull) {
                        diagnostics.Push(
                            Error.NullInitializerListOnImplicit(value.location, type.isConstant)
                        );

                        return null;
                    }
                }
            }

            if (isConstantExpression && initializer.constantValue is null)
                diagnostics.Push(Error.NotConstantExpression(value.location));

            var tempType = type.isImplicit ? initializer.type : type;
            var variableType = BoundType.CopyWith(
                tempType, isConstant: type.isConstant ? true : null, isNullable: isNullable, isLiteral: false
            );

            if (!variableType.isNullable && initializer is BoundLiteralExpression ble && ble.value is null) {
                diagnostics.Push(Error.NullAssignOnNotNull(value.location, variableType.isConstant));
                return null;
            }

            var itemType = variableType.BaseType();

            var castedInitializer = BindCast(value?.location, initializer, variableType);
            var variable = BindVariable(
                declaration.identifier,
                BoundType.CopyWith(
                    type,
                    typeSymbol: itemType.typeSymbol,
                    isExplicitReference: false,
                    isLiteral: false,
                    dimensions: variableType.dimensions,
                    arity: variableType.arity,
                    templateArguments: variableType.templateArguments,
                    sizes: variableType.sizes
                ),
                castedInitializer.constantValue,
                modifiers
            );

            if (diagnostics.Errors().Count > currentCount)
                return null;

            return new BoundVariableDeclaration(variable, castedInitializer);
        } else {
            var initializer = value != null
                ? BindExpression(value)
                : new BoundTypeWrapper(type, new ConstantValue(null));

            if (isConstantExpression && initializer.constantValue is null)
                diagnostics.Push(Error.NotConstantExpression(value.location));

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
                diagnostics.Push(Error.NullAssignOnNotNull(value.location, variableType.isConstant));
                return null;
            }

            if (!variableType.isReference && value?.kind == SyntaxKind.ReferenceExpression) {
                diagnostics.Push(
                    Error.WrongInitializationReference(
                        declaration.initializer.equalsToken.location,
                        variableType.isConstant
                    )
                );

                return null;
            }

            var castedInitializer = BindCast(value?.location, initializer, variableType);
            var variable = BindVariable(
                declaration.identifier,
                variableType,
                castedInitializer.constantValue,
                modifiers
            );

            if (initializer.constantValue is null || initializer.constantValue.value != null)
                _scope.NoteAssignment(variable);

            if (diagnostics.Errors().Count > currentCount)
                return null;

            return new BoundVariableDeclaration(variable, castedInitializer);
        }
    }

    private BoundStatement BindLocalDeclarationStatement(LocalDeclarationStatementSyntax expression) {
        // ? This will return eventually
        BindAttributeLists(expression.attributeLists);

        var modifiers = BindLocalDeclarationModifiers(expression.modifiers);
        var declaration = BindVariableDeclaration(expression.declaration, modifiers);

        return new BoundLocalDeclarationStatement(declaration);
    }

    private BoundStatement BindExpressionStatement(ExpressionStatementSyntax statement) {
        var expression = BindExpression(statement.expression, true, true);
        return new BoundExpressionStatement(expression);
    }

    private BoundExpression BindExpression(
        ExpressionSyntax expression,
        bool canBeVoid = false,
        bool ownStatement = false,
        BoundType initializerListType = null,
        bool called = false,
        bool allowTypes = false) {
        var result = BindExpressionInternal(expression, ownStatement, initializerListType, called, allowTypes);

        if (!canBeVoid && result.type?.typeSymbol == TypeSymbol.Void) {
            diagnostics.Push(Error.NoValue(expression.location));
            return new BoundErrorExpression();
        }

        return result;
    }

    private BoundExpression BindExpressionInternal(
        ExpressionSyntax expression,
        bool ownStatement,
        BoundType initializerListType,
        bool called,
        bool allowTypes) {
        switch (expression.kind) {
            case SyntaxKind.LiteralExpression:
                return BindLiteralExpression((LiteralExpressionSyntax)expression);
            case SyntaxKind.InitializerListExpression:
                return BindInitializerListExpression((InitializerListExpressionSyntax)expression, initializerListType);
            case SyntaxKind.InitializerDictionaryExpression:
                return BindInitializerDictionaryExpression((InitializerDictionaryExpressionSyntax)expression);
            case SyntaxKind.UnaryExpression:
                return BindUnaryExpression((UnaryExpressionSyntax)expression);
            case SyntaxKind.BinaryExpression:
                return BindBinaryExpression((BinaryExpressionSyntax)expression);
            case SyntaxKind.TernaryExpression:
                return BindTernaryExpression((TernaryExpressionSyntax)expression);
            case SyntaxKind.ParenthesizedExpression:
                return BindParenExpression((ParenthesisExpressionSyntax)expression);
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
            case SyntaxKind.NameOfExpression:
                return BindNameOfExpression((NameOfExpressionSyntax)expression);
            case SyntaxKind.ObjectCreationExpression:
                return BindObjectCreationExpression((ObjectCreationExpressionSyntax)expression);
            case SyntaxKind.ThisExpression:
                return BindThisExpression((ThisExpressionSyntax)expression);
            case SyntaxKind.BaseExpression:
                return BindBaseExpression((BaseExpressionSyntax)expression);
            case SyntaxKind.ThrowExpression:
                return BindThrowExpression((ThrowExpressionSyntax)expression);
            case SyntaxKind.TemplateName:
            case SyntaxKind.IdentifierName:
                return BindIdentifier((SimpleNameSyntax)expression, called, allowTypes);
            case SyntaxKind.MemberAccessExpression:
                return BindMemberAccessExpression((MemberAccessExpressionSyntax)expression, called);
            case SyntaxKind.QualifiedName:
                return BindQualifiedName((QualifiedNameSyntax)expression, called);
            case SyntaxKind.NonNullableType when allowTypes:
            case SyntaxKind.ReferenceType when allowTypes:
            case SyntaxKind.ArrayType when allowTypes:
                return BindType((TypeSyntax)expression, explicitly: true);
            default:
                throw new BelteInternalException($"BindExpressionInternal: unexpected syntax '{expression.kind}'");
        }
    }

    private BoundExpression BindThrowExpression(ThrowExpressionSyntax expression) {
        var exceptionType = new BoundType(_wellKnownTypes[WellKnownTypeNames.Exception]);
        var boundExpression = BindExpression(expression.expression);
        var castedExpression = BindCast(expression.expression.location, boundExpression, exceptionType);

        return new BoundThrowExpression(castedExpression);
    }

    private BoundExpression BindThisExpression(ThisExpressionSyntax expression) {
        if (!_flags.Includes(BinderFlags.Class)) {
            diagnostics.Push(Error.CannotUseThis(expression.location));
            return new BoundErrorExpression();
        }

        return BindThisExpressionInternal();
    }

    private BoundThisExpression BindThisExpressionInternal() {
        var type = new BoundType(_containingType, isReference: true, arity: _containingType.arity);
        return new BoundThisExpression(type);
    }

    private BoundExpression BindBaseExpression(BaseExpressionSyntax expression) {
        if (!_flags.Includes(BinderFlags.Class)) {
            diagnostics.Push(Error.CannotUseBase(expression.location));
            return new BoundErrorExpression();
        }

        return BindBaseExpressionInternal();
    }

    private BoundBaseExpression BindBaseExpressionInternal() {
        return new BoundBaseExpression((_containingType as ClassSymbol).baseType);
    }

    private BoundExpression BindObjectCreationExpression(ObjectCreationExpressionSyntax expression) {
        var type = BindType(expression.type);
        type = BoundType.CopyWith(type, isLiteral: true, isNullable: false);

        if (type is null || type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        if (type.typeSymbol.isStatic) {
            diagnostics.Push(Error.CannotConstructStatic(expression.location, type.ToString()));
            return new BoundErrorExpression();
        }

        if (type.typeSymbol.isAbstract) {
            diagnostics.Push(Error.CannotConstructAbstract(expression.location, type.ToString()));
            return new BoundErrorExpression();
        }

        if (type.dimensions > 0) {
            return new BoundObjectCreationExpression(type);
        } else if (type.typeSymbol is StructSymbol) {
            if (!PartiallyBindArgumentList(expression.argumentList, out var arguments))
                return new BoundErrorExpression();

            if (arguments.Length > 0) {
                var span = TextSpan.FromBounds(
                    expression.argumentList.arguments[0].span.start,
                    expression.argumentList.arguments[^1].span.end
                );

                var location = new TextLocation(expression.syntaxTree.text, span);
                diagnostics.Push(Error.StructTakesNoArguments(location));
            }

            return new BoundObjectCreationExpression(type);
        } else {
            if (type.typeSymbol is not NamedTypeSymbol) {
                diagnostics.Push(Error.CannotConstructPrimitive(expression.location, type.typeSymbol.name));
                return new BoundErrorExpression();
            }

            if (!PartiallyBindArgumentList(expression.argumentList, out var arguments))
                return new BoundErrorExpression();

            var result = _overloadResolution.MethodOverloadResolution(
                (type.typeSymbol as NamedTypeSymbol).constructors,
                arguments,
                type.typeSymbol.name,
                expression.type,
                expression.argumentList,
                null,
                type
            );

            if (!result.succeeded)
                return new BoundErrorExpression();

            var insideSameType = _containingType == result.bestOverload.containingType;
            var insideChildType = TypeUtilities.TypeInheritsFrom(_containingType, result.bestOverload.containingType);

            if (_options.buildMode != BuildMode.Repl &&
                ((result.bestOverload.accessibility == Accessibility.Private && !insideSameType) ||
                (result.bestOverload.accessibility == Accessibility.Protected && !insideChildType))) {
                diagnostics.Push(Error.MemberIsInaccessible(
                    expression.type.location,
                    result.bestOverload.Signature(),
                    result.bestOverload.containingType.name
                ));
            }

            return new BoundObjectCreationExpression(type, result.bestOverload, result.arguments);
        }
    }

    private BoundExpression BindMemberAccessExpression(MemberAccessExpressionSyntax expression, bool called) {
        var boundLeft = BindExpression(expression.expression, allowTypes: true);

        return BindMemberAccessWithBoundLeft(
            expression,
            boundLeft,
            expression.name,
            expression.operatorToken,
            called
        );
    }

    private BoundExpression BindTypeOfExpression(TypeOfExpressionSyntax expression) {
        var type = BindType(expression.type);
        return new BoundTypeOfExpression(type);
    }

    private BoundExpression BindNameOfExpression(NameOfExpressionSyntax expression) {
        var name = BindExpression(expression.name, allowTypes: true);

        return new BoundLiteralExpression(name.kind switch {
            BoundNodeKind.VariableExpression => ((BoundVariableExpression)name).variable.name,
            BoundNodeKind.MemberAccessExpression => ((BoundMemberAccessExpression)name).left.ToString(),
            BoundNodeKind.ErrorExpression => "",
            BoundNodeKind.Type => name.type.ToString(),
            _ => throw ExceptionUtilities.Unreachable(),
        });
    }

    private BoundExpression BindReferenceExpression(ReferenceExpressionSyntax expression) {
        var boundExpression = BindExpression(expression.expression);

        if (boundExpression is not BoundVariableExpression and
            not BoundMemberAccessExpression and
            not BoundErrorExpression) {
            diagnostics.Push(Error.CannotReferenceNonField(expression.expression.location));
            return new BoundErrorExpression();
        }

        /*

    internal override BoundType type => BoundType.CopyWith(
        expression.type,
        isConstant: false,
        isConstantReference: expression.type.isConstant,
        isReference: true,
        isExplicitReference: true
    );

        */

        return new BoundReferenceExpression(boundExpression);
    }

    private BoundExpression BindPostfixExpression(PostfixExpressionSyntax expression, bool ownStatement = false) {
        var boundOperand = BindExpression(expression.operand);

        if (expression.operatorToken.kind is SyntaxKind.PlusPlusToken or SyntaxKind.MinusMinusToken) {
            if (boundOperand is not BoundVariableExpression
                and not BoundMemberAccessExpression
                and not BoundIndexExpression) {
                diagnostics.Push(Error.CannotIncrement(expression.operand.location));
                return new BoundErrorExpression();
            }
        }

        if (expression.operatorToken.kind is SyntaxKind.PlusPlusToken or SyntaxKind.MinusMinusToken)
            CheckForAssignmentInConstMethod(boundOperand, expression.operatorToken.location);

        (var isConstant, var isConstantReference) = CheckConstantality(boundOperand);

        if (boundOperand.type.isReference ? isConstantReference : isConstant) {
            string name = null;

            if (boundOperand is BoundVariableExpression v)
                name = v.variable.name;
            else if (boundOperand is BoundMemberAccessExpression m)
                name = (m.right as BoundVariableExpression).variable.name;

            diagnostics.Push(Error.ConstantAssignment(expression.operatorToken.location, name, false));

            return new BoundErrorExpression();
        }

        var boundOp = BoundPostfixOperator.BindWithOverloading(
            expression.operatorToken,
            expression.operatorToken.kind,
            boundOperand,
            _overloadResolution,
            out var result
        );

        if (result.succeeded || result.ambiguous) {
            if (ownStatement)
                return new BoundCallExpression(boundOperand.type, result.bestOverload, [boundOperand], []);
            else
                diagnostics.Push(Error.Unsupported.OverloadedPostfix(expression.operatorToken.location));
        }

        if (boundOp is null) {
            diagnostics.Push(Error.InvalidPostfixUse(
                expression.operatorToken.location,
                expression.operatorToken.text, boundOperand.type
            ));

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

        CheckForAssignmentInConstMethod(boundOperand, expression.operatorToken.location);

        (var isConstant, var isConstantReference) = CheckConstantality(boundOperand);

        if (boundOperand.type.isReference ? isConstantReference : isConstant) {
            string name = null;

            if (boundOperand is BoundVariableExpression v)
                name = v.variable.name;
            else if (boundOperand is BoundMemberAccessExpression m)
                name = (m.right as BoundVariableExpression).variable.name;

            diagnostics.Push(Error.ConstantAssignment(expression.operatorToken.location, name, false));

            return new BoundErrorExpression();
        }

        var boundOp = BoundPrefixOperator.BindWithOverloading(
            expression.operatorToken,
            expression.operatorToken.kind,
            boundOperand,
            _overloadResolution,
            out var result
        );

        if (result.succeeded || result.ambiguous)
            return new BoundCallExpression(boundOperand.type, result.bestOverload, [boundOperand], []);

        if (boundOp is null) {
            diagnostics.Push(Error.InvalidPrefixUse(
                expression.operatorToken.location,
                expression.operatorToken.text, boundOperand.type
            ));

            return new BoundErrorExpression();
        }

        return new BoundPrefixExpression(boundOp, boundOperand);
    }

    private BoundExpression BindIndexExpression(IndexExpressionSyntax expression) {
        var boundIndex = BindExpression(expression.index);

        if (ConstantValue.IsNotNull(boundIndex.constantValue) &&
            boundIndex.constantValue.value is int v &&
            expression.expression is InitializerListExpressionSyntax i) {
            // Optimizes inline list indexes working around the List rewrite that initializer lists perform
            var saved = _flags;
            _flags |= BinderFlags.LowLevelContext;
            var list = BindInitializerListExpression(i, null) as BoundInitializerListExpression;
            _flags = saved;

            return list.items[v];
        }

        var boundExpression = BindExpression(expression.expression);

        return BindIndexWithBoundSides(expression, boundExpression, boundIndex);
    }

    private BoundExpression BindIndexWithBoundSides(
        IndexExpressionSyntax expression,
        BoundExpression boundExpression,
        BoundExpression boundIndex) {
        var name = SyntaxFacts.GetOperatorMemberName(expression.openBracket.kind, 2);

        if (name is not null) {
            var symbols = ((boundExpression.type.typeSymbol is NamedTypeSymbol l) ? l.GetMembers(name) : [])
                .Concat((boundIndex.type.typeSymbol is NamedTypeSymbol r &&
                    boundExpression.type.typeSymbol != boundIndex.type.typeSymbol) ? r.GetMembers(name) : [])
                .Where(m => m is MethodSymbol)
                .Select(m => m as MethodSymbol)
                .ToImmutableArray();

            if (symbols.Length > 0) {
                var result = _overloadResolution.SuppressedMethodOverloadResolution(
                    symbols,
                    [(null, boundExpression), (null, boundIndex)],
                    name,
                    expression.openBracket,
                    null,
                    boundExpression.type
                );

                if (result.succeeded || result.ambiguous) {
                    var call = new BoundCallExpression(
                        boundExpression.type,
                        result.bestOverload,
                        [boundExpression, boundIndex],
                        []
                    );

                    if (expression.openBracket.kind == SyntaxKind.QuestionOpenBracketToken) {
                        return NullConditional(
                            @if: Call(BuiltinMethods.HasValueAny, boundExpression),
                            @then: call,
                            @else: Literal(null, result.bestOverload.type)
                        );
                    } else {
                        return call;
                    }
                }
            }
        }

        if (boundExpression is BoundErrorExpression)
            return boundExpression;

        if (boundExpression.type.dimensions > 0) {
            var index = BindCast(expression.index.location, boundIndex, BoundType.NullableInt);

            return new BoundIndexExpression(
                boundExpression,
                boundIndex,
                expression.openBracket.kind == SyntaxKind.QuestionOpenBracketToken
            );
        } else {
            diagnostics.Push(Error.CannotApplyIndexing(expression.location, boundExpression.type));
            return new BoundErrorExpression();
        }
    }

    private BoundExpression BindCallExpression(CallExpressionSyntax expression) {
        var boundExpression = BindExpression(expression.expression, called: true);
        BoundExpression receiver = new BoundEmptyExpression();

        if (boundExpression is BoundMemberAccessExpression ma) {
            receiver = ma.left;
            boundExpression = ma.right;
        } else if (_containingType is not null) {
            receiver = new BoundThisExpression(new BoundType(_containingType));
        }

        if (boundExpression is BoundMethodGroup mg) {
            if (!PartiallyBindArgumentList(expression.argumentList, out var arguments))
                return new BoundErrorExpression();

            if (mg.methods.Length == 1 &&
                (mg.methods[0] == BuiltinMethods.ToAny || mg.methods[0] == BuiltinMethods.ToObject)) {
                if (arguments.Length != 1) {
                    _overloadResolution.ResolveIncorrectArgumentCount(
                        expression.expression,
                        expression.argumentList.closeParenthesis.span,
                        mg.name,
                        mg.methods[0].parameters,
                        0,
                        arguments.Length,
                        expression.argumentList.arguments,
                        false
                    );

                    return new BoundErrorExpression();
                }

                var resultType = mg.methods[0] == BuiltinMethods.ToAny
                    ? BoundType.NullableAny
                    : new BoundType(_wellKnownTypes[WellKnownTypeNames.Object], isNullable: true);

                return new BoundCallExpression(
                    receiver,
                    new MethodSymbol(mg.name, [], [], mg.methods[0].parameters, resultType, null, mg.methods[0]),
                    arguments.Select(m => m.Item2).ToImmutableArray(),
                    []
                );
            }

            var result = _overloadResolution.MethodOverloadResolution(
                mg.methods,
                arguments,
                mg.name,
                expression.expression,
                expression.argumentList,
                mg.templateArguments,
                receiver.type
            );

            if (!result.succeeded)
                return new BoundErrorExpression();

            var method = result.bestOverload;

            if (receiver is not BoundEmptyExpression &&
                !method.isConstant &&
                (receiver.type.isReference ? receiver.type.isConstantReference : receiver.type.isConstant)) {
                diagnostics.Push(Error.NonConstantCallOnConstant(expression.location, method.Signature()));
            }

            if (receiver is BoundEmptyExpression || receiver is BoundThisExpression) {
                if ((_containingMethod?.isConstant ?? false) &&
                    !(method.isConstant || method.isStatic) &&
                    (method.containingType == _containingMethod.containingType)) {
                    diagnostics.Push(
                        Error.NonConstantCallInConstant(expression.location, method.Signature())
                    );
                }

                if ((_containingMethod?.isStatic ?? false) &&
                    (method.containingType == _containingMethod.containingType) &&
                    (!method.isStatic)) {
                    diagnostics.Push(Error.InvalidStaticReference(expression.location, mg.name));
                }
            }

            if (_options.buildMode != BuildMode.Repl && method.accessibility == Accessibility.Private) {
                if (_containingType is null ||
                    _containingType != method.containingType) {
                    diagnostics.Push(Error.MemberIsInaccessible(
                        expression.expression is MemberAccessExpressionSyntax m
                            ? m.name.location
                            : expression.expression.location,
                        $"{method.name}()",
                        method.containingType.name
                    ));
                }
            }

            if (receiver.type is not null &&
                receiver.type.templateArguments.Length > 0 &&
                method.type.typeSymbol is NamedTypeSymbol) {
                var templateMappings = new Dictionary<ParameterSymbol, TypeOrConstant>();

                for (var i = 0; i < receiver.type.templateArguments.Length; i++) {
                    templateMappings.Add(
                        (receiver.type.typeSymbol as NamedTypeSymbol).templateParameters[i],
                        receiver.type.templateArguments[i]
                    );
                }

                var returnType = BoundType.Clarify(method.type, templateMappings);

                method = new MethodSymbol(
                    method.name,
                    method.templateParameters,
                    method.templateConstraints,
                    method.parameters,
                    returnType,
                    method.declaration,
                    method,
                    method.modifiers,
                    method.accessibility);
            }

            return new BoundCallExpression(receiver, method, result.arguments, mg.templateArguments);
        }

        if (boundExpression is not BoundErrorExpression)
            diagnostics.Push(Error.CannotCallNonMethod(expression.expression.location, null));

        return new BoundErrorExpression();
    }

    private bool PartiallyBindArgumentList(
        ArgumentListSyntax argumentList, out ImmutableArray<(string, BoundExpression)> arguments) {
        if (argumentList is null) {
            arguments = [];
            return true;
        } else {
            return PartiallyBindArguments(argumentList.arguments, out arguments);
        }
    }

    private bool BindTemplateArgumentList(
        TemplateArgumentListSyntax argumentList,
        out ImmutableArray<(string, TypeOrConstant)> templateArguments) {
        var saved = _flags;
        _flags |= BinderFlags.TemplateArgumentList;

        bool result;

        if (argumentList is null) {
            templateArguments = [];
            result = true;
        } else {
            result = PartiallyBindArguments(argumentList.arguments, out var arguments, true);
            var builder = ImmutableArray.CreateBuilder<(string, TypeOrConstant)>();

            foreach ((var name, var expression) in arguments) {
                if (expression is BoundType t)
                    builder.Add((name, new TypeOrConstant(t)));
                else
                    builder.Add((name, new BoundTypeOrConstant(expression.constantValue, expression.type, expression)));
            }

            templateArguments = builder.ToImmutable();
        }

        _flags = saved;
        return result;
    }

    private bool PartiallyBindArguments(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        out ImmutableArray<(string, BoundExpression)> boundArguments,
        bool isTemplate = false) {
        var argumentsBuilder = ImmutableArray.CreateBuilder<(string name, BoundExpression expression)>();
        var seenNames = new HashSet<string>();
        var result = true;

        for (var i = 0; i < arguments.Count; i++) {
            var argumentName = arguments[i].identifier;

            if (i < arguments.Count - 1 &&
                argumentName != null &&
                arguments[i + 1].identifier is null) {
                diagnostics.Push(Error.NamedBeforeUnnamed(argumentName.location));
                result = false;
            }

            if (argumentName != null && !seenNames.Add(argumentName.text)) {
                diagnostics.Push(Error.NamedArgumentTwice(argumentName.location, argumentName.text));
                result = false;
            }

            var boundExpression = BindExpression(arguments[i].expression, allowTypes: isTemplate);

            if (boundExpression is BoundEmptyExpression)
                boundExpression = new BoundLiteralExpression(null, true);

            if (isTemplate &&
                boundExpression.constantValue is null &&
                boundExpression is not BoundType &&
                !boundExpression.type.isConstantExpression) {
                diagnostics.Push(Error.TemplateMustBeConstant(arguments[i].location));
                result = false;
            }

            argumentsBuilder.Add((argumentName?.text, boundExpression));
        }

        boundArguments = argumentsBuilder.ToImmutable();

        return result;
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

        var initializerList = new BoundInitializerListExpression(boundItems.ToImmutable(), type);

        if (_flags.Includes(BinderFlags.LowLevelContext))
            return initializerList;

        var listType = _wellKnownTypes[WellKnownTypeNames.List];
        var constructedListType = new BoundType(
            listType,
            templateArguments: [new TypeOrConstant(type.ChildType())],
            arity: 1
        );

        if (!_options.isLibrary)
            _usedLibraryTypes.Add(listType);

        return new BoundObjectCreationExpression(constructedListType, listType.constructors[3], [initializerList]);
    }

    private BoundExpression BindInitializerDictionaryExpression(InitializerDictionaryExpressionSyntax expression) {
        var keyType = BoundType.Any;
        var valueType = BoundType.Any;

        var builder = ImmutableArray.CreateBuilder<(BoundExpression, BoundExpression)>();

        foreach (var pair in expression.items) {
            var key = BindExpression(pair.key);
            var value = BindExpression(pair.value);
            builder.Add((key, value));
        }

        var dictionaryType = _wellKnownTypes[WellKnownTypeNames.Dictionary];
        var constructedType = new BoundType(
            dictionaryType,
            templateArguments: [new TypeOrConstant(keyType), new TypeOrConstant(valueType)],
            arity: 2
        );

        if (!_options.isLibrary)
            _usedLibraryTypes.Add(dictionaryType);

        return new BoundInitializerDictionaryExpression(builder.ToImmutable(), constructedType);
    }

    private BoundExpression BindLiteralExpression(LiteralExpressionSyntax expression) {
        var value = expression.token.value;
        return new BoundLiteralExpression(value);
    }

    private BoundExpression BindUnaryExpression(UnaryExpressionSyntax expression) {
        var boundOperand = BindExpression(expression.operand);

        if (boundOperand.type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        var boundOp = BoundUnaryOperator.BindWithOverloading(
            expression.operatorToken,
            expression.operatorToken.kind,
            boundOperand,
            _overloadResolution,
            out var result
        );

        if (result.succeeded || result.ambiguous)
            return new BoundCallExpression(boundOperand.type, result.bestOverload, [boundOperand], []);

        if (boundOp is null) {
            diagnostics.Push(Error.InvalidUnaryOperatorUse(
                expression.operatorToken.location,
                expression.operatorToken.text,
                boundOperand.type
            ));

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
            expression.leftOperatorToken.kind, expression.rightOperatorToken.kind, boundLeft.type,
            boundCenter.type, boundRight.type
        );

        if (boundOp is null) {
            diagnostics.Push(Error.InvalidTernaryOperatorUse(
                expression.leftOperatorToken.location,
                $"{expression.leftOperatorToken.text}{expression.rightOperatorToken.text}",
                boundLeft.type,
                boundCenter.type,
                boundRight.type)
            );

            return new BoundErrorExpression();
        }

        return new BoundTernaryExpression(boundLeft, boundOp, boundCenter, boundRight);
    }

    private BoundExpression BindBinaryExpression(BinaryExpressionSyntax expression) {
        var boundLeft = BindExpression(expression.left);
        var boundRight = BindExpression(expression.right, allowTypes: true);

        if (boundLeft.type.typeSymbol == TypeSymbol.Error || boundRight.type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        var boundOp = BoundBinaryOperator.BindWithOverloading(
            expression.operatorToken,
            expression.operatorToken.kind,
            boundLeft,
            boundRight,
            _overloadResolution,
            out var result
        );

        if (result.succeeded || result.ambiguous) {
            var receiver = boundLeft.type.typeSymbol == result.bestOverload.containingType
                ? boundLeft.type
                : boundRight.type;

            return new BoundCallExpression(receiver, result.bestOverload, [boundLeft, boundRight], []);
        }

        if (boundOp is null) {
            diagnostics.Push(Error.InvalidBinaryOperatorUse(
                expression.operatorToken.location,
                expression.operatorToken.text,
                boundLeft.type,
                boundRight.type,
                false
            ));

            return new BoundErrorExpression();
        }

        if (boundOp.opKind != BoundBinaryOperatorKind.NullCoalescing &&
            boundOp.opKind != BoundBinaryOperatorKind.Is &&
            boundOp.opKind != BoundBinaryOperatorKind.Isnt &&
            boundOp.opKind != BoundBinaryOperatorKind.As &&
            boundOp.opKind != BoundBinaryOperatorKind.ConditionalAnd &&
            boundOp.opKind != BoundBinaryOperatorKind.ConditionalOr) {
            if (ConstantValue.IsNull(boundLeft.constantValue) || ConstantValue.IsNull(boundRight.constantValue)) {
                diagnostics.Push(Warning.AlwaysValue(expression.location, null));
                return new BoundTypeWrapper(boundOp.type, new ConstantValue(null));
            }
        }

        if (boundOp.opKind is BoundBinaryOperatorKind.Is or BoundBinaryOperatorKind.Isnt) {
            if (ConstantValue.IsNull(boundLeft.constantValue) && ConstantValue.IsNull(boundRight.constantValue)) {
                var constant = boundOp.opKind == BoundBinaryOperatorKind.Is ? true : false;
                diagnostics.Push(Warning.AlwaysValue(expression.location, constant));
                return new BoundTypeWrapper(boundOp.type, new ConstantValue(constant));
            }
        }

        if (boundOp.opKind == BoundBinaryOperatorKind.Division &&
            boundRight.constantValue != null && boundRight.constantValue.value.Equals(0)) {
            diagnostics.Push(Error.DivideByZero(expression.location));
            return new BoundErrorExpression();
        }

        if (boundOp.opKind == BoundBinaryOperatorKind.As) {
            if (boundRight is not BoundType) {
                diagnostics.Push(Error.ExpectedType(expression.right.location));
                return new BoundErrorExpression();
            }

            var invalidPrimitiveCast = boundLeft.type != boundRight.type &&
                !boundRight.type.Equals(BoundType.Any, isTypeCheck: true) &&
                !boundRight.type.Equals(BoundType.NullableAny, isTypeCheck: true);

            var invalidReferenceCast = !TypeUtilities.TypeInheritsFrom(boundLeft.type, boundRight.type) &&
                !TypeUtilities.TypeInheritsFrom(boundRight.type, boundLeft.type);

            if ((boundLeft.type.typeSymbol is PrimitiveTypeSymbol && invalidPrimitiveCast) ||
                (boundLeft.type.typeSymbol is not PrimitiveTypeSymbol && invalidReferenceCast)) {
                diagnostics.Push(Error.CannotConvert(expression.location, boundLeft.type, boundRight.type));
                return new BoundErrorExpression();
            }
        }

        return new BoundBinaryExpression(boundLeft, boundOp, boundRight);
    }

    private BoundExpression BindParenExpression(ParenthesisExpressionSyntax expression) {
        return BindExpression(expression.expression);
    }

    private BoundExpression BindEmptyExpression(EmptyExpressionSyntax _) {
        return new BoundEmptyExpression();
    }

    private (bool, bool) CheckConstantality(BoundExpression expression) {
        var isConstant = false;
        var isConstantReference = false;

        while (true) {
            if (expression.type.isConstant)
                isConstant = true;
            if (expression.type.isConstantReference)
                isConstantReference = true;

            if (expression is BoundMemberAccessExpression m)
                expression = m.left;
            else
                break;
        }

        return (isConstant, isConstantReference);
    }

    private BoundExpression BindAssignmentExpression(AssignmentExpressionSyntax expression) {
        BoundExpression left;
        BoundExpression right = null;

        if (expression.left.kind == SyntaxKind.IndexExpression &&
            expression.assignmentToken.kind == SyntaxKind.EqualsToken) {
            var indexExpression = (IndexExpressionSyntax)expression.left;
            var name = SyntaxFacts.GetOperatorMemberName(SyntaxKind.OpenBracketToken, 3);
            var boundLeft = BindExpression(indexExpression.expression);
            var boundIndex = BindExpression(indexExpression.index);
            right = BindExpression(expression.right);

            var symbols = ((boundLeft.type.typeSymbol is NamedTypeSymbol l) ? l.GetMembers(name) : [])
                .Where(m => m is MethodSymbol)
                .Select(m => m as MethodSymbol)
                .ToImmutableArray();

            if (symbols.Length > 0) {
                var result = _overloadResolution.SuppressedMethodOverloadResolution(
                    symbols,
                    [(null, boundLeft), (null, boundIndex), (null, right)],
                    name,
                    expression.assignmentToken,
                    null,
                    boundLeft.type
                );

                if (result.succeeded || result.ambiguous) {
                    return new BoundCallExpression(
                        boundLeft.type,
                        result.bestOverload,
                        [boundLeft, boundIndex, right],
                        []
                    );
                }

                left = BindIndexWithBoundSides(indexExpression, boundLeft, boundIndex);
            }
        }

        left = BindExpression(expression.left);

        if (left is BoundErrorExpression)
            return left;

        if (left is not BoundVariableExpression &&
            left is not BoundMemberAccessExpression &&
            left is not BoundIndexExpression) {
            diagnostics.Push(Error.CannotAssign(expression.left.location));
            return new BoundErrorExpression();
        }

        var boundExpression = right ?? BindExpression(expression.right);
        var type = left.type;

        CheckForAssignmentInConstMethod(left, expression.assignmentToken.location);

        if (!type.isNullable && boundExpression is BoundLiteralExpression le && le.value is null) {
            diagnostics.Push(Error.NullAssignOnNotNull(expression.right.location, false));
            return boundExpression;
        }

        (var isConstant, var isConstantReference) = CheckConstantality(left);

        if ((type.isReference && isConstant &&
            boundExpression.kind == BoundNodeKind.ReferenceExpression) ||
            (isConstant && boundExpression.kind != BoundNodeKind.ReferenceExpression)) {
            string name = null;

            if (left is BoundVariableExpression v)
                name = v.variable.name;
            else if (left is BoundMemberAccessExpression m)
                name = (m.right as BoundVariableExpression).variable.name;

            diagnostics.Push(Error.ConstantAssignment(
                expression.assignmentToken.location, name, isConstantReference
            ));
        }

        if (expression.assignmentToken.kind != SyntaxKind.EqualsToken) {
            var equivalentOperatorTokenKind = SyntaxFacts.GetBinaryOperatorOfAssignmentOperator(
                expression.assignmentToken.kind
            );

            var boundOperator = BoundBinaryOperator.BindWithOverloading(
                expression.assignmentToken,
                equivalentOperatorTokenKind,
                left,
                boundExpression,
                _overloadResolution,
                out var result
            );

            if (result.succeeded) {
                var callExpression = new BoundCallExpression(
                    new BoundEmptyExpression(),
                    result.bestOverload,
                    [left, boundExpression],
                    []
                );

                var converted = BindCast(expression.right.location, callExpression, type);
                return new BoundAssignmentExpression(left, converted);
            }

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
                if (ve.variable.constantValue is null || ConstantValue.IsNotNull(ve.variable.constantValue))
                    _scope.NoteAssignment(ve.variable);
            }

            return new BoundAssignmentExpression(left, convertedExpression);
        }
    }

    private void CheckForAssignmentInConstMethod(BoundExpression left, TextLocation assignmentLocation) {
        // Checks if `left` is apart of the current instance
        // Starts by returning if inside a constant method
        // Recursively checks the right most node of `left` to get to a variable expression, and aborting if
        // it is a parameter or of another containing type (as that does not break the rules of constant methods)
        // Otherwise, checks the left most node of `left` to check if it roots in a field on this instance
        // If so, raises an error
        if (_containingMethod is null || !_containingMethod.isConstant)
            return;

        bool CheckForField(BoundExpression current) {
            if (current is BoundVariableExpression v) {
                if (v.variable is ParameterSymbol ||
                    v.variable.containingType != _containingMethod.containingType ||
                    v.variable is LocalVariableSymbol) {
                    return false;
                } else {
                    return true;
                }
            } else if (current is BoundMemberAccessExpression m) {
                return CheckForField(m.left);
            } else if (current is BoundIndexExpression i) {
                return CheckForField(i.expression);
            } else {
                return false;
            }
        }

        bool CheckIsPartOfThis(BoundExpression current) {
            if (current is BoundVariableExpression v) {
                if (current == left)
                    return CheckForField(v);
                else if (v.variable is ParameterSymbol || v.variable.containingType != _containingMethod.containingType)
                    return false;
            } else if (current is BoundMemberAccessExpression m) {
                if (m.left is BoundThisExpression)
                    return true;

                if (CheckForField(m.left))
                    return true;

                return CheckIsPartOfThis(m.right);
            } else if (current is BoundIndexExpression i) {
                if (i.expression is BoundThisExpression)
                    return true;

                return CheckIsPartOfThis(i.expression);
            }

            return false;
        }

        if (CheckIsPartOfThis(left))
            diagnostics.Push(Error.AssignmentInConstMethod(assignmentLocation));
    }

    private static bool IsCompilerComputable(
        BoundExpression expression,
        ImmutableArray<ParameterSymbol> allowedVariables) {
        if (expression.constantValue is not null)
            return true;

        switch (expression.kind) {
            case BoundNodeKind.UnaryExpression:
                return IsCompilerComputable(((BoundUnaryExpression)expression).operand, allowedVariables);
            case BoundNodeKind.BinaryExpression:
                var binaryExpression = (BoundBinaryExpression)expression;
                return IsCompilerComputable(binaryExpression.left, allowedVariables) &&
                    IsCompilerComputable(binaryExpression.right, allowedVariables);
            case BoundNodeKind.TernaryExpression:
                var ternaryExpression = (BoundTernaryExpression)expression;
                return IsCompilerComputable(ternaryExpression.left, allowedVariables) &&
                    IsCompilerComputable(ternaryExpression.center, allowedVariables) &&
                    IsCompilerComputable(ternaryExpression.right, allowedVariables);
            case BoundNodeKind.CastExpression:
                return IsCompilerComputable(((BoundCastExpression)expression).operand, allowedVariables);
            case BoundNodeKind.IndexExpression:
                var indexExpression = (BoundIndexExpression)expression;
                return IsCompilerComputable(indexExpression.expression, allowedVariables) &&
                    IsCompilerComputable(indexExpression.index, allowedVariables);
            case BoundNodeKind.VariableExpression:
                var variableExpression = (BoundVariableExpression)expression;

                if (allowedVariables.Contains(variableExpression.variable))
                    return true;
                else
                    return false;
            default:
                return false;
        }
    }

    private static bool? EvaluateConstraint(BoundExpression expression, ImmutableArray<TypeOrConstant> templates) {
        return (bool?)EvaluateExpression(expression);

        object EvaluateExpression(BoundExpression expression) {
            if (expression.constantValue is not null)
                return expression.constantValue.value;

            switch (expression.kind) {
                case BoundNodeKind.UnaryExpression:
                    var unaryExpression = (BoundUnaryExpression)expression;
                    var unaryOperand = EvaluateExpression(unaryExpression.operand);

                    return ConstantFolding.FoldUnary(
                        unaryExpression.op,
                        new BoundLiteralExpression(unaryOperand)
                    ).value;
                case BoundNodeKind.BinaryExpression:
                    var binaryExpression = (BoundBinaryExpression)expression;
                    var binaryLeft = EvaluateExpression(binaryExpression.left);
                    var binaryRight = EvaluateExpression(binaryExpression.right);

                    return ConstantFolding.FoldBinary(
                        new BoundLiteralExpression(binaryLeft),
                        binaryExpression.op,
                        new BoundLiteralExpression(binaryRight)
                    ).value;
                case BoundNodeKind.TernaryExpression:
                    var ternaryExpression = (BoundTernaryExpression)expression;
                    var ternaryLeft = EvaluateExpression(ternaryExpression.left);
                    var ternaryCenter = EvaluateExpression(ternaryExpression.center);
                    var ternaryRight = EvaluateExpression(ternaryExpression.right);

                    return ConstantFolding.FoldTernary(
                        new BoundLiteralExpression(ternaryLeft),
                        ternaryExpression.op,
                        new BoundLiteralExpression(ternaryCenter),
                        new BoundLiteralExpression(ternaryRight)
                    ).value;
                case BoundNodeKind.CastExpression:
                    var castExpression = (BoundCastExpression)expression;
                    var castOperand = EvaluateExpression(castExpression.operand);

                    return ConstantFolding.FoldCast(
                        new BoundLiteralExpression(castOperand),
                        castExpression.type
                    ).value;
                case BoundNodeKind.IndexExpression:
                    var indexExpression = (BoundIndexExpression)expression;
                    var indexOperand = EvaluateExpression(indexExpression.expression);
                    var indexIndex = EvaluateExpression(indexExpression.index);

                    return ConstantFolding.FoldIndex(
                        new BoundLiteralExpression(indexOperand),
                        new BoundLiteralExpression(indexIndex)
                    ).value;
                case BoundNodeKind.VariableExpression:
                    var variableExpression = (BoundVariableExpression)expression;
                    var index = (variableExpression.variable as ParameterSymbol).ordinal - 1;
                    var replacement = templates[index];

                    return replacement.constant.value;
                default:
                    throw ExceptionUtilities.Unreachable();
            }
        }
    }
}