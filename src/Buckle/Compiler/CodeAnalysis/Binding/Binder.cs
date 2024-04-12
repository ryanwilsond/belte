using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using Buckle.CodeAnalysis.FlowAnalysis;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Binding.BoundFactory;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Binds a <see cref="Syntax.InternalSyntax.Parser" /> output into a immutable "bound" tree. This is where most error
/// checking happens. The <see cref="Lowerer" /> is also called here to simplify the code,
/// And convert control of flow into gotos and labels. Dead code is also removed here, as well as other optimizations.
/// </summary>
internal sealed class Binder {
    private readonly MethodSymbol _containingMethod;
    private readonly NamedTypeSymbol _containingType;
    private readonly List<(MethodSymbol method, BoundBlockStatement body)> _methodBodies =
        new List<(MethodSymbol, BoundBlockStatement)>();
    private readonly CompilationOptions _options;
    private readonly OverloadResolution _overloadResolution;
    private readonly Stack<(BoundLabel breakLabel, BoundLabel continueLabel)> _loopStack = new Stack<(BoundLabel, BoundLabel)>();
    private readonly Stack<List<string>> _localLocals = new Stack<List<string>>();
    private readonly List<string> _resolvedLocals = new List<string>();
    private readonly Dictionary<string, LocalFunctionStatementSyntax> _unresolvedLocals =
        new Dictionary<string, LocalFunctionStatementSyntax>();

    private BinderFlags _flags;
    private BoundScope _scope;
    private int _labelCount;
    private ImmutableArray<string> _peekedLocals = ImmutableArray<string>.Empty;
    private int _checkPeekedLocals = 0;
    // Methods should be available correctly, so only track variables
    private Stack<HashSet<VariableSymbol>> _trackedSymbols = new Stack<HashSet<VariableSymbol>>();
    private Stack<HashSet<VariableSymbol>> _trackedDeclarations = new Stack<HashSet<VariableSymbol>>();
    private Stack<string> _innerPrefix = new Stack<string>();
    private string _shadowingVariable;

    private Binder(CompilationOptions options, BinderFlags flags, BoundScope parent, MethodSymbol method) {
        diagnostics = new BelteDiagnosticQueue();
        _scope = new BoundScope(parent);
        _containingMethod = method;
        _containingType = method?.containingType;
        _options = options;
        _flags = flags;
        _overloadResolution = new OverloadResolution(this);

        var needsNewScope = false;

        if (_containingType != null) {
            needsNewScope = true;
            _flags |= BinderFlags.Class;
        }

        var currentContainingType = _containingType;

        while (currentContainingType != null) {
            foreach (var member in currentContainingType.members) {
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

        var members = syntaxTrees.SelectMany(st => st.GetCompilationUnitRoot().members);

        foreach (var member in members) {
            if (member is TypeDeclarationSyntax ts)
                binder.PreBindTypeDeclaration(ts);
        }

        foreach (var member in members) {
            if (member is TypeDeclarationSyntax ts)
                binder.BindTypeDeclaration(ts);
            else if (member is MethodDeclarationSyntax ms)
                binder.BindMethodDeclaration(ms);
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

        if (binder._options.isScript) {
            if (globalStatements.Any()) {
                entryPoint = new MethodSymbol(
                    "<Eval>$", ImmutableArray<ParameterSymbol>.Empty, BoundType.NullableAny
                );
            }
        } else {
            var mains = methods.Where(f => f.name.ToLower() == "main").ToArray();

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

                if (entryPoint.parameters.Any()) {
                    if (entryPoint.parameters.Length != 2 ||
                        entryPoint.parameters[0].name != "argc" ||
                        !entryPoint.parameters[0].type.Equals(BoundType.Int) ||
                        entryPoint.parameters[1].name != "argv" ||
                        !entryPoint.parameters[1].type.Equals(new BoundType(TypeSymbol.String, dimensions: 1))) {
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
            var binder = new Binder(options, options.topLevelBinderFlags, parentScope, method);

            binder._innerPrefix.Push(method.name);
            var body = binder.BindMethodBody(method.declaration?.body, method.parameters);
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

        if (!allowExplicit && conversion.isExplicit) {
            var canAssert = false;

            if (expression.type.typeSymbol.kind == type.typeSymbol.kind &&
                expression.type.isNullable && !type.isNullable) {
                canAssert = true;
            }

            diagnostics.Push(
                Error.CannotConvertImplicitly(diagnosticLocation, expression.type, type, argument, canAssert)
            );
        }

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
            if (statement is LocalDeclarationStatementSyntax vd)
                locals.Add(vd.declaration.identifier.text);
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

    private TypeSymbol LookupPrimitive(string name) {
        return name switch {
            "any" => TypeSymbol.Any,
            "bool" => TypeSymbol.Bool,
            "int" => TypeSymbol.Int,
            "decimal" => TypeSymbol.Decimal,
            "string" => TypeSymbol.String,
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

    private BoundBlockStatement BindMethodBody(BlockStatementSyntax syntax, ImmutableArray<ParameterSymbol> parameters) {
        BoundBlockStatement body;

        if (syntax != null) {
            _peekedLocals = PeekLocals(syntax.statements, parameters);
            body = BindStatement(syntax) as BoundBlockStatement;
        } else {
            body = Block();
        }

        if (_containingMethod.name == WellKnownMemberNames.InstanceConstructorName &&
            _containingType is ClassSymbol cs && cs.defaultFieldAssignments.Length > 0) {
            return Block(BindDefaultFieldAssignments(cs.defaultFieldAssignments), body.statements);
        } else {
            return body;
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
                                field,
                                field.type
                            ),
                            initializer
                        )
                    )
                );
            }
        }

        return boundAssignmentsBuilder.ToImmutable();
    }

    private MethodSymbol BindMethodDeclaration(MethodDeclarationSyntax method, string name = null) {
        // ? This will return eventually
        BindAttributeLists(method.attributeLists);

        var modifiers = BindMethodDeclarationModifiers(method.modifiers);
        var type = BindType(method.returnType);
        var parameters = BindParameterList(method.parameterList);
        var newMethod = new MethodSymbol(
            name ?? method.identifier.text,
            parameters,
            type,
            method,
            modifiers: modifiers
        );

        if (newMethod.declaration.identifier.text != null && !_scope.TryDeclareMethod(newMethod))
            diagnostics.Push(Error.MethodAlreadyDeclared(method.identifier.location, name ?? newMethod.name));

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

                    if ((declarationModifiers & DeclarationModifiers.Static) != 0) {
                        diagnostics.Push(Error.ModifierAlreadyApplied(modifier.location, modifier.text));
                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Static;
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));
                    break;
            }
        }

        return declarationModifiers;
    }

    private MethodSymbol BindConstructorDeclaration(ConstructorDeclarationSyntax constructor) {
        // ? This will return eventually
        BindAttributeLists(constructor.attributeLists);

        var modifiers = BindConstructorModifiers(constructor.modifiers);
        var name = constructor.identifier.text;
        var parameters = BindParameterList(constructor.parameterList);
        var method = new MethodSymbol(
            WellKnownMemberNames.InstanceConstructorName,
            parameters,
            BoundType.Void,
            constructor
        );

        // Currently this method is only called while binding a class declaration, so for now this is guaranteed
        var parent = constructor.parent as ClassDeclarationSyntax;
        var className = parent.identifier.text;

        if (name != className)
            diagnostics.Push(Error.IncorrectConstructorName(constructor.identifier.location, className));

        if (name == className && !_scope.TryDeclareMethod(method))
            diagnostics.Push(Error.MethodAlreadyDeclared(constructor.identifier.location, name, className));

        return method;
    }

    private DeclarationModifiers BindConstructorModifiers(SyntaxTokenList modifiers) {
        if (modifiers is null)
            return DeclarationModifiers.None;

        foreach (var modifier in modifiers)
            diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));

        return DeclarationModifiers.None;
    }

    private void PreBindTypeDeclaration(TypeDeclarationSyntax @type) {
        if (@type is StructDeclarationSyntax s) {
            _scope.TryDeclareType(
                new StructSymbol(ImmutableArray<ParameterSymbol>.Empty, ImmutableArray<Symbol>.Empty, s)
            );
        } else if (@type is ClassDeclarationSyntax c) {
            _scope.TryDeclareType(
                new ClassSymbol(
                    ImmutableArray<ParameterSymbol>.Empty,
                    ImmutableArray<Symbol>.Empty,
                    ImmutableArray<(FieldSymbol, ExpressionSyntax)>.Empty,
                    c
                )
            );
        } else {
            throw new BelteInternalException($"BindTypeDeclaration: unexpected type '{@type.identifier.text}'");
        }
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
        // ? This will return eventually
        BindAttributeLists(@struct.attributeLists);

        var modifiers = BindStructDeclarationModifiers(@struct.modifiers);
        var builder = ImmutableList.CreateBuilder<Symbol>();
        var oldStruct = _scope.LookupSymbol<StructSymbol>(@struct.identifier.text);
        _scope = new BoundScope(_scope);

        foreach (var fieldDeclaration in @struct.members.OfType<FieldDeclarationSyntax>()) {
            var field = BindFieldDeclaration(fieldDeclaration);
            builder.Add(field);
        }

        _scope = _scope.parent;

        var newStruct = new StructSymbol(
            ImmutableArray<ParameterSymbol>.Empty,
            builder.ToImmutableArray(),
            @struct,
            modifiers
        );

        if (!_scope.TryReplaceSymbol(oldStruct, newStruct))
            diagnostics.Push(Error.TypeAlreadyDeclared(@struct.identifier.location, @struct.identifier.text, false));
        else
            diagnostics.Push(Error.CannotUseStruct(@struct.keyword.location));

        return newStruct;
    }

    private DeclarationModifiers BindStructDeclarationModifiers(SyntaxTokenList modifiers) {
        if (modifiers is null)
            return DeclarationModifiers.None;

        foreach (var modifier in modifiers)
            diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));

        return DeclarationModifiers.None;
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
        var modifiers = BindClassDeclarationModifiers(@class.modifiers);
        var builder = ImmutableList.CreateBuilder<Symbol>();
        var templateBuilder = ImmutableList.CreateBuilder<ParameterSymbol>();
        var oldClass = _scope.LookupSymbol<ClassSymbol>(@class.identifier.text);
        _scope = new BoundScope(_scope);

        var saved = _flags;
        _flags |= BinderFlags.Class;

        foreach (var member in @class.members) {
            if (member is TypeDeclarationSyntax ts)
                PreBindTypeDeclaration(ts);
        }

        if (@class.templateParameterList != null) {
            var templateParameters = BindParameters(@class.templateParameterList.parameters);

            foreach (var templateParameter in templateParameters) {
                builder.Add(templateParameter);
                templateBuilder.Add(templateParameter);
            }
        }

        var defaultFieldAssignmentsBuilder =
            ImmutableArray.CreateBuilder<(FieldSymbol, ExpressionSyntax)>();

        foreach (var fieldDeclaration in @class.members.OfType<FieldDeclarationSyntax>()) {
            var field = BindFieldDeclaration(fieldDeclaration);
            builder.Add(field);

            if (!field.isConstant && fieldDeclaration.declaration.initializer != null)
                defaultFieldAssignmentsBuilder.Add((field, fieldDeclaration.declaration.initializer.value));
        }

        var defaultFieldAssignments = defaultFieldAssignmentsBuilder.ToImmutable();
        var hasConstructor = false;

        foreach (var constructorDeclaration in @class.members.OfType<ConstructorDeclarationSyntax>()) {
            var constructor = BindConstructorDeclaration(constructorDeclaration);
            builder.Add(constructor);
            hasConstructor = true;
        }

        if (!hasConstructor) {
            var defaultConstructor = new MethodSymbol(
                WellKnownMemberNames.InstanceConstructorName,
                ImmutableArray<ParameterSymbol>.Empty,
                BoundType.Void
            );

            builder.Add(defaultConstructor);
            // This should never fail
            _scope.TryDeclareMethod(defaultConstructor);
        }

        foreach (var methodDeclaration in @class.members.OfType<MethodDeclarationSyntax>()) {
            var method = BindMethodDeclaration(methodDeclaration);
            builder.Add(method);
        }

        foreach (var typeDeclaration in @class.members.OfType<TypeDeclarationSyntax>()) {
            var type = BindTypeDeclaration(typeDeclaration);
            builder.Add(type);
        }

        // This allows the methods to be seen by the global scope
        foreach (var method in _scope.GetDeclaredMethods())
            _scope.parent.DeclareMethodStrict(method);

        _scope = _scope.parent;

        var newClass = new ClassSymbol(
            templateBuilder.ToImmutableArray(),
            builder.ToImmutableArray(),
            defaultFieldAssignments,
            @class,
            modifiers
        );

        // If no members, the default .ctor has yet to be built by the compiler, meaning this instance is a temporary
        // symbol that needs to be replaced
        if (oldClass.members.Length == 0)
            _scope.TryReplaceSymbol(oldClass, newClass);
        else if (!_scope.TryDeclareType(newClass))
            diagnostics.Push(Error.TypeAlreadyDeclared(@class.identifier.location, @class.identifier.text, true));

        _flags = saved;

        return newClass;
    }

    private DeclarationModifiers BindClassDeclarationModifiers(SyntaxTokenList modifiers) {
        if (modifiers is null)
            return DeclarationModifiers.None;

        foreach (var modifier in modifiers)
            diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));

        return DeclarationModifiers.None;
    }

    private BoundStatement BindLocalFunctionDeclaration(LocalFunctionStatementSyntax statement) {
        _innerPrefix.Push(statement.identifier.text);
        var functionSymbol = (MethodSymbol)_scope.LookupSymbol(ConstructInnerName());
        _innerPrefix.Pop();

        var binder = new Binder(_options, _flags | BinderFlags.LocalFunction, _scope, functionSymbol) {
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

        var newFunctionSymbol = parametersChanged
            ? functionSymbol.UpdateParameters(parameters.ToImmutable())
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
        // ? This will return eventually
        BindAttributeLists(fieldDeclaration.attributeLists);

        var modifiers = BindFieldDeclarationModifiers(fieldDeclaration.modifiers);
        var type = BindType(fieldDeclaration.declaration.type);
        return BindField(fieldDeclaration.declaration, type, modifiers);
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

                    if ((declarationModifiers & DeclarationModifiers.Const) != 0) {
                        diagnostics.Push(Error.ModifierAlreadyApplied(modifier.location, modifier.text));
                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Const;
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));
                    break;
            }
        }

        return DeclarationModifiers.None;
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
        if (type is ReferenceTypeSyntax rt) {
            var coreType = BindType(rt.type);

            if (coreType.isImplicit)
                diagnostics.Push(Error.ImpliedReference(rt.refKeyword.location));

            return BoundType.CopyWith(coreType, isReference: true, isConstantReference: rt.constKeyword != null);
        } else if (type is NonNullableTypeSyntax nnt) {
            var coreType = BindType(nnt.type);
            return BoundType.CopyWith(coreType, isNullable: false);
        } else if (type is ArrayTypeSyntax at) {
            var coreType = BindType(at.elementType);

            if (coreType.isImplicit) {
                var span = TextSpan.FromBounds(
                    at.rankSpecifiers[0].openBracket.location.span.start,
                    at.rankSpecifiers[^1].closeBracket.location.span.end
                );

                var location = new TextLocation(type.location.text, span);
                diagnostics.Push(Error.ImpliedDimensions(location));
            }

            return BoundType.CopyWith(coreType, dimensions: at.rankSpecifiers.Count);
        }

        var name = type as NameSyntax;

        if (name is QualifiedNameSyntax qn) {
            var rightType = qn.right;
            var leftType = BindType(qn.left);

            if (leftType.typeSymbol is PrimitiveTypeSymbol) {
                diagnostics.Push(Error.PrimitivesDoNotHaveMembers(qn.period.location));
                return null;
            } else {
                var namedLeft = leftType.typeSymbol as NamedTypeSymbol;
                var symbols = namedLeft.members
                    .Where(m => m is NamedTypeSymbol && m.name == rightType.identifier.text)
                    .Select(n => n as NamedTypeSymbol);

                if (!symbols.Any()) {
                    diagnostics.Push(Error.UnknownType(rightType.location, rightType.identifier.text));
                    return null;
                }

                if (rightType is IdentifierNameSyntax id) {
                    return BindIdentifierNameCore(id, symbols);
                } else if (rightType is TemplateNameSyntax tn) {
                    return BindTemplateNameCore(tn, symbols);
                }
            }
        } else if (name is EmptyNameSyntax) {
            return new BoundType(null, isImplicit: true, isConstant: true);
        } else if (name is SimpleNameSyntax sn) {
            var symbols = LookupTypes(sn.identifier.text);

            if (!symbols.Any()) {
                diagnostics.Push(Error.UnknownType(sn.location, sn.identifier.text));
                return null;
            }

            if (sn.identifier.text == "var") {
                if (sn is TemplateNameSyntax templateName) {
                    diagnostics.Push(Error.TemplateNotExpected(
                        templateName.templateArgumentList.location,
                        templateName.identifier.text
                    ));
                }

                return new BoundType(null, isImplicit: true);
            }

            if (symbols.Length == 1 && symbols[0] is PrimitiveTypeSymbol) {
                if (sn is TemplateNameSyntax templateName) {
                    diagnostics.Push(Error.TemplateNotExpected(
                        templateName.templateArgumentList.location,
                        templateName.identifier.text
                    ));
                }

                return new BoundType(symbols[0]);
            }

            var namedSymbols = symbols.Select(s => s as NamedTypeSymbol);

            if (sn is IdentifierNameSyntax id)
                return BindIdentifierNameCore(id, namedSymbols);
            else if (sn is TemplateNameSyntax tn)
                return BindTemplateNameCore(tn, namedSymbols);
        }

        throw new UnreachableException();
    }

    private BoundType BindIdentifierNameCore(
        IdentifierNameSyntax name,
        IEnumerable<NamedTypeSymbol> symbols) {
        var identifierSymbols = symbols.Where(s => s.arity == 0);

        if (!identifierSymbols.Any()) {
            var result = _overloadResolution.TemplateOverloadResolution(
                symbols.ToImmutableArray(),
                ImmutableArray<(string, BoundConstant)>.Empty,
                name.identifier.text,
                name.identifier,
                null
            );

            if (result.succeeded) {
                return new BoundType(
                    result.bestOverload,
                    templateArguments: result.arguments,
                    arity: result.bestOverload.arity
                );
            }

            return null;
        }

        return new BoundType(identifierSymbols.First());
    }

    private BoundType BindTemplateNameCore(TemplateNameSyntax name, IEnumerable<NamedTypeSymbol> symbols) {
        var templateSymbols = symbols.Where(s => s.arity > 0);

        if (!templateSymbols.Any()) {
            diagnostics.Push(
                Error.TemplateNotExpected(name.templateArgumentList.location, name.identifier.text)
            );

            return null;
        }

        if (BindTemplateArgumentList(name.templateArgumentList, out var arguments)) {
            var result = _overloadResolution.TemplateOverloadResolution(
                templateSymbols.ToImmutableArray(),
                arguments,
                name.identifier.text,
                name.identifier,
                name.templateArgumentList
            );

            if (result.succeeded) {
                return new BoundType(
                    result.bestOverload,
                    templateArguments: result.arguments,
                    arity: result.bestOverload.arity
                );
            }
        }

        return null;
    }

    private BoundExpression BindIdentifier(SimpleNameSyntax syntax, bool called) {
        var arity = 0;
        var templateArguments = ImmutableArray<(string, BoundConstant)>.Empty;
        var name = syntax.identifier.text;

        if (syntax is TemplateNameSyntax tn) {
            arity = tn.templateArgumentList.arguments.Count;

            if (!BindTemplateArgumentList(tn.templateArgumentList, out templateArguments))
                return new BoundErrorExpression();
        }

        if (called) {
            if (arity > 0) {
                diagnostics.Push(
                    Error.TemplateNotExpected((syntax as TemplateNameSyntax).templateArgumentList.location, name)
                );

                return new BoundErrorExpression();
            }

            var methods = ImmutableArray<MethodSymbol>.Empty;

            _innerPrefix.Push(name);
            var innerName = ConstructInnerName();
            _innerPrefix.Pop();

            var symbols = _scope.LookupOverloads(name, innerName);

            if (symbols.Length == 0) {
                diagnostics.Push(
                    Error.UndefinedMethod(syntax.location, name, _options.buildMode == BuildMode.Interpret)
                );

                return new BoundErrorExpression();
            } else if (symbols[0] is not MethodSymbol) {
                diagnostics.Push(Error.CannotCallNonMethod(syntax.location));
                return new BoundErrorExpression();
            }

            var isInner = false;

            if (_unresolvedLocals.ContainsKey(innerName) && !_resolvedLocals.Contains(innerName)) {
                BindLocalFunctionDeclaration(_unresolvedLocals[innerName]);
                _resolvedLocals.Add(innerName);
                isInner = true;

                if (symbols.Length > 1) {
                    throw new BelteInternalException(
                        "BindIdentifier: overloaded generated function"
                    );
                }
            }

            if (isInner)
                methods = ImmutableArray.Create(_scope.LookupSymbol<MethodSymbol>(innerName));
            else
                methods = symbols.Where(s => s is MethodSymbol).Select(s => s as MethodSymbol).ToImmutableArray();

            return new BoundMethodGroup(name, methods);
        }



        return BindNameCore(syntax, false);
    }

    private BoundExpression BindQualifiedName(QualifiedNameSyntax syntax, bool called) {

    }

    private Symbol BindVariableOrTypeReference(SyntaxToken identifier, bool allowTypes = false) {
        var name = identifier.text;
        Symbol reference = null;

        var primitive = LookupPrimitive(name);

        if (primitive != null)
            return primitive;

        switch (name == _shadowingVariable ? null : _scope.LookupSymbol(name, _containingMethod?.isStatic ?? false)) {
            case VariableSymbol variable:
                if (_containingType is not null &&
                    variable.containingType is not null &&
                    _containingType != variable.containingType) {
                    diagnostics.Push(Error.InvalidStaticReference(identifier.location, name));
                    break;
                }

                reference = variable;
                break;
            case NamedTypeSymbol type when allowTypes:
                if (_containingType is not null &&
                    type.containingType is not null &&
                    _containingType != type.containingType) {
                    diagnostics.Push(Error.InvalidStaticReference(identifier.location, name));
                    break;
                }

                reference = type;
                break;
            case NamedTypeSymbol when !allowTypes:
                diagnostics.Push(Error.NotAVariable(identifier.location, name, false));
                break;
            case null:
                diagnostics.Push(Error.UndefinedSymbol(identifier.location, name));
                break;
            default:
                diagnostics.Push(Error.NotAVariable(identifier.location, name, true));
                break;
        }

        if (reference != null && _flags.Includes(BinderFlags.LocalFunction) && reference is VariableSymbol vs) {
            foreach (var frame in _trackedSymbols)
                frame.Add(vs);
        }

        return reference;
    }

    private VariableSymbol BindVariableReference(SyntaxToken identifier) {
        return BindVariableOrTypeReference(identifier, allowTypes: false) as VariableSymbol;
    }

    private VariableSymbol BindVariable(SyntaxToken identifier, BoundType type, BoundConstant constant = null) {
        var name = identifier.text ?? "?";
        var declare = !identifier.isFabricated;
        var variable = _flags.Includes(BinderFlags.Method)
            ? new LocalVariableSymbol(name, type, constant)
            : (VariableSymbol)new GlobalVariableSymbol(name, type, constant);

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
        BoundType type,
        DeclarationModifiers modifiers) {
        var name = declaration.identifier.text;
        BoundConstant constant = null;

        if (type.isConstant) {
            var initializer = BindExpression(declaration.initializer.value);
            constant = initializer?.constantValue;
        }

        var field = new FieldSymbol(name, type, constant, modifiers);

        if (LookupTypes(name).Length > 0) {
            diagnostics.Push(Error.VariableUsingTypeName(declaration.identifier.location, name, type.isConstant));
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
                    fd.attributeLists,
                    fd.modifiers,
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

    private DeclarationModifiers BindLocalDeclarationModifiers(SyntaxTokenList modifiers) {
        var declarationModifiers = DeclarationModifiers.None;

        if (modifiers is null)
            return declarationModifiers;

        foreach (var modifier in modifiers) {
            switch (modifier.kind) {
                case SyntaxKind.ConstKeyword:
                    if ((declarationModifiers & DeclarationModifiers.Const) != 0) {
                        diagnostics.Push(Error.ModifierAlreadyApplied(modifier.location, modifier.text));
                        break;
                    }

                    declarationModifiers |= DeclarationModifiers.Const;
                    break;
                default:
                    diagnostics.Push(Error.InvalidModifier(modifier.location, modifier.text));
                    break;
            }
        }

        return DeclarationModifiers.None;
    }

    private BoundVariableDeclaration BindVariableDeclaration(
        VariableDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        bool declare = true) {
        var currentCount = diagnostics.Errors().Count;
        var type = BindType(declaration.type);

        if ((modifiers & DeclarationModifiers.Const) != 0 && !type.isConstant)
            type = BoundType.CopyWith(type, isConstant: true);

        var value = declaration.initializer.value;

        if (diagnostics.Errors().Count > currentCount)
            return null;

        if (type.isImplicit && value is null) {
            diagnostics.Push(Error.NoInitOnImplicit(declaration.identifier.location));
            return null;
        }

        if (type.isReference && value is null) {
            diagnostics.Push(Error.ReferenceNoInitialization(declaration.identifier.location, type.isConstant));
            return null;
        }

        if (type.isReference && value?.kind != SyntaxKind.ReferenceExpression) {
            diagnostics.Push(
                Error.ReferenceWrongInitialization(declaration.initializer.equalsToken.location, type.isConstant)
            );

            return null;
        }

        if (value is LiteralExpressionSyntax le) {
            if (le.token.kind == SyntaxKind.NullKeyword && type.isImplicit) {
                diagnostics.Push(Error.NullAssignOnImplicit(value.location, type.isConstant));
                return null;
            }
        }

        if (type.typeSymbol == TypeSymbol.Void) {
            diagnostics.Push(Error.VoidVariable(declaration.type.location));
            return null;
        }

        var isNullable = type.isNullable;
        _shadowingVariable = declaration.identifier.text;

        if (_peekedLocals.Contains(declaration.identifier.text) && _checkPeekedLocals > 1) {
            diagnostics.Push(
                Error.NameUsedInEnclosingScope(declaration.identifier.location, declaration.identifier.text)
            );
        }

        if (type.isReference || (type.isImplicit && value?.kind == SyntaxKind.ReferenceExpression)) {
            var initializer = BindReferenceExpression((ReferenceExpressionSyntax)value);

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
                    declaration.initializer.equalsToken.location, variableType.isConstantReference)
                );

                return null;
            }

            if (!initializer.type.isConstant && variableType.isConstant) {
                diagnostics.Push(Error.ConstantToNonConstantReference(
                    declaration.initializer.equalsToken.location, variableType.isConstantReference)
                );

                return null;
            }

            if (diagnostics.Errors().Count > currentCount)
                return null;

            // References cant have implicit casts
            var variable = declare
                ? BindVariable(declaration.identifier, variableType, initializer.constantValue)
                : null;

            return new BoundVariableDeclaration(variable, initializer);
        } else if (type.dimensions > 0 ||
            (type.isImplicit && value is InitializerListExpressionSyntax)) {
            var initializer = (value is null ||
                (value is LiteralExpressionSyntax l && l.token.kind == SyntaxKind.NullKeyword))
                ? new BoundTypeWrapper(type, new BoundConstant(null))
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
                        if (!BoundConstant.IsNull(item.constantValue))
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
            var variable = declare
                ? BindVariable(declaration.identifier,
                    BoundType.CopyWith(
                        type, typeSymbol: itemType.typeSymbol, isExplicitReference: false,
                        isLiteral: false, dimensions: variableType.dimensions
                    ),
                    castedInitializer.constantValue
                  )
                : null;

            if (diagnostics.Errors().Count > currentCount)
                return null;

            return new BoundVariableDeclaration(variable, castedInitializer);
        } else {
            var initializer = value != null
                ? BindExpression(value)
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
            var variable = declare
                ? BindVariable(declaration.identifier, variableType, castedInitializer.constantValue)
                : null;

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
        bool called = false) {
        var result = BindExpressionInternal(expression, ownStatement, initializerListType, called);

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
        bool called) {
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
            case SyntaxKind.ObjectCreationExpression:
                return BindObjectCreationExpression((ObjectCreationExpressionSyntax)expression);
            case SyntaxKind.ThisExpression:
                return BindThisExpression((ThisExpressionSyntax)expression);
            case SyntaxKind.TemplateName:
            case SyntaxKind.IdentifierName:
                return BindIdentifier((SimpleNameSyntax)expression, called);
            case SyntaxKind.MemberAccessExpression:
                return BindMemberAccessExpression((MemberAccessExpressionSyntax)expression, called);
            case SyntaxKind.QualifiedName:
                return BindQualifiedName((QualifiedNameSyntax)expression, called);
            default:
                throw new BelteInternalException($"BindExpressionInternal: unexpected syntax '{expression.kind}'");
        }
    }

    private BoundExpression BindThisExpression(ThisExpressionSyntax expression) {
        if (!_flags.Includes(BinderFlags.Class)) {
            diagnostics.Push(Error.CannotUseThis(expression.location));
            return new BoundErrorExpression();
        }

        return BindThisExpressionInternal();
    }

    private BoundThisExpression BindThisExpressionInternal() {
        var type = new BoundType(_containingType, isReference: true);
        return new BoundThisExpression(type);
    }

    private BoundExpression BindObjectCreationExpression(ObjectCreationExpressionSyntax expression) {
        var type = BoundType.CopyWith(BindType(expression.type), isLiteral: true, isNullable: false);

        if (type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        if (type.typeSymbol is not NamedTypeSymbol) {
            diagnostics.Push(Error.CannotConstructPrimitive(expression.type.location, type.typeSymbol.name));
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

    private BoundExpression BindMemberAccessExpression(MemberAccessExpressionSyntax expression, bool called) {
        var operand = BindExpression(expression.expression);

        if (operand is BoundErrorExpression)
            return operand;

        if (operand is BoundTypeOfExpression te) {
            if (te.typeOfType.typeSymbol is PrimitiveTypeSymbol) {
                diagnostics.Push(Error.PrimitivesDoNotHaveMembers(expression.location));
                return new BoundErrorExpression();
            }
        } else if (operand.type.typeSymbol is PrimitiveTypeSymbol) {
            diagnostics.Push(Error.PrimitivesDoNotHaveMembers(expression.location));
            return new BoundErrorExpression();
        }

        var type = (operand is BoundTypeOfExpression toe ? toe.typeOfType : operand.type)
            .typeSymbol as ITypeSymbolWithMembers;
        // If there are multiple members with the name, it means it is an overloaded method
        // BindCallExpression will resolve the correct one for us so we just get the first one as a placeholder
        var symbols = BindNamePlural(expression.name);

        if (!symbols.Any()) {
            diagnostics.Push(
                Error.NoSuchMember(expression.identifier.location, operand.type, expression.identifier.text)
            );

            return new BoundErrorExpression();
        }

        var isNullCondition = expression.op.kind == SyntaxKind.QuestionPeriodToken;

        if (operand.type.isNullable && operand is BoundVariableExpression ve &&
            !_scope.GetAssignedVariables().Contains(ve.variable) && !isNullCondition) {
            diagnostics.Push(Warning.NullDeference(expression.operatorToken.location));
        }

        var staticAccess = operand is BoundTypeOfExpression;
        var staticSymbols = symbols.Where(s => s.isStatic || (s is FieldSymbol f && f.constantValue is not null));
        var instanceSymbols = symbols.Where(s => !s.isStatic && (s is not FieldSymbol f || f.constantValue is null));

        if (!staticAccess && !instanceSymbols.Any()) {
            diagnostics.Push(Error.InvalidInstanceReference(expression.location, expression.identifier.text, type.name));
            return new BoundErrorExpression();
        }

        if (staticAccess && !staticSymbols.Any()) {
            diagnostics.Push(Error.InvalidStaticReference(expression.location, expression.identifier.text));
            return new BoundErrorExpression();
        }

        BoundType boundType = null;

        var symbol = staticAccess ? staticSymbols.FirstOrDefault() : instanceSymbols.FirstOrDefault();

        if (symbol is FieldSymbol f)
            boundType = f.type;
        else if (symbol is MethodSymbol m)
            boundType = BoundType.CreateFunc(m.parameters, m.type);

        return new BoundMemberAccessExpression(
            operand,
            symbol,
            BoundType.CopyWith(boundType, isConstantReference: false, isReference: true),
            isNullCondition,
            staticAccess
        );
    }

    private BoundExpression BindTypeOfExpression(TypeOfExpressionSyntax expression) {
        var type = BindType(expression.type);
        return new BoundTypeOfExpression(type);
    }

    private BoundExpression BindReferenceExpression(ReferenceExpressionSyntax expression) {
        var boundExpression = BindExpression(expression.expression);

        if (boundExpression is not BoundVariableExpression and not BoundMemberAccessExpression) {
            diagnostics.Push(Error.CannotReferenceNonField(expression.expression.location));
            return new BoundErrorExpression();
        }

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

        var type = boundOperand.type;

        if (type.isConstant) {
            string name = null;

            if (boundOperand is BoundVariableExpression v)
                name = v.variable.name;
            else if (boundOperand is BoundMemberAccessExpression m)
                name = (m.member as BoundVariableExpression).variable.name;

            diagnostics.Push(Error.ConstantAssignment(expression.operatorToken.location, name, false));

            return new BoundErrorExpression();
        }

        var boundOp = BoundPostfixOperator.Bind(expression.operatorToken.kind, boundOperand.type);

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

        var type = boundOperand.type;

        if (type.isConstant) {
            string name = null;

            if (boundOperand is BoundVariableExpression v)
                name = v.variable.name;
            else if (boundOperand is BoundMemberAccessExpression m)
                name = (m.member as BoundVariableExpression).variable.name;

            diagnostics.Push(Error.ConstantAssignment(expression.operatorToken.location, name, false));

            return new BoundErrorExpression();
        }

        var boundOp = BoundPrefixOperator.Bind(expression.operatorToken.kind, boundOperand.type);

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
        var boundExpression = BindExpression(expression.expression);

        if (boundExpression is BoundErrorExpression)
            return boundExpression;

        if (boundExpression.type.dimensions > 0) {
            var index = BindCast(
                expression.index.location, BindExpression(expression.index), BoundType.NullableInt
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
        var boundExpression = BindExpression(expression.expression, called: true);
        BoundExpression receiver = new BoundEmptyExpression();

        if (boundExpression is BoundMemberAccessExpression ma) {
            receiver = ma.expression;
            boundExpression = ma.member;
        }

        if (boundExpression is BoundMethodGroup mg) {
            if (!PartiallyBindArgumentList(expression.argumentList, out var arguments))
                return new BoundErrorExpression();

            var result = _overloadResolution.MethodOverloadResolution(
                mg.methods,
                arguments,
                mg.name,
                expression.expression,
                expression.argumentList
            );

            if (!result.succeeded)
                return new BoundErrorExpression();

            return new BoundCallExpression(receiver, result.bestOverload, result.arguments);
        }

        diagnostics.Push(Error.CannotCallNonMethod(expression.expression.location));
        return new BoundErrorExpression();
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

    private bool BindTemplateArgumentList(
        TemplateArgumentListSyntax argumentList,
        out ImmutableArray<(string, BoundConstant)> templateArguments) {
        var saved = _flags;
        _flags |= BinderFlags.TemplateArgumentList;

        bool result;

        if (argumentList is null) {
            templateArguments = ImmutableArray<(string, BoundConstant)>.Empty;
            result = true;
        } else {
            result = PartiallyBindArguments(argumentList.arguments, out var arguments, true);
            var builder = ImmutableArray.CreateBuilder<(string, BoundConstant)>();

            foreach (var argument in arguments)
                builder.Add((argument.Item1, argument.Item2.constantValue));

            templateArguments = builder.ToImmutable();
        }

        _flags = saved;
        return result;
    }

    private bool PartiallyBindArguments(
        SeparatedSyntaxList<ArgumentSyntax> arguments,
        out ImmutableArray<(string, BoundExpression)> boundArguments,
        bool mustBeConstant = false) {
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

            var boundExpression = BindExpression(arguments[i].expression);

            if (boundExpression is BoundEmptyExpression)
                boundExpression = new BoundLiteralExpression(null, true);

            if (mustBeConstant && boundExpression.constantValue is null) {
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
        var boundRight = BindExpression(expression.right);

        if (boundLeft.type.typeSymbol == TypeSymbol.Error || boundRight.type.typeSymbol == TypeSymbol.Error)
            return new BoundErrorExpression();

        var boundOp = BoundBinaryOperator.Bind(expression.operatorToken.kind, boundLeft.type, boundRight.type);

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

    private BoundExpression BindEmptyExpression(EmptyExpressionSyntax _) {
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
                name = (m.member as BoundVariableExpression).variable.name;

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
