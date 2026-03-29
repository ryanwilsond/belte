using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.FlowAnalysis;
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
    private readonly Dictionary<MethodSymbol, BoundBlockStatement> _methodBodies;
    private readonly Dictionary<MethodSymbol, EvaluatorSlotManager> _methodLayouts;
    private readonly MultiDictionary<NamedTypeSymbol, NamedTypeSymbol> _synthesizedNestedTypes;
    private readonly ArrayBuilder<NamedTypeSymbol> _types;
    private readonly Dictionary<NamedTypeSymbol, EvaluatorSlotManager> _typeLayouts;
    private readonly Predicate<Symbol> _filter;

    private Dictionary<FieldSymbol, NamedTypeSymbol> _lazyFixedImplementationTypes;
    private MethodSymbol _entryPoint;
    private MethodSymbol _updatePoint;

    private MethodCompiler(
        Compilation compilation,
        Dictionary<MethodSymbol, BoundBlockStatement> methodBodiesBeingBuilt,
        BelteDiagnosticQueue diagnostics,
        MethodSymbol entryPoint,
        MethodSymbol updatePoint,
        Dictionary<NamedTypeSymbol, EvaluatorSlotManager> typeLayouts,
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
        _methodLayouts = [];
        _typeLayouts = typeLayouts;
        _synthesizedNestedTypes = [];
    }

    internal static BoundProgram CompileMethodBodies(
        Compilation compilation,
        BelteDiagnosticQueue diagnostics,
        Predicate<Symbol> filter) {
        var emittingToDll = compilation.options.outputKind == OutputKind.DynamicallyLinkedLibrary;
        var transpiling = compilation.options.buildMode == BuildMode.CSharpTranspile;

        var globalNamespace = compilation.globalNamespaceInternal;

        var typeLayouts = EvaluatorTypeLayoutVisitor.CreateTypeLayouts(globalNamespace);
        var previousLayouts = compilation?.previous?.boundProgram?.typeLayouts;

        if (previousLayouts is not null) {
            foreach (var layout in previousLayouts)
                typeLayouts.TryAdd(layout.Key, layout.Value);
        }

        var methodBodiesBeingBuilt = new Dictionary<MethodSymbol, BoundBlockStatement>();
        var entryPoint = emittingToDll ? null : GetEntryPoint(compilation, diagnostics);
        var updatePoint = emittingToDll ? null : GetUpdatePoint(compilation, entryPoint, diagnostics);

        if (!compilation.options.isScript) {
            if (updatePoint is not null && !entryPoint.containingType.Equals(updatePoint.containingType))
                diagnostics.Push(Error.SeparateMainAndUpdate(updatePoint.location));
        }

        var methodCompiler = new MethodCompiler(
            compilation,
            methodBodiesBeingBuilt,
            diagnostics,
            entryPoint,
            updatePoint,
            typeLayouts,
            filter,
            !transpiling
        );

        methodCompiler.CompileNamespace(globalNamespace);

        if (!methodCompiler._diagnostics.AnyErrors())
            methodCompiler.ComputeCompileTimeExpressions();

        if (compilation.options.isScript && methodCompiler._updatePoint is null)
            methodCompiler._updatePoint = compilation.GetLateScriptUpdatePoint(methodCompiler._methodBodies);

        return methodCompiler.CreateBoundProgram();
    }

    private static MethodSymbol GetEntryPoint(Compilation compilation, BelteDiagnosticQueue diagnostics) {
        return compilation.GetEntryPoint(diagnostics);
    }

    private static MethodSymbol GetUpdatePoint(
        Compilation compilation,
        MethodSymbol entryPoint,
        BelteDiagnosticQueue diagnostics) {
        return compilation.GetUpdatePoint(entryPoint, diagnostics);
    }

    private BoundProgram CreateBoundProgram() {
        return new BoundProgram(
            _compilation,
            _methodBodies.ToImmutableDictionary(),
            _methodLayouts.ToImmutableDictionary(),
            _types.ToImmutableAndFree(),
            _typeLayouts.ToImmutableDictionary(),
            _synthesizedNestedTypes,
            _lazyFixedImplementationTypes is null ? [] : _lazyFixedImplementationTypes.ToImmutableDictionary(),
            _entryPoint,
            _updatePoint,
            _compilation.previous?.boundProgram
        );
    }

    private void ComputeCompileTimeExpressions() {
        var newMethodBodies = new Dictionary<MethodSymbol, BoundBlockStatement>();
        var newMethodLayouts = new Dictionary<MethodSymbol, EvaluatorSlotManager>();

        if (!_compilation.options.buildMode.Evaluating()) {
            var current = _methodBodies;
            var currentCompilation = _compilation;

            while (currentCompilation is not null) {
                foreach (var (key, value) in current) {
                    newMethodBodies.Add(key, EvaluatorSlotRewriter.Rewrite(
                        key,
                        value,
                        _typeLayouts,
                        _compilation.previous?.boundProgram,
                        out var manager
                    ));

                    newMethodLayouts.Add(key, manager);
                }

                // We have to recompute libraries because they aren't build with evaluator slots in mind
                currentCompilation = currentCompilation.previous;
                current = currentCompilation?.boundProgram?.methodBodies?.ToDictionary();
            }
        } else {
            newMethodBodies = _methodBodies;
            newMethodLayouts = _methodLayouts;
        }

        var boundProgram = new BoundProgram(
            _compilation,
            newMethodBodies.ToImmutableDictionary(),
            newMethodLayouts.ToImmutableDictionary(),
            _types.ToImmutableArray(),
            _typeLayouts.ToImmutableDictionary(),
            _synthesizedNestedTypes,
            _lazyFixedImplementationTypes is null ? [] : _lazyFixedImplementationTypes.ToImmutableDictionary(),
            null,
            null,
            _compilation.previous?.boundProgram
        );

        var evaluatorContext = new EvaluatorContext(_compilation.options);

        foreach (var (method, body) in _methodBodies) {
            if (body is not null) {
                var loweredBody = CompileTimeLowerer.Lower(
                    method,
                    body,
                    _diagnostics,
                    boundProgram,
                    evaluatorContext,
                    _compilation
                );

                _methodBodies[method] = (BoundBlockStatement)loweredBody;
            }
        }
    }

    private void CompileNamespace(NamespaceSymbol symbol) {
        foreach (var member in symbol.GetMembersUnordered())
            member.Accept(this, null);
    }

    internal override object VisitNamespace(NamespaceSymbol symbol, TypeCompilationState _) {
        if (!PassesFilter(_filter, symbol))
            return null;

        CompileNamespace(symbol);
        return null;
    }

    internal override object VisitNamedType(NamedTypeSymbol symbol, TypeCompilationState _) {
        if (!PassesFilter(_filter, symbol))
            return null;

        CompileNamedType(symbol);
        return null;
    }

    private void CompileNamedType(NamedTypeSymbol symbol) {
        _types.Add(symbol);

        var state = new TypeCompilationState(symbol, _compilation, _typeLayouts);
        var members = symbol.GetMembers();
        var processedInstanceInitializers = new Binder.ProcessedFieldInitializers();
        var processedStaticInitializers = new Binder.ProcessedFieldInitializers();

        var sourceType = symbol as SourceMemberContainerTypeSymbol;

        if (sourceType is not null) {
            Binder.BindFieldInitializers(
                _compilation,
                sourceType.staticInitializers,
                _diagnostics,
                ref processedStaticInitializers
            );

            Binder.BindFieldInitializers(
                _compilation,
                sourceType.instanceInitializers,
                _diagnostics,
                ref processedInstanceInitializers
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
                    var processedInitializers = (m.methodKind == MethodKind.Constructor) ? processedInstanceInitializers
                        : (m.methodKind == MethodKind.StaticConstructor) ? processedStaticInitializers : default;
                    CompileMethod(m, ordinal, ref processedInitializers, state);
                    break;
                case FieldSymbol f:
                    if (f.isConstExpr)
                        f.GetConstantValue(ConstantFieldsInProgress.Empty);

                    if (f.isFixedSizeBuffer)
                        SetFixedImplementationType(f as SourceMemberFieldSymbol);

                    break;
            }
        }

        if (state.synthesizedTypes is not null) {
            foreach (var synthesizedType in state.synthesizedTypes) {
                _types.Add(synthesizedType.Item2);
                _synthesizedNestedTypes.Add(synthesizedType.Item1, synthesizedType.Item2);
            }
        }

        if (state.synthesizedMethods is not null) {
            foreach (var synthesizedMethod in state.synthesizedMethods)
                _methodBodies.Add(synthesizedMethod.Item1, synthesizedMethod.Item2);
        }

        if (state.methodLayouts is not null) {
            foreach (var methodLayout in state.methodLayouts)
                _methodLayouts.Add(methodLayout.Item1, methodLayout.Item2);
        }

        state.Free();
    }

    private void SetFixedImplementationType(SourceMemberFieldSymbol field) {
        if (_lazyFixedImplementationTypes is null)
            Interlocked.CompareExchange(ref _lazyFixedImplementationTypes, [], null);

        lock (_lazyFixedImplementationTypes) {
            if (_lazyFixedImplementationTypes.TryGetValue(field, out _))
                return;

            var result = new FixedFieldImplementationType(field);
            _lazyFixedImplementationTypes.Add(field, result);
        }
    }

    private void CompileMethod(
        MethodSymbol method,
        int methodOrdinal,
        ref Binder.ProcessedFieldInitializers processedInitializers,
        TypeCompilationState state) {
        if (method.isAbstract)
            return;

        var oldImportChain = state.currentImportChain;

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

            processedInitializers.hasErrors = processedInitializers.hasErrors || analyzedInitializers.hasErrors;

            RefSafetyAnalysis.Analyze(
                method,
                new BoundBlockStatement(analyzedInitializers.syntax, analyzedInitializers.statements, [], []),
                currentDiagnostics
            );
        }

        var body = BindMethodBody(
            method,
            state,
            currentDiagnostics,
            includeInitializers,
            analyzedInitializers,
            ref _entryPoint,
            out var importChain
        );

        if (body is null || !_emitting || currentDiagnostics.AnyErrors()) {
            _diagnostics.PushRangeAndFree(currentDiagnostics);
            _methodBodies.Add(method, body);
            return;
        }

        importChain ??= processedInitializers.firstImportChain;
        state.currentImportChain = importChain;

        var loweredBody = LowerBody(
            method,
            methodOrdinal,
            body,
            state,
            _compilation.previousAnalyses,
            currentDiagnostics
        );

        if (!ControlFlowGraph.AllPathsReturn(loweredBody))
            currentDiagnostics.Push(Error.NotAllPathsReturn(method.location));

        if (_compilation.options.buildMode.Evaluating()) {
            loweredBody = EvaluatorSlotRewriter.Rewrite(
                method,
                loweredBody,
                _typeLayouts,
                _compilation.previous?.boundProgram,
                out var slotManager
            );

            _methodLayouts.Add(method, slotManager);
        }

        _diagnostics.PushRangeAndFree(currentDiagnostics);
        state.currentImportChain = oldImportChain;
        _methodBodies.Add(method, loweredBody);
    }

    private static BoundBlockStatement LowerBody(
        MethodSymbol method,
        int methodOrdinal,
        BoundBlockStatement body,
        TypeCompilationState state,
        List<LocalFunctionRewriter.Analysis> previousAnalyses,
        BelteDiagnosticQueue currentDiagnostics) {
        var loweredBody = Lowerer.Lower(method, body, currentDiagnostics);

        loweredBody = LocalFunctionRewriter.Rewrite(
            loweredBody,
            state.type,
            method,
            methodOrdinal,
            null,
            state,
            previousAnalyses,
            currentDiagnostics,
            null // TODO When do we want to use this?
        );

        loweredBody = Optimizer.RemoveDeadCode(loweredBody, currentDiagnostics);

        return loweredBody;
    }

    internal static BoundBlockStatement BindSynthesizedMethodBody(
        MethodSymbol method,
        TypeCompilationState compilationState,
        BelteDiagnosticQueue diagnostics) {
        MethodSymbol _1 = null;

        return BindMethodBody(
            method,
            compilationState,
            diagnostics,
            includeInitializers: false,
            initializersBody: null,
            entryPoint: ref _1,
            importChain: out _
        );
    }

    private static BoundBlockStatement BindMethodBody(
        MethodSymbol method,
        TypeCompilationState state,
        BelteDiagnosticQueue diagnostics,
        bool includeInitializers,
        BoundBlockStatement initializersBody,
        ref MethodSymbol entryPoint,
        out ImportChain importChain) {
        BoundBlockStatement body = null;
        importChain = null;
        var syntax = method.GetNonNullSyntaxNode();
        initializersBody ??= new BoundBlockStatement(syntax, [], [], []);
        var builder = ArrayBuilder<BoundStatement>.GetInstance();
        BelteSyntaxNode syntaxNode = null;

        if (method is SourceMemberMethodSymbol sourceMethod) {
            syntaxNode = sourceMethod.syntaxNode;

            if (sourceMethod.methodKind == MethodKind.StaticConstructor &&
                syntaxNode is ConstructorDeclarationSyntax constructorSyntax &&
                constructorSyntax.constructorInitializer is not null) {
                diagnostics.Push(Error.StaticConstructorWithInitializer(
                    constructorSyntax.constructorInitializer.thisOrBaseKeyword.location
                ));
            }

            if (sourceMethod.isExtern)
                return null;

            var bodyBinder = sourceMethod.TryGetBodyBinder(null, state.compilation.options.isScript);

            if (bodyBinder is null)
                return null;

            importChain = bodyBinder.importChain;

            var methodBody = bodyBinder.BindMethodBody(syntaxNode, diagnostics);

            RefSafetyAnalysis.Analyze(method, methodBody, diagnostics);

            switch (methodBody) {
                case BoundConstructorMethodBody constructor:
                    body = constructor.body;

                    if (sourceMethod.methodKind == MethodKind.Constructor &&
                        constructor.initializer is BoundExpressionStatement expressionStatement) {
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

        if (method == entryPoint && method is SynthesizedEntryPoint synth && body.localFunctions.Length > 0) {
            var candidateLocals = ArrayBuilder<MethodSymbol>.GetInstance();

            foreach (var local in body.localFunctions) {
                if (Compilation.HasEntryPointSignature(local))
                    candidateLocals.Add(local);
            }

            if (candidateLocals.Count > 0) {
                var bodyHasLogic = body.statements.Any(t => t.kind != BoundKind.LocalFunctionStatement);

                if (bodyHasLogic)
                    candidateLocals.Add(synth);

                var newEntryPoint = Compilation.SelectEntryPoint(
                    bodyHasLogic ? synth : null,
                    candidateLocals.ToImmutableAndFree(),
                    diagnostics,
                    false
                );

                if (newEntryPoint is not null && !bodyHasLogic) {
                    body = new BoundBlockStatement(
                        syntax,
                        [
                            ..body.statements,
                            BoundFactory.Statement(syntax, BoundFactory.Call(syntax, newEntryPoint)),
                            new BoundReturnStatement(syntax, RefKind.None, null),
                        ],
                        [],
                        []
                    );
                }
            }
        }

        if (body is not null)
            builder.Add(body);

        return new BoundBlockStatement(
            syntax,
            builder.ToImmutableAndFree(),
            body?.locals ?? [],
            body?.localFunctions ?? []
        );
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
