using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed class MethodCompiler : SymbolVisitor<TypeCompilationState, object> {
    private readonly Compilation _compilation;
    private readonly bool _emitting;
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly MethodSymbol _entryPoint;
    private readonly MethodSymbol _updatePoint;
    private readonly Dictionary<MethodSymbol, BoundBlockStatement> _methodBodies;
    private readonly ArrayBuilder<NamedTypeSymbol> _types;
    private readonly Predicate<Symbol> _filter;

    private MethodCompiler(
        Compilation compilation,
        Dictionary<MethodSymbol, BoundBlockStatement> methodBodiesBeingBuilt,
        BelteDiagnosticQueue diagnostics,
        MethodSymbol entryPoint,
        MethodSymbol updatePoint,
        Predicate<Symbol> filter,
        bool emitting) {
        _compilation = compilation;
        _diagnostics = diagnostics;
        _entryPoint = entryPoint;
        _updatePoint = updatePoint;
        _filter = filter;
        _emitting = emitting;
        _types = ArrayBuilder<NamedTypeSymbol>.GetInstance();
        _methodBodies = methodBodiesBeingBuilt;
    }

    internal static BoundProgram CompileMethodBodies(
        Compilation compilation,
        BelteDiagnosticQueue diagnostics,
        Predicate<Symbol> filter,
        bool emitting) {
        var globalNamespace = compilation.globalNamespaceInternal;
        var methodBodiesBeingBuilt = new Dictionary<MethodSymbol, BoundBlockStatement>();
        var entryPoint = GetEntryPoint(compilation, diagnostics);
        var updatePoint = GetUpdatePoint(compilation, diagnostics);

        if (updatePoint is not null && !entryPoint.containingType.Equals(updatePoint.containingType))
            diagnostics.Push(Error.SeparateMainAndUpdate(updatePoint.location));

        var methodCompiler = new MethodCompiler(
            compilation,
            methodBodiesBeingBuilt,
            diagnostics,
            entryPoint,
            updatePoint,
            filter,
            emitting
        );

        methodCompiler.CompileNamespace(globalNamespace);
        return methodCompiler.CreateBoundProgram();
    }

    private static MethodSymbol GetEntryPoint(Compilation compilation, BelteDiagnosticQueue diagnostics) {
        return compilation.GetEntryPoint(diagnostics);
    }

    private static MethodSymbol GetUpdatePoint(Compilation compilation, BelteDiagnosticQueue diagnostics) {
        return compilation.GetUpdatePoint(diagnostics);
    }

    private BoundProgram CreateBoundProgram() {
        return new BoundProgram(
            _methodBodies.ToImmutableDictionary(),
            _types.ToImmutableAndFree(),
            _entryPoint,
            _updatePoint,
            _compilation.previous?.boundProgram
        );
    }

    private void CompileNamespace(NamespaceSymbol symbol) {
        foreach (var member in symbol.GetMembersUnordered())
            member.Accept(this, null);
    }

    internal override object VisitNamedType(NamedTypeSymbol symbol, TypeCompilationState _) {
        if (!PassesFilter(_filter, symbol))
            return null;

        CompileNamedType(symbol);
        return null;
    }

    private void CompileNamedType(NamedTypeSymbol symbol) {
        _types.Add(symbol);

        var state = new TypeCompilationState(symbol, _compilation);
        var members = symbol.GetMembers();
        var processedInitializers = new Binder.ProcessedFieldInitializers();

        var sourceType = symbol as SourceMemberContainerTypeSymbol;

        if (sourceType is not null) {
            Binder.BindFieldInitializers(
                _compilation,
                sourceType.initializers,
                _diagnostics,
                ref processedInitializers
            );
        }

        for (var ordinal = 0; ordinal < members.Length; ordinal++) {
            var member = members[ordinal];

            if (!PassesFilter(_filter, member))
                continue;

            switch (member) {
                case NamedTypeSymbol:
                    member.Accept(this, state);
                    break;
                case MethodSymbol m:
                    var initializers = m.methodKind == MethodKind.Constructor ? processedInitializers : default;
                    CompileMethod(m, ordinal, ref initializers, state);
                    break;
                case FieldSymbol f:
                    if (f.isConstExpr)
                        f.GetConstantValue(ConstantFieldsInProgress.Empty);

                    break;
            }
        }

        if (state.synthesizedMethods is not null) {
            foreach (var synthesizedMethod in state.synthesizedMethods)
                _methodBodies.Add(synthesizedMethod.Item1, synthesizedMethod.Item2);
        }

        state.Free();
    }

    private void CompileMethod(
        MethodSymbol method,
        int methodOrdinal,
        ref Binder.ProcessedFieldInitializers processedInitializers,
        TypeCompilationState state) {
        if (method.isAbstract)
            return;

        var currentDiagnostics = BelteDiagnosticQueue.GetInstance();
        BoundBlockStatement analyzedInitializers = null;

        var includeInitializers = method.IncludeFieldInitializersInBody();
        var includeNonEmptyInitializers = includeInitializers &&
            !processedInitializers.boundInitializers.IsDefaultOrEmpty;

        if (includeNonEmptyInitializers && processedInitializers.loweredInitializers is null) {
            analyzedInitializers = InitializerRewriter.RewriteConstructor(
                processedInitializers.boundInitializers,
                method
            );
        }

        var body = BindMethodBody(
            method,
            state,
            currentDiagnostics,
            includeInitializers,
            analyzedInitializers
        );

        if (!_emitting || currentDiagnostics.AnyErrors()) {
            _diagnostics.PushRangeAndFree(currentDiagnostics);
            _methodBodies.Add(method, body);
            return;
        }

        var loweredBody = LowerBody(
            method,
            methodOrdinal,
            body,
            state,
            _compilation.previousAnalyses,
            _compilation.options.buildMode,
            currentDiagnostics
        );

        _diagnostics.PushRangeAndFree(currentDiagnostics);
        _methodBodies.Add(method, loweredBody);
    }

    private static BoundBlockStatement LowerBody(
        MethodSymbol method,
        int methodOrdinal,
        BoundBlockStatement body,
        TypeCompilationState state,
        List<LocalFunctionRewriter.Analysis> previousAnalyses,
        BuildMode buildMode,
        BelteDiagnosticQueue currentDiagnostics) {
        var loweredBody = Lowerer.Lower(method, body, currentDiagnostics);

        // ? C# handles closure a little different than we do so we just let the C# compiler handle that itself
        if (buildMode != BuildMode.CSharpTranspile) {
            // ? TODO Why do we have a substitutedMethodSymbol parameter here if it's never supplied?
            loweredBody = LocalFunctionRewriter.Rewrite(
                loweredBody,
                state.type,
                method,
                methodOrdinal,
                null,
                state,
                previousAnalyses,
                currentDiagnostics
            );
        }

        return loweredBody;
    }

    private static BoundBlockStatement BindMethodBody(
        MethodSymbol method,
        TypeCompilationState state,
        BelteDiagnosticQueue diagnostics,
        bool includeInitializers,
        BoundBlockStatement initializersBody) {
        BoundBlockStatement body = null;
        var syntax = method.GetNonNullSyntaxNode();
        initializersBody ??= new BoundBlockStatement(syntax, [], [], []);
        var builder = ArrayBuilder<BoundStatement>.GetInstance();
        BelteSyntaxNode syntaxNode = null;

        if (method is SourceMemberMethodSymbol sourceMethod) {
            syntaxNode = sourceMethod.syntaxNode;
            var bodyBinder = sourceMethod.TryGetBodyBinder(null, state.compilation.options.isScript);

            if (bodyBinder is null)
                return null;

            var methodBody = bodyBinder.BindMethodBody(syntaxNode, diagnostics);

            switch (methodBody) {
                case BoundConstructorMethodBody constructor:
                    body = constructor.body;

                    if (constructor.initializer is BoundExpressionStatement expressionStatement) {
                        ReportConstructorInitializerCycles(
                            method,
                            expressionStatement.expression,
                            state,
                            syntaxNode,
                            diagnostics
                        );

                        if (includeInitializers)
                            builder.Add(initializersBody);

                        builder.Add(constructor.initializer);

                        if (body is not null)
                            builder.Add(body);

                        body = new BoundBlockStatement(syntax, builder.ToImmutableAndFree(), constructor.locals, []);
                    }

                    return body;
                case BoundNonConstructorMethodBody nonConstructor:
                    body = nonConstructor.body;
                    break;
                case BoundBlockStatement block:
                    body = block;
                    break;
                default:
                    throw ExceptionUtilities.UnexpectedValue(methodBody.kind);
            }
        }

        var constructorInitializer = BindImplicitConstructorInitializerIfAny(method, state, syntaxNode, diagnostics);

        if (includeInitializers)
            builder.Add(initializersBody);

        if (constructorInitializer is not null)
            builder.Add(constructorInitializer);

        if (body is not null)
            builder.Add(body);

        return new BoundBlockStatement(syntax, builder.ToImmutableAndFree(), [], []);
    }

    private static BoundStatement BindImplicitConstructorInitializerIfAny(
        MethodSymbol method,
        TypeCompilationState state,
        SyntaxNode syntax,
        BelteDiagnosticQueue diagnostics) {
        if (method.methodKind == MethodKind.Constructor) {
            var compilation = method.declaringCompilation;
            var call = Binder.BindImplicitConstructorInitializer(method, diagnostics, compilation);

            if (call is not null) {
                ReportConstructorInitializerCycles(method, call, state, syntax, diagnostics);
                return new BoundExpressionStatement(call.syntax, call);
            }
        }

        return null;
    }

    private static void ReportConstructorInitializerCycles(
        MethodSymbol method,
        BoundExpression expression,
        TypeCompilationState state,
        SyntaxNode syntax,
        BelteDiagnosticQueue diagnostics) {
        var call = expression as BoundCallExpression;

        if (call is not null &&
            call.method != method &&
            TypeSymbol.Equals(call.method.containingType, method.containingType, TypeCompareKind.ConsiderEverything)) {
            state.ReportConstructorInitializerCycles(method, call.method, syntax, diagnostics);
        }
    }

    private static bool PassesFilter(Predicate<Symbol> filter, Symbol symbol) {
        return (filter is null) || filter(symbol);
    }
}
