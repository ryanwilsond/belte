using System;
using System.Collections.Concurrent;
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

internal sealed partial class MethodCompiler : SymbolVisitor<TypeCompilationState, object> {
    private readonly Compilation _compilation;
    private readonly BelteDiagnosticQueue _diagnostics;
    private readonly ConcurrentDictionary<MethodSymbol, BoundBlockStatement> _methodBodies;
    private readonly ConcurrentDictionary<MethodSymbol, EvaluatorSlotManager> _methodLayouts;
    private readonly MultiDictionary<NamedTypeSymbol, NamedTypeSymbol> _synthesizedNestedTypes;
    private readonly ArrayBuilder<NamedTypeSymbol> _types;
    private readonly ImmutableDictionary<NamedTypeSymbol, EvaluatorSlotManager>.Builder _typeLayouts;
    private readonly Dictionary<NamedTypeSymbol, SynthesizedEnumMethodContainer> _enumMethodContainerTypes;
    private readonly Predicate<Symbol> _filter;
    private readonly bool _collectSymbols;

    private ConcurrentQueue<Action> _workQueue;
    private ManualResetEventSlim _signal;
    private int _pending;
    private volatile bool _stopping;
    private AutoResetEvent _workAvailable;
    private ManualResetEventSlim _done;

    private ImmutableDictionary<FieldSymbol, NamedTypeSymbol>.Builder _lazyFixedImplementationTypes;
    private MethodSymbol _entryPoint;
    private MethodSymbol _updatePoint;

    private bool _sawCompileTimeExpression;

    private MethodCompiler(
        Compilation compilation,
        BelteDiagnosticQueue diagnostics,
        MethodSymbol entryPoint,
        MethodSymbol updatePoint,
        ImmutableDictionary<NamedTypeSymbol, EvaluatorSlotManager>.Builder typeLayouts,
        Dictionary<NamedTypeSymbol, SynthesizedEnumMethodContainer> enumMethodContainerTypes,
        Predicate<Symbol> filter,
        bool collectSymbols) {
        _compilation = compilation;
        _diagnostics = diagnostics;
        _entryPoint = entryPoint;
        _updatePoint = updatePoint;
        _filter = filter;
        _types = ArrayBuilder<NamedTypeSymbol>.GetInstance();
        _methodBodies = [];
        _methodLayouts = [];
        _typeLayouts = typeLayouts;
        _synthesizedNestedTypes = [];
        _collectSymbols = collectSymbols;
        _enumMethodContainerTypes = enumMethodContainerTypes;
    }

    internal bool transpiling => _compilation.options.buildMode == BuildMode.CSharpTranspile;

    internal bool emitting => _compilation.options.buildMode.Emitting();

    internal bool evaluating => _compilation.options.buildMode.Evaluating();

    internal static BoundProgram CompileMethodBodies(
        Compilation compilation,
        BelteDiagnosticQueue diagnostics,
        Predicate<Symbol> filter,
        bool skipEntryPoint = false,
        bool collectSymbols = false) {
        var emittingToDll = compilation.options.outputKind == OutputKind.DynamicallyLinkedLibrary;
        var globalNamespace = compilation.globalNamespaceInternal;

        var typeLayouts = ImmutableDictionary.CreateBuilder<NamedTypeSymbol, EvaluatorSlotManager>();

        // Unfortunately we have to do this even if we don't use it
        // We have to allow any future programs to use layouts from this one
        EvaluatorTypeLayoutVisitor.CreateTypeLayouts(typeLayouts, globalNamespace);
        var previousProgram = compilation?.previous?.boundProgram;
        var previousLayouts = previousProgram?.typeLayouts;

        if (previousLayouts is not null) {
            foreach (var layout in previousLayouts)
                typeLayouts.TryAdd(layout.Key, layout.Value);
        }

        // TODO If this gets expensive we can store it in the bound program directly instead of recalculating
        // Figured that this usually will be really small
        var enumMethodContainerTypes = new Dictionary<NamedTypeSymbol, SynthesizedEnumMethodContainer>();

        if (previousProgram is not null) {
            var current = previousProgram;

            do {
                foreach (var type in current.types) {
                    if (type is SynthesizedEnumMethodContainer enumContainer)
                        enumMethodContainerTypes.Add(enumContainer.enumType, enumContainer);
                }

                current = current.previous;
            } while (current is not null);
        }

        var entryPoint = (emittingToDll || skipEntryPoint) ? null : GetEntryPoint(compilation, diagnostics);
        var updatePoint = (emittingToDll || skipEntryPoint) ? null : GetUpdatePoint(compilation, entryPoint, diagnostics);

        if (!compilation.options.isScript) {
            if (updatePoint is not null && !entryPoint.containingType.Equals(updatePoint.containingType))
                diagnostics.Push(Error.SeparateMainAndUpdate(updatePoint.location));
        }

        var methodCompiler = new MethodCompiler(
            compilation,
            diagnostics,
            entryPoint,
            updatePoint,
            typeLayouts,
            enumMethodContainerTypes,
            filter,
            collectSymbols
        );

        if (compilation.options.concurrentBuild) {
            methodCompiler._workQueue = new();
            methodCompiler._signal = new(false);
            methodCompiler._workAvailable = new(false);
            methodCompiler._done = new(false);

            methodCompiler.StartWorkers(compilation.options.maxCoreCount);
            methodCompiler.Enqueue(() => methodCompiler.CompileNamespace(globalNamespace));
            methodCompiler.WaitForCompletion();

            methodCompiler._stopping = true;
            methodCompiler._signal.Set();
        } else {
            methodCompiler.CompileNamespace(globalNamespace);
        }

        if (!diagnostics.AnyErrors() && methodCompiler._sawCompileTimeExpression)
            methodCompiler.ComputeCompileTimeExpressions();

        if (compilation.options.isScript && methodCompiler._updatePoint is null)
            methodCompiler._updatePoint = compilation.GetLateScriptUpdatePoint(methodCompiler._methodBodies);

        if (compilation.options.optimizationLevel == OptimizationLevel.Debug)
            methodCompiler.InjectSequencePoints();

        return methodCompiler.CreateBoundProgram();
    }

    private void StartWorkers(int count) {
        for (var i = 0; i < count; i++) {
            var thread = new Thread(WorkerLoop) {
                IsBackground = true
            };

            thread.Start();
        }
    }

    private void WorkerLoop() {
        while (true) {
            if (_stopping)
                return;

            if (_workQueue.TryDequeue(out var work)) {
                work();

                if (Interlocked.Decrement(ref _pending) == 0)
                    _done.Set();

                continue;
            }

            _workAvailable.WaitOne();
        }
    }

    private void Enqueue(Action work) {
        Interlocked.Increment(ref _pending);

        _workQueue.Enqueue(work);
        _workAvailable.Set();
    }

    private void WaitForCompletion() {
        _done.Wait();
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
            _typeLayouts.ToImmutable(),
            _synthesizedNestedTypes,
            _lazyFixedImplementationTypes is null ? [] : _lazyFixedImplementationTypes.ToImmutable(),
            _entryPoint,
            _updatePoint,
            _compilation.previous?.boundProgram
        );
    }

    private void ComputeCompileTimeExpressions() {
        ImmutableDictionary<MethodSymbol, BoundBlockStatement> newMethodBodies;
        ImmutableDictionary<MethodSymbol, EvaluatorSlotManager> newMethodLayouts;

        if (!_compilation.options.buildMode.Evaluating()) {
            var methodBodiesBuilder = ImmutableDictionary.CreateBuilder<MethodSymbol, BoundBlockStatement>();
            var methodLayoutsBuilder = ImmutableDictionary.CreateBuilder<MethodSymbol, EvaluatorSlotManager>();

            var current = _methodBodies.ToDictionary();
            var currentCompilation = _compilation;

            while (currentCompilation is not null) {
                foreach (var (key, value) in current) {
                    methodBodiesBuilder.Add(key, EvaluatorSlotRewriter.Rewrite(
                        key,
                        value,
                        _typeLayouts,
                        _compilation.previous?.boundProgram,
                        out var manager
                    ));

                    methodLayoutsBuilder.Add(key, manager);
                }

                // We have to recompute libraries because they aren't build with evaluator slots in mind
                currentCompilation = currentCompilation.previous;
                current = currentCompilation?.boundProgram?.methodBodies?.ToDictionary();
            }

            newMethodBodies = methodBodiesBuilder.ToImmutable();
            newMethodLayouts = methodLayoutsBuilder.ToImmutable();
        } else {
            newMethodBodies = _methodBodies.ToImmutableDictionary();
            newMethodLayouts = _methodLayouts.ToImmutableDictionary();
        }

        var boundProgram = new BoundProgram(
            _compilation,
            newMethodBodies,
            newMethodLayouts,
            _types.ToImmutable(),
            _typeLayouts.ToImmutable(),
            _synthesizedNestedTypes,
            _lazyFixedImplementationTypes is null ? [] : _lazyFixedImplementationTypes.ToImmutable(),
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

    private void InjectSequencePoints() {
        foreach (var (method, body) in _methodBodies) {
            if (body is not null)
                _methodBodies[method] = SequencePointInjector.Lower(body);
        }
    }

    private void CompileNamespace(NamespaceSymbol symbol) {
        foreach (var member in symbol.GetMembersUnordered())
            member.Accept(this, null);
    }

    internal override object VisitNamespace(NamespaceSymbol symbol, TypeCompilationState _) {
        if (!PassesFilter(_filter, symbol))
            return null;

        if (_compilation.options.concurrentBuild)
            Enqueue(() => CompileNamespace(symbol));
        else
            CompileNamespace(symbol);

        return null;
    }

    internal override object VisitNamedType(NamedTypeSymbol symbol, TypeCompilationState _) {
        if (!PassesFilter(_filter, symbol))
            return null;

        if (_compilation.options.concurrentBuild)
            Enqueue(() => CompileNamedType(symbol));
        else
            CompileNamedType(symbol);

        return null;
    }

    private void CompileNamedType(NamedTypeSymbol symbol) {
        lock (_types)
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

            var unions = sourceType.anonymousUnionFields;

            if (unions is not null) {
                lock (_synthesizedNestedTypes) {
                    foreach (var union in unions)
                        _synthesizedNestedTypes.Add(symbol, union.Key);
                }
            }
        }

        var fieldsRequiringAssignment = ArrayBuilder<FieldSymbol>.GetInstance();

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

                    if (_collectSymbols) {
                        f.type.VisitType(
                            SymbolCollector.VisitTypePredicate,
                            new SymbolCollectorArgument() { compiler = this, visited = [] },
                            true
                        );
                    }

                    if (f.definiteAssignmentError is not null && !(symbol.IsStructType() && f.type.HasDefaultValue()))
                        fieldsRequiringAssignment.Add(f);

                    break;
            }
        }

        if (state.synthesizedTypes is not null) {
            foreach (var synthesizedType in state.synthesizedTypes) {
                lock (_types)
                    _types.Add(synthesizedType.Item2);

                lock (_synthesizedNestedTypes)
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

        if (fieldsRequiringAssignment.Count > 0)
            state.ReportFieldsRequiringAssignment(fieldsRequiringAssignment, _diagnostics);

        fieldsRequiringAssignment.Free();
        state.Free();
    }

    private void SetFixedImplementationType(SourceMemberFieldSymbol field) {
        if (_lazyFixedImplementationTypes is null) {
            Interlocked.CompareExchange(
                ref _lazyFixedImplementationTypes,
                ImmutableDictionary.CreateBuilder<FieldSymbol, NamedTypeSymbol>(),
                null
            );
        }

        lock (_lazyFixedImplementationTypes) {
            if (_lazyFixedImplementationTypes.TryGetValue(field, out _))
                return;

            var result = new FixedFieldImplementationType(field);
            _lazyFixedImplementationTypes.Add(field, result);
        }
    }

    internal MethodSymbol GetEnumMethod(NamedTypeSymbol enumType, MethodSymbol originalMethod) {
        if (_enumMethodContainerTypes.TryGetValue(enumType, out var container))
            return container.methodMap[originalMethod];

        var synthesizedContainer = new SynthesizedEnumMethodContainer(enumType, enumType.containingNamespace);

        lock (_enumMethodContainerTypes)
            _enumMethodContainerTypes.TryAdd(enumType, synthesizedContainer);

        lock (_types)
            _types.Add(synthesizedContainer);

        return synthesizedContainer.methodMap[originalMethod];
    }

    private void CompileMethod(
        MethodSymbol method,
        int methodOrdinal,
        ref Binder.ProcessedFieldInitializers processedInitializers,
        TypeCompilationState state) {
        if (method.isAbstract || method.originalDefinition is PEMethodSymbol or SourceStateMethodSymbol)
            return;

        var methodDiagnostics = CompileMethodCore(method, methodOrdinal, ref processedInitializers, state);

        if (methodDiagnostics is not null)
            _diagnostics.PushRangeAndFree(methodDiagnostics);
    }

    private BelteDiagnosticQueue CompileMethodCore(
        MethodSymbol method,
        int methodOrdinal,
        ref Binder.ProcessedFieldInitializers processedInitializers,
        TypeCompilationState state,
        bool isStateMethod = false,
        BoundBlockStatement partialTargetBody = null) {
        if (_methodBodies.ContainsKey(method))
            return null;

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

            processedInitializers.hasErrors = processedInitializers.hasErrors || analyzedInitializers.hasAnyErrors;

            RefSafetyAnalysis.Analyze(
                method,
                new BoundBlockStatement(analyzedInitializers.syntax, analyzedInitializers.statements, [], []),
                currentDiagnostics
            );
        }

        var outInitializers = InitializerRewriter.RewriteOutParameters(method);

        var body = BindMethodBody(
            method,
            state,
            currentDiagnostics,
            includeInitializers,
            analyzedInitializers,
            outInitializers,
            ref _entryPoint,
            out var importChain
        );

        if (body is null || currentDiagnostics.AnyErrors()) {
            _methodBodies.Add(method, body);
            return currentDiagnostics;
        }

        importChain ??= processedInitializers.firstImportChain;
        state.currentImportChain = importChain;

        if (body is not null)
            DiagnosticPass.ReportDiagnostics(body, currentDiagnostics);

        if (currentDiagnostics.AnyErrors())
            return currentDiagnostics;

        var loweredBody = LowerBody(
            method,
            methodOrdinal,
            body,
            state,
            _compilation.previousAnalyses,
            currentDiagnostics,
            ref _entryPoint,
            out var sawCompileTimeExpression
        );

        if (method.methodKind == MethodKind.Ordinary && method.containingType.IsEnumType())
            method = GetEnumMethod(method.containingType, method);

        _sawCompileTimeExpression |= sawCompileTimeExpression;

        var controlFlowGraph = ControlFlowGraph.Create(method, loweredBody);
        var assignments = controlFlowGraph.CheckDefiniteAssignment(currentDiagnostics);

        foreach (var field in method.initFields) {
            if (!assignments.Contains(field))
                currentDiagnostics.Push(Error.MissingFieldInit(method.location, field));
        }

        if ((object)state.type == _entryPoint?.containingType) {
            if (method == _entryPoint)
                state.AddConstructorDefiniteAssignments(method.isStatic, assignments);
            else if (method.IsConstructor() && !method.HasThisConstructorInitializer())
                state.OrConstructorDefiniteAssignments(method.methodKind == MethodKind.StaticConstructor, assignments);
        } else if (method.IsConstructor() && !method.HasThisConstructorInitializer()) {
            state.AddConstructorDefiniteAssignments(method.methodKind == MethodKind.StaticConstructor, assignments);
        }

        if (isStateMethod)
            loweredBody = StateMethodRewriter.Merge(method, partialTargetBody, loweredBody);

        if (method.hasReversalState) {
            CompileMethodCore(
                method.stateMethod,
                methodOrdinal + 1,
                ref processedInitializers,
                state,
                true,
                loweredBody
            );
        }

        if (!transpiling) {
            if (!controlFlowGraph.AllPathsReturn())
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
        }

        if (_collectSymbols)
            SymbolCollector.Collect(this, loweredBody);

        state.currentImportChain = oldImportChain;
        _methodBodies.TryAdd(method, loweredBody);

        return currentDiagnostics;
    }

    private BoundBlockStatement LowerBody(
        MethodSymbol method,
        int methodOrdinal,
        BoundBlockStatement body,
        TypeCompilationState state,
        List<LocalFunctionRewriter.Analysis> previousAnalyses,
        BelteDiagnosticQueue currentDiagnostics,
        ref MethodSymbol entryPoint,
        out bool sawCompileTimeExpression) {
        try {
            var loweredBody = Lowerer.Lower(
                this,
                state.compilation.options.optimizationLevel,
                method,
                body,
                entryPoint?.containingType,
                currentDiagnostics,
                out sawCompileTimeExpression
            );

            if (!transpiling) {
                loweredBody = LocalFunctionRewriter.Rewrite(
                    loweredBody,
                    state.type,
                    method.thisParameter,
                    method,
                    methodOrdinal,
                    null,
                    state,
                    previousAnalyses,
                    currentDiagnostics,
                    null, // TODO When do we want to use this?
                    ref entryPoint
                );

                loweredBody = Optimizer.RemoveDeadCode(method, loweredBody, currentDiagnostics);
            }

            return loweredBody;
        } catch (BoundTreeVisitor.CancelledByStackGuardException ex) {
            ex.AddAnError(currentDiagnostics);
            sawCompileTimeExpression = false;

            return new BoundBlockStatement(
                body.syntax,
                [new BoundErrorStatement(body.syntax, [body], hasErrors: true)],
                [],
                [],
                hasErrors: true
            );
        }
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
            outInitializersBody: null,
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
        BoundBlockStatement outInitializersBody,
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

                        if (outInitializersBody is not null)
                            builder.Add(outInitializersBody);

                        if (body is not null)
                            builder.Add(body);

                        body = new BoundBlockStatement(syntax, builder.ToImmutableAndFree(), constructor.locals, []);
                        return body;
                    }

                    // TODO Roslyn returns here even in the static constructor case
                    // But for the life of me I can't figure out where the static initializers are added so i'm just
                    // going to break here instead
                    // return body;
                    break;
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

        if (method.methodKind == MethodKind.Finalizer && body is not null) {
            return ConstructFinalizerBody(
                method,
                body,
                state.compilation.options.optimizationLevel == OptimizationLevel.Debug
            );
        }

        var constructorInitializer = BindImplicitConstructorInitializerIfAny(method, state, syntaxNode, diagnostics);

        if (includeInitializers)
            builder.Add(initializersBody);

        if (constructorInitializer is not null)
            builder.Add(constructorInitializer);

        if (outInitializersBody is not null)
            builder.Add(outInitializersBody);

        if (method == entryPoint && method is SynthesizedEntryPoint synth && body.localFunctions.Length > 0) {
            var candidateLocals = ArrayBuilder<MethodSymbol>.GetInstance();
            var potentiallyMistakenLocals = ArrayBuilder<MethodSymbol>.GetInstance();

            foreach (var local in body.localFunctions) {
                if (Compilation.HasEntryPointSignature(local))
                    candidateLocals.Add(local);
                else if (local.name == WellKnownMemberNames.EntryPointMethodName)
                    potentiallyMistakenLocals.Add(local);
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

                if (newEntryPoint is not null && !bodyHasLogic)
                    entryPoint = newEntryPoint;
            } else if (potentiallyMistakenLocals.Count > 0) {
                foreach (var potentiallyMistake in potentiallyMistakenLocals)
                    diagnostics.Push(Warning.LocalFunctionUsingEntryPointName(potentiallyMistake.location));
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
            !call.hasAnyErrors &&
            TypeSymbol.Equals(call.method.containingType, method.containingType, TypeCompareKind.ConsiderEverything)) {
            state.ReportConstructorInitializerCycles(method, call.method, syntax, diagnostics);
        }
    }

    private static bool PassesFilter(Predicate<Symbol> filter, Symbol symbol) {
        return (filter is null) || filter(symbol);
    }

    internal static BoundBlockStatement ConstructFinalizerBody(
        MethodSymbol method,
        BoundBlockStatement block,
        bool generateSequencePoint) {
        var syntax = block.syntax;
        var baseTypeFinalize = GetBaseTypeFinalizeMethod(method);

        if (baseTypeFinalize is not null) {
            BoundStatement baseFinalizeCall = new BoundExpressionStatement(syntax,
                new BoundCallExpression(syntax,
                    new BoundBaseExpression(syntax, method.containingType),
                    baseTypeFinalize,
                    [],
                    [],
                    BitVector.Empty,
                    LookupResultKind.Empty,
                    baseTypeFinalize.returnType
                )
            );

            if (syntax.kind == SyntaxKind.BlockStatement && generateSequencePoint) {
                baseFinalizeCall = new BoundSequencePointWithLocation(
                    syntax,
                    baseFinalizeCall,
                    ((BlockStatementSyntax)syntax).closeBrace.location
                );
            }

            return new BoundBlockStatement(syntax,
                [new BoundTryStatement(syntax,
                    block,
                    null,
                    new BoundBlockStatement(syntax,
                        [baseFinalizeCall],
                        [],
                        []
                    )
                )],
                [],
                []
            );
        }

        return block;
    }

    internal static MethodSymbol GetBaseTypeFinalizeMethod(MethodSymbol method) {
        var baseType = method.containingType.baseType;

        while (baseType is not null) {
            foreach (var member in baseType.GetMembers(WellKnownMemberNames.FinalizerName)) {
                if (member.kind == SymbolKind.Method) {
                    var baseTypeMethod = (MethodSymbol)member;
                    var accessibility = baseTypeMethod.declaredAccessibility;

                    if ((accessibility == Accessibility.Protected) &&
                        baseTypeMethod.parameterCount == 0 &&
                        baseTypeMethod.arity == 0 &&
                        baseTypeMethod.returnsVoid) {
                        return baseTypeMethod;
                    }
                }
            }

            baseType = baseType.baseType;
        }

        return null;
    }
}
