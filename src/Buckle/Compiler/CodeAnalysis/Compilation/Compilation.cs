using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Buckle.CodeAnalysis.Authoring;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Emitting;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.FlowAnalysis;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Buckle.Utilities;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;
using Shared;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Handles evaluation of program, and keeps track of Symbols.
/// </summary>
public sealed partial class Compilation {
    private static readonly Func<SyntaxTree, SmallConcurrentSetOfInts> CreateSetCallback =
        t => new SmallConcurrentSetOfInts();
    private readonly static Predicate<Symbol> SkipLibrariesFilter
        = type => type is not SynthesizedFinishedNamedTypeSymbol;

    private readonly NamespaceSymbol _specialNamespace;
    private readonly ReferenceManager _referenceManager;
    private SyntaxAndDeclarationManager _syntax;
    private WeakReference<BinderFactory>[] _binderFactories;
    private WeakReference<BinderFactory>[] _ignoreAccessibilityBinderFactories;

    private ConcurrentDictionary<SyntaxTree, SmallConcurrentSetOfInts> _lazyTreeToUsedImportDirectivesMap;
    private BelteDiagnosticQueue _lazyDeclarationDiagnostics;
    private BoundProgram _lazyBoundProgram;
    private BelteDiagnosticQueue _lazyMethodDiagnostics;
    private List<LocalFunctionRewriter.Analysis> _lazyPreviousAnalyses;
    private MethodSymbol _lazyEntryPoint;
    private MethodSymbol _lazyUpdatePoint;
    private NamedTypeSymbol _lazyScriptClass;
    private NamespaceSymbol _lazyGlobalNamespace;
    private AliasSymbol _lazyGlobalNamespaceAlias;
    private AssemblySymbol _lazyAssembly;
    private HandleManager _lazyHandleManager;
    private ConcurrentSet<AssemblySymbol> _lazyUsedAssemblyReferences;
    private ConcurrentDictionary<ImportInfo, ImmutableArray<AssemblySymbol>> _lazyImportInfos;

    private Compilation(
        string assemblyName,
        CompilationOptions options,
        Compilation previous,
        SyntaxAndDeclarationManager syntax,
        ReferenceManager referenceManager,
        NamespaceSymbol namespaceOpt = null) {
        this.assemblyName = assemblyName;
        this.options = options;
        this.previous = previous;
        _syntax = syntax;
        _specialNamespace = namespaceOpt;

        if (previous?.declarationDiagnostics is not null)
            declarationDiagnostics.PushRange(previous.declarationDiagnostics);

        if (referenceManager is not null)
            _referenceManager = referenceManager;
        else
            _referenceManager = new ReferenceManager(options.references, declarationDiagnostics);

        handleManager.SendParsedMessage();
    }

    public string assemblyName { get; }

    public INamespaceSymbol globalNamespace => globalNamespaceInternal;

    public CompilationOptions options { get; }

    public Compilation previous { get; }

    internal MethodSymbol entryPoint => boundProgram.entryPoint;

    internal BelteDiagnosticQueue declarationDiagnostics {
        get {
            if (_lazyDeclarationDiagnostics is null)
                Interlocked.CompareExchange(ref _lazyDeclarationDiagnostics, new BelteDiagnosticQueue(), null);

            return _lazyDeclarationDiagnostics;
        }
    }

    internal BoundProgram boundProgram {
        get {
            EnsureBoundProgramAndMethodDiagnostics();
            return _lazyBoundProgram;
        }
    }

    internal BelteDiagnosticQueue methodDiagnostics {
        get {
            EnsureBoundProgramAndMethodDiagnostics();
            return _lazyMethodDiagnostics;
        }
    }

    internal ImmutableArray<SyntaxTree> syntaxTrees => _syntax.state.syntaxTrees;

    internal bool keepLookingForCorTypes => CorLibrary.StillLookingForSpecialTypes();

    internal MergedNamespaceDeclaration mergedRootDeclaration => _syntax.state.declarationTable.GetMergedRoot(this);

    internal DeclarationTable declarationTable => _syntax.state.declarationTable;

    internal AssemblySymbol assembly {
        get {
            if (_lazyAssembly is null)
                Interlocked.CompareExchange(ref _lazyAssembly, new SourceAssemblySymbol(this, assemblyName), null);

            return _lazyAssembly;
        }
    }

    internal ModuleSymbol sourceModule => assembly.modules[0];

    internal NamespaceSymbol globalNamespaceInternal {
        get {
            if (_lazyGlobalNamespace is null) {
                var builder = ArrayBuilder<NamespaceSymbol>.GetInstance();
                var modules = ArrayBuilder<ModuleSymbol>.GetInstance();
                GetAllUnaliasedModules(modules);
                builder.AddRange(modules.SelectDistinct(m => m.globalNamespace));
                builder.AddRange(_referenceManager.GetGlobalNamespaces());

                if (_specialNamespace is not null)
                    builder.Add(_specialNamespace);

                var result = MergedNamespaceSymbol.Create(
                    new NamespaceExtent(this),
                    null,
                    builder.ToImmutableAndFree()
                );

                modules.Free();

                Interlocked.CompareExchange(ref _lazyGlobalNamespace, result, null);
            }

            return _lazyGlobalNamespace;
        }
    }

    internal SemanticModelProvider semanticModelProvider { get; }

    internal AliasSymbol globalNamespaceAlias {
        get {
            if (_lazyGlobalNamespaceAlias is null)
                Interlocked.CompareExchange(ref _lazyGlobalNamespaceAlias, CreateGlobalNamespaceAlias(), null);

            return _lazyGlobalNamespaceAlias;
        }
    }

    internal NamedTypeSymbol scriptClass {
        get {
            if (_lazyScriptClass is null) {
                var scriptClass = SynthesizedEntryPoint.GetSimpleProgramNamedTypeSymbol(this);
                Interlocked.CompareExchange(ref _lazyScriptClass, scriptClass, null);
            }

            return _lazyScriptClass;
        }
    }

    internal List<LocalFunctionRewriter.Analysis> previousAnalyses {
        get {
            if (_lazyPreviousAnalyses is null) {
                List<LocalFunctionRewriter.Analysis> result;

                if (previous?.previousAnalyses is not null)
                    result = new List<LocalFunctionRewriter.Analysis>(previous.previousAnalyses);
                else
                    result = [];

                Interlocked.CompareExchange(ref _lazyPreviousAnalyses, result, null);
            }

            return _lazyPreviousAnalyses;
        }
    }

    internal HandleManager handleManager {
        get {
            if (_lazyHandleManager is null)
                Interlocked.CompareExchange(ref _lazyHandleManager, new HandleManager(this, GetHandles()), null);

            return _lazyHandleManager;
        }
    }

    private ConcurrentDictionary<SyntaxTree, SmallConcurrentSetOfInts> treeToUsedImportDirectivesMap {
        get {
            if (_lazyTreeToUsedImportDirectivesMap is null)
                InterlockedOperations.Initialize(ref _lazyTreeToUsedImportDirectivesMap, new());

            return _lazyTreeToUsedImportDirectivesMap;
        }
    }

    public SemanticModel GetSemanticModel(SyntaxTree syntaxTree) {
        ArgumentNullException.ThrowIfNull(syntaxTree);

        if (!_syntax.state.rootNamespaces.ContainsKey(syntaxTree))
            throw new ArgumentException($"Syntax tree {nameof(syntaxTree)} not found");

        SemanticModel model = null;

        if (semanticModelProvider is not null)
            model = semanticModelProvider.GetSemanticModel(syntaxTree, this);

        return model ?? CreateSemanticModel(syntaxTree);
    }

    internal SemanticModel CreateSemanticModel(SyntaxTree syntaxTree) {
        // return new SyntaxTreeSemanticModel(this, syntaxTree);
        throw new NotImplementedException();
    }

    public ImmutableArray<ISymbol> GetSymbols(
        bool includePreviousCompilations = false,
        bool includeExternal = false,
        bool includeSimpleProgramLocals = false) {
        if (!includePreviousCompilations)
            return globalNamespace.GetMembers();

        // TODO Cache this lookup?
        // TODO Eventually flesh out this function to support more options (filtering, sorting, etc.)
        var builder = new HashSet<ISymbol>();
        var current = this;

        while (current is not null) {
            if (includeExternal) {
                builder.AddAll(current.globalNamespace.GetMembers());
            } else {
                foreach (var member in current.globalNamespace.GetMembers()) {
                    if (member is not SynthesizedFinishedNamedTypeSymbol)
                        builder.Add(member);
                }
            }

            if (includeSimpleProgramLocals) {
                if (current.entryPoint is SynthesizedEntryPoint synthesizedEntryPoint) {
                    var compilationUnit = synthesizedEntryPoint.compilationUnit;
                    var entryPointBinder = synthesizedEntryPoint
                        .TryGetBodyBinder(null, true)
                        .GetBinder(compilationUnit);

                    builder.AddAll(entryPointBinder.GetDeclaredLocalsForScope(compilationUnit));
                    builder.AddAll(entryPointBinder.GetDeclaredLocalFunctionsForScope(compilationUnit));
                }
            }

            current = current.previous;
        }

        return builder.ToImmutableArray();
    }

    public static Compilation Create(string assemblyName, CompilationOptions options, params SyntaxTree[] syntaxTrees) {
        return Create(assemblyName, options, null, syntaxTrees);
    }

    public static Compilation Create(
        string assemblyName,
        CompilationOptions options,
        Compilation previous,
        params SyntaxTree[] syntaxTrees) {
        return Create(assemblyName, options, previous, (IEnumerable<SyntaxTree>)syntaxTrees);
    }

    public static Compilation CreateScript(
        string assemblyName,
        CompilationOptions options,
        SyntaxTree syntaxTree = null,
        Compilation previous = null) {
        options.isScript = true;
        var syntaxTress = syntaxTree is null ? null : (IEnumerable<SyntaxTree>)[syntaxTree];
        return Create(assemblyName, options, previous, syntaxTress);
    }

    public EvaluationResult Evaluate(
        ValueWrapper<bool> abort,
        bool verbose = false,
        bool logTime = false,
        string verbosePath = null) {
        using var context = new EvaluatorContext(options);
        var result = Evaluate(context, abort, verbose, logTime, verbosePath);
        context.WaitForCompletion();

        if (verbose && result.heap is not null) {
            Console.WriteLine(
                $"Heap after completion: Capacity {result.heap.capacity}, " +
                $"Allocated {result.heap.usedCount}");
        }

        return result;
    }

    public EvaluationResult Evaluate(
        EvaluatorContext context,
        ValueWrapper<bool> abort,
        bool verbose = false,
        bool logTime = false,
        string verbosePath = null) {
        EvaluationResult result = null;
        Evaluate(context, abort, ref result, verbose, logTime, verbosePath);

        if (verbose && result.heap is not null) {
            Console.WriteLine(
                $"Heap: Capacity {result.heap.capacity}, " +
                $"Allocated {result.heap.usedCount}");
        }

        return result;
    }

    internal Compilation AddNamespace(NamespaceSymbol namespaceSymbol) {
        return new Compilation(assemblyName, options, previous, _syntax, _referenceManager, namespaceSymbol);
    }

    internal void Evaluate(
        EvaluatorContext context,
        ValueWrapper<bool> abort,
        ref EvaluationResult rollingResult,
        bool verbose = false,
        bool logTime = false,
        string verbosePath = null) {
        var timer = logTime ? Stopwatch.StartNew() : null;
        var diagnostics = GetDiagnostics();
        var program = boundProgram;

        Log(logTime, timer, diagnostics, $"Bound the program in {timer?.ElapsedMilliseconds} ms");

        if (diagnostics.AnyErrors()) {
            rollingResult = EvaluationResult.Failed(diagnostics);
            return;
        }

        handleManager.SendBeforeEmitMessage();

        if (verbose && options.enableOutput) {
            EmitCFG(verbosePath);
            EmitBoundProgram(verbosePath);
        }

        var evaluator = new Evaluator(program, context, options.arguments);
        var evalResult = evaluator.Evaluate(abort, out var hasValue, out var resultType);

        Log(logTime, timer, diagnostics, $"Evaluated the program in {timer?.ElapsedMilliseconds} ms");

        if (verbose && options.enableOutput && evalResult is not null)
            Console.WriteLine(evalResult);

        handleManager.SendFinishedMessage();

        if (rollingResult is null) {
            rollingResult = new EvaluationResult(
                evalResult,
                resultType,
                hasValue,
                diagnostics,
                evaluator.exceptions,
                evaluator.lastOutputWasPrint,
                evaluator.containsIO,
                context.heap
            );
        } else {
            rollingResult.Update(
                evalResult,
                resultType,
                hasValue,
                diagnostics,
                evaluator.exceptions,
                evaluator.lastOutputWasPrint,
                evaluator.containsIO,
                context.heap
            );
        }
    }

    public BelteDiagnosticQueue Emit(string outputPath, bool debugMode, bool logTime, bool verbose, string verbosePath) {
        if (options.buildMode == BuildMode.Independent) {
            var fatal = new BelteDiagnosticQueue();
            fatal.Push(Fatal.Unsupported.IndependentCompilation());
            return fatal;
        }

        var timer = logTime ? Stopwatch.StartNew() : null;
        var diagnostics = GetDiagnostics();
        var program = boundProgram;

        Log(logTime, timer, diagnostics, $"Bound the program in {timer?.ElapsedMilliseconds} ms");

        if (diagnostics.AnyErrors())
            return diagnostics;

        handleManager.SendBeforeEmitMessage();

        if (verbose && options.enableOutput) {
            EmitCFG(verbosePath);
            EmitBoundProgram(verbosePath);
        }

        if (options.buildMode == BuildMode.Dotnet)
            ILEmitter.Emit(program, assemblyName, options.references, outputPath, debugMode, diagnostics);
        else if (options.buildMode == BuildMode.CSharpTranspile)
            CSharpEmitter.Emit(program, outputPath, diagnostics);

        if (options.buildMode is BuildMode.Dotnet or BuildMode.CSharpTranspile)
            Log(logTime, timer, diagnostics, $"Emitted the program in {timer?.ElapsedMilliseconds} ms");

        handleManager.SendFinishedMessage();
        return diagnostics;
    }

    public BelteDiagnosticQueue Execute(bool verbose = false, bool logTime = false, string verbosePath = null) {
        return Execute(verbose, logTime, verbosePath, out _);
    }

    internal BelteDiagnosticQueue Execute(bool verbose, bool logTime, string verbosePath, out object result) {
        var timer = logTime ? Stopwatch.StartNew() : null;
        var diagnostics = GetDiagnostics();
        var program = boundProgram;

        Log(logTime, timer, diagnostics, $"Bound the program in {timer?.ElapsedMilliseconds} ms");

        if (diagnostics.AnyErrors()) {
            result = null;
            return diagnostics;
        }

        handleManager.SendBeforeEmitMessage();

        if (verbose && options.enableOutput) {
            EmitCFG(verbosePath);
            EmitBoundProgram(verbosePath);
        }

        var executor = new Executor(program, options.arguments, diagnostics);
        result = executor.Execute(verbose, logTime, verbosePath);

        if (verbose && options.enableOutput && result is not null)
            Console.WriteLine(result);

        handleManager.SendFinishedMessage();
        return diagnostics;
    }

    public EvaluationResult Interpret(ValueWrapper<bool> abort, bool logTime) {
        return Interpreter.Interpret(_syntax.syntaxTrees[0], options, abort, logTime);
    }

    public string EmitToString(
        out BelteDiagnosticQueue diagnostics,
        BuildMode? alternateBuildMode = null,
        bool programOnly = false) {
        var buildMode = alternateBuildMode ?? options.buildMode;
        diagnostics = GetDiagnostics();
        var program = boundProgram;

        if (diagnostics.AnyErrors())
            return null;

        if (buildMode == BuildMode.CSharpTranspile) {
            return CSharpEmitter.EmitToString(program, assemblyName, diagnostics);
        } else if (buildMode == BuildMode.Dotnet) {
            return ILEmitter.EmitToString(
                program,
                assemblyName,
                programOnly,
                options.references,
                diagnostics
            );
        }

        return null;
    }

    public BelteDiagnosticQueue GetParseDiagnostics() {
        return GetDiagnostics(true, false, false);
    }

    public BelteDiagnosticQueue GetDeclarationDiagnostics() {
        return GetDiagnostics(false, true, false);
    }

    public BelteDiagnosticQueue GetMethodBodyDiagnostics() {
        return GetDiagnostics(false, false, true);
    }

    public BelteDiagnosticQueue GetDiagnostics() {
        return GetDiagnostics(true, true, true);
    }

    internal void SetDeclarationDiagnostics(BelteDiagnosticQueue diagnostics) {
        Interlocked.Exchange(ref _lazyDeclarationDiagnostics, diagnostics);
    }

    internal MethodSymbol GetEntryPoint(BelteDiagnosticQueue diagnostics) {
        if (_lazyEntryPoint is null) {
            var simpleEntryPoint = SynthesizedEntryPoint.GetSimpleProgramEntryPoint(this);
            Interlocked.CompareExchange(ref _lazyEntryPoint, FindEntryPoint(simpleEntryPoint, diagnostics), null);
        }

        return _lazyEntryPoint;
    }

    internal MethodSymbol GetUpdatePoint(MethodSymbol entryPoint, BelteDiagnosticQueue diagnostics) {
        if (_lazyUpdatePoint is null)
            Interlocked.CompareExchange(ref _lazyUpdatePoint, FindUpdatePoint(entryPoint, diagnostics), null);

        return _lazyUpdatePoint;
    }

    internal MethodSymbol GetLateScriptUpdatePoint(Dictionary<MethodSymbol, BoundBlockStatement> methodBodies) {
        if (_lazyUpdatePoint is null)
            Interlocked.CompareExchange(ref _lazyUpdatePoint, FindLateScriptUpdatePoint(methodBodies), null);

        return _lazyUpdatePoint;
    }

    internal int CompareSourceLocations(SyntaxReference syntax1, SyntaxReference syntax2) {
        var comparison = CompareSyntaxTreeOrdering(syntax1.syntaxTree, syntax2.syntaxTree);

        if (comparison != 0)
            return comparison;

        return syntax1.span.start - syntax2.span.start;
    }

    internal int CompareSourceLocations(
        SyntaxReference syntax1,
        TextLocation location1,
        SyntaxReference syntax2,
        TextLocation location2) {
        var comparison = CompareSyntaxTreeOrdering(syntax1.syntaxTree, syntax2.syntaxTree);

        if (comparison != 0)
            return comparison;

        return location1.span.start - location2.span.start;
    }

    internal int CompareSyntaxTreeOrdering(SyntaxTree tree1, SyntaxTree tree2) {
        if (tree1 == tree2)
            return 0;

        return GetSyntaxTreeOrdinal(tree1) - GetSyntaxTreeOrdinal(tree2);
    }

    internal void RegisterDeclaredSpecialType(NamedTypeSymbol type) {
        // TODO Maybe make the CorLibrary not static?
        CorLibrary.RegisterDeclaredSpecialType(type);
    }

    internal Binder GetBinder(BelteSyntaxNode syntax) {
        return GetBinderFactory(syntax.syntaxTree).GetBinder(syntax);
    }

    internal BinderFactory GetBinderFactory(SyntaxTree syntaxTree, bool ignoreAccessibility = false) {
        if (ignoreAccessibility && SynthesizedEntryPoint.GetSimpleProgramEntryPoint(this) is not null)
            return GetBinderFactory(syntaxTree, ignoreAccessibility: true, ref _ignoreAccessibilityBinderFactories);

        return GetBinderFactory(syntaxTree, ignoreAccessibility: false, ref _binderFactories);
    }

    internal BinderFactory GetBinderFactory(
        SyntaxTree syntaxTree,
        bool ignoreAccessibility,
        ref WeakReference<BinderFactory>[] cachedBinderFactories) {
        var treeOrdinal = GetSyntaxTreeOrdinal(syntaxTree);
        var binderFactories = cachedBinderFactories;

        if (binderFactories is null) {
            binderFactories = new WeakReference<BinderFactory>[syntaxTrees.Length];
            binderFactories = Interlocked.CompareExchange(ref cachedBinderFactories, binderFactories, null)
                ?? binderFactories;
        }

        var previousWeakReference = binderFactories[treeOrdinal];

        if (previousWeakReference is not null && previousWeakReference.TryGetTarget(out var previousFactory))
            return previousFactory;

        return AddNewFactory(syntaxTree, ignoreAccessibility, ref binderFactories[treeOrdinal]);
    }

    internal int GetSyntaxTreeOrdinal(SyntaxTree syntaxTree) {
        return _syntax.state.ordinalMap[syntaxTree];
    }

    internal void RecordImport(UsingDirectiveSyntax syntax) {
        LazyInitializer.EnsureInitialized(ref _lazyImportInfos)
            .TryAdd(new ImportInfo(syntax.syntaxTree, syntax.kind, syntax.span), default);
    }

    internal void AddUsedAssemblies(ICollection<AssemblySymbol> assemblies) {
        if (!assemblies.IsNullOrEmpty()) {
            foreach (var candidate in assemblies)
                AddUsedAssembly(candidate);
        }
    }

    internal bool AddUsedAssembly(AssemblySymbol assembly) {
        if (assembly is null || assembly == this.assembly || assembly.isMissing)
            return false;

        if (_lazyUsedAssemblyReferences is null)
            Interlocked.CompareExchange(ref _lazyUsedAssemblyReferences, new ConcurrentSet<AssemblySymbol>(), null);

        return _lazyUsedAssemblyReferences.Add(assembly);
    }

    private MethodSymbol FindUpdatePoint(MethodSymbol entryPoint, BelteDiagnosticQueue diagnostics) {
        var builder = ArrayBuilder<MethodSymbol>.GetInstance();
        var classes = globalNamespaceInternal.GetTypeMembersUnordered();

        foreach (var type in classes) {
            foreach (var member in type.GetMembers(WellKnownMemberNames.UpdatePointMethodName)) {
                if (member is MethodSymbol m && HasUpdatePointSignature(m))
                    builder.Add(m);
            }
        }

        var updatePointCandidates = builder.ToImmutableAndFree();
        MethodSymbol updatePoint = null;

        if (updatePointCandidates.Length == 1) {
            updatePoint = updatePointCandidates[0];
        } else if (updatePointCandidates.Length > 1) {
            var updatesNearMain = ArrayBuilder<MethodSymbol>.GetInstance();

            foreach (var method in updatePointCandidates) {
                if (method.containingType.Equals(entryPoint.containingType))
                    updatesNearMain.Add(method);
            }

            if (updatesNearMain.Count == 1)
                updatePoint = updatesNearMain[0];
            else
                diagnostics.Push(Error.MultipleUpdates(updatePointCandidates[0].location));
        }

        return updatePoint;
    }

    private MethodSymbol FindLateScriptUpdatePoint(Dictionary<MethodSymbol, BoundBlockStatement> methodBodies) {
        var builder = ArrayBuilder<MethodSymbol>.GetInstance();

        foreach (var (method, _) in methodBodies) {
            if (HasUpdatePointSignature(method))
                builder.Add(method);
        }

        var updatePointCandidates = builder.ToImmutableAndFree();
        MethodSymbol updatePoint = null;

        if (updatePointCandidates.Length == 1)
            updatePoint = updatePointCandidates[0];

        return updatePoint;
    }

    private static bool HasUpdatePointSignature(MethodSymbol method) {
        var returnType = method.returnType;

        if (!returnType.IsVoidType())
            return false;

        if (method.refKind != RefKind.None)
            return false;

        if (method.parameterCount != 1)
            return false;

        if (!method.parameterRefKinds.IsDefault)
            return false;

        var firstType = method.parameters[0].type;

        if (firstType.specialType != SpecialType.Decimal)
            return false;

        return true;
    }

    private MethodSymbol FindEntryPoint(SynthesizedEntryPoint simpleEntryPoint, BelteDiagnosticQueue diagnostics) {
        var builder = ArrayBuilder<MethodSymbol>.GetInstance();

        AddEntryPointCandidates(
            builder,
            GetSymbolsWithName(WellKnownMemberNames.EntryPointMethodName, SymbolFilter.Member)
        );

        builder.AddIfNotNull(simpleEntryPoint);
        var entryPointCandidates = builder.ToImmutableAndFree();
        return SelectEntryPoint(simpleEntryPoint, entryPointCandidates, diagnostics, options.isScript);

        static void AddEntryPointCandidates(
            ArrayBuilder<MethodSymbol> entryPointCandidates,
            IEnumerable<Symbol> members) {
            foreach (var member in members) {
                if (member is MethodSymbol m and not SynthesizedEntryPoint && HasEntryPointSignature(m))
                    entryPointCandidates.Add(m);
            }
        }
    }

    internal IEnumerable<Symbol> GetSymbolsWithName(string name, SymbolFilter filter) {
        ArgumentNullException.ThrowIfNull(name);

        if (filter == SymbolFilter.None)
            throw new ArgumentException(nameof(filter));

        return new NameSymbolSearcher(this, filter, name).GetSymbolsWithName();
    }

    internal IEnumerable<Symbol> GetSymbolsWithName(Func<string, bool> predicate, SymbolFilter filter) {
        if (predicate is null)
            throw new ArgumentNullException(nameof(predicate));

        if (filter == SymbolFilter.None)
            throw new ArgumentException(nameof(filter));

        return new PredicateSymbolSearcher(this, filter, predicate).GetSymbolsWithName();
    }

    internal IEnumerable<Symbol> GetSymbols(SymbolFilter filter) {
        if (filter == SymbolFilter.None)
            throw new ArgumentException(nameof(filter));

        return new PredicateSymbolSearcher(this, filter, x => true).GetSymbolsWithName();
    }

    internal static MethodSymbol SelectEntryPoint(
        SynthesizedEntryPoint simpleEntryPoint,
        ImmutableArray<MethodSymbol> methods,
        BelteDiagnosticQueue diagnostics,
        bool isScript) {
        var expectedCount = simpleEntryPoint is null ? 1 : 2;

        MethodSymbol entryPoint = null;

        if (methods.Length == 0 && !isScript) {
            diagnostics.Push(Error.NoSuitableEntryPoint());
        } else if (methods.Length == 1) {
            entryPoint = methods[0];
        } else if (methods.Length > 1) {
            if (methods.Length > expectedCount)
                diagnostics.Push(Error.MultipleMains(methods[0].location));

            if (methods.Length > 1 && simpleEntryPoint is not null)
                diagnostics.Push(Error.MainAndGlobals(methods[0].location));
        }

        if (entryPoint is not null && !entryPoint.isStatic) {
            var containingConstructors = entryPoint.containingType.instanceConstructors;

            if (containingConstructors.Length != 1 ||
                containingConstructors[0] is not SynthesizedInstanceConstructorSymbol) {
                diagnostics.Push(Error.EntryConstructor(entryPoint.containingType.location));
            }
        }

        return entryPoint;
    }

    internal static bool HasEntryPointSignature(MethodSymbol method) {
        if (!method.name.Equals("main", StringComparison.CurrentCultureIgnoreCase))
            return false;

        var returnType = method.returnType;

        if (returnType.specialType != SpecialType.Int && !returnType.IsVoidType()) {
            if (returnType.specialType == SpecialType.Nullable &&
                returnType.GetNullableUnderlyingType().specialType != SpecialType.Int) {
                return false;
            }
        }

        if (method.refKind != RefKind.None)
            return false;

        if (method.parameterCount == 0)
            return true;

        if (method.parameterCount > 1)
            return false;

        if (!method.parameterRefKinds.IsDefault)
            return false;

        var firstType = method.parameters[0].type;

        if (firstType.specialType != SpecialType.Array)
            return false;

        var elementType = ((ArrayTypeSymbol)firstType).elementType;

        if (elementType.specialType != SpecialType.String)
            return false;

        return true;
    }

    private AliasSymbol CreateGlobalNamespaceAlias() {
        return AliasSymbol.CreateGlobalNamespaceAlias(globalNamespaceInternal);
    }

    private Compilation AddSyntaxTrees(IEnumerable<SyntaxTree> trees) {
        ArgumentNullException.ThrowIfNull(trees);

        if (trees.IsEmpty())
            return this;

        var externalSyntaxTrees = PooledHashSet<SyntaxTree>.GetInstance();
        var syntax = _syntax;
        externalSyntaxTrees.AddAll(syntax.syntaxTrees);

        var i = 0;

        foreach (var tree in trees) {
            if (tree is null)
                throw new ArgumentNullException($"{nameof(trees)}[{i}]");

            if (externalSyntaxTrees.Contains(tree))
                throw new ArgumentException("Syntax tree already present", $"{nameof(trees)}[{i}]");

            externalSyntaxTrees.Add(tree);
            i++;
        }

        externalSyntaxTrees.Free();
        syntax = syntax.AddSyntaxTrees(trees);
        return Update(syntax);
    }

    private DirectiveTriviaSyntax[] GetHandles() {
        var builder = ArrayBuilder<DirectiveTriviaSyntax>.GetInstance();

        foreach (var tree in _syntax.syntaxTrees) {
            builder.AddRange(tree
                .GetRoot()
                .GetDirectives(d => d.kind == SyntaxKind.HandleDirectiveTrivia)
            );
        }

        return builder.ToArrayAndFree();
    }

    private Compilation Update(SyntaxAndDeclarationManager syntax) {
        var compilation = new Compilation(assemblyName, options, previous, syntax, _referenceManager);
        compilation.declarationDiagnostics.PushRange(declarationDiagnostics);
        return compilation;
    }

    private BinderFactory AddNewFactory(
        SyntaxTree syntaxTree,
        bool ignoreAccessibility,
        ref WeakReference<BinderFactory> slot) {
        var newFactory = new BinderFactory(this, syntaxTree, ignoreAccessibility);
        var newWeakReference = new WeakReference<BinderFactory>(newFactory);

        while (true) {
            var previousWeakReference = slot;

            if (previousWeakReference is not null && previousWeakReference.TryGetTarget(out var previousFactory))
                return previousFactory;

            if (Interlocked.CompareExchange(ref slot!, newWeakReference, previousWeakReference)
                == previousWeakReference) {
                return newFactory;
            }
        }
    }

    internal void MarkImportDirectiveAsUsed(SyntaxReference node) {
        MarkImportDirectiveAsUsed(node.syntaxTree, node.span.start);
    }

    internal void MarkImportDirectiveAsUsed(SyntaxTree? syntaxTree, int position) {
        if (syntaxTree is not null) {
            var set = treeToUsedImportDirectivesMap.GetOrAdd(syntaxTree, CreateSetCallback);
            set.Add(position);
        }
    }

    private static Compilation Create(
        string assemblyName,
        CompilationOptions options,
        Compilation previous,
        IEnumerable<SyntaxTree> syntaxTrees) {
        var compilation = new Compilation(
            assemblyName,
            options,
            previous,
            new SyntaxAndDeclarationManager([], null),
            null
        );

        if (syntaxTrees is not null)
            compilation = compilation.AddSyntaxTrees(syntaxTrees);

        return compilation;
    }

    private void GetAllUnaliasedModules(ArrayBuilder<ModuleSymbol> builder) {
        builder.AddRange(assembly.modules);
    }

    private BelteDiagnosticQueue GetDiagnostics(bool includeParse, bool includeDeclaration, bool includeMethods) {
        var builder = new BelteDiagnosticQueue();

        if (includeParse) {
            foreach (var syntaxTree in _syntax.syntaxTrees)
                builder.PushRange(syntaxTree.GetDiagnostics());
        }

        if (includeDeclaration) {
            assembly.ForceComplete(null);
            builder.PushRange(declarationDiagnostics);
        }

        if (includeMethods)
            builder.PushRange(methodDiagnostics);

        builder = BelteDiagnosticQueue.CleanDiagnostics(builder);
        handleManager.SendDiagnosticsMessage(builder);
        return builder;
    }

    private void EnsureBoundProgramAndMethodDiagnostics() {
        if (_lazyBoundProgram is null)
            CreateBoundProgramAndMethodDiagnostics();
    }

    internal void ReplaceBoundProgram(BoundProgram program, BelteDiagnosticQueue methodDiagnostics) {
        _lazyMethodDiagnostics = methodDiagnostics;
        _lazyBoundProgram = program;
    }

    internal void AddLateSyntaxTrees(IEnumerable<SyntaxTree> trees) {
        _syntax = _syntax.AddSyntaxTrees(trees);

        Interlocked.Exchange(ref _lazyAssembly, null);
        Interlocked.Exchange(ref _lazyBoundProgram, null);
        Interlocked.Exchange(ref _lazyEntryPoint, null);
        Interlocked.Exchange(ref _lazyGlobalNamespace, null);
        Interlocked.Exchange(ref _lazyGlobalNamespaceAlias, null);
        Interlocked.Exchange(ref _lazyImportInfos, null);
        Interlocked.Exchange(ref _lazyPreviousAnalyses, null);
        Interlocked.Exchange(ref _lazyScriptClass, null);
        Interlocked.Exchange(ref _lazyTreeToUsedImportDirectivesMap, null);
        Interlocked.Exchange(ref _lazyUpdatePoint, null);
        Interlocked.Exchange(ref _lazyUsedAssemblyReferences, null);
        Interlocked.Exchange(ref _binderFactories, null);
        Interlocked.Exchange(ref _ignoreAccessibilityBinderFactories, null);

        handleManager.SendParsedMessage();
    }

    private void CreateBoundProgramAndMethodDiagnostics() {
        _lazyMethodDiagnostics = new BelteDiagnosticQueue();
        _lazyBoundProgram = MethodCompiler.CompileMethodBodies(
            this,
            _lazyMethodDiagnostics,
            SkipLibrariesFilter
        );

        handleManager.SendBoundMessage();
    }

    private void EmitCFG(string path) {
#if DEBUG
        const string CFGName = "cfg.dot";

        var program = boundProgram;
        var cfgPath = path is null ? GetProjectPath(CFGName) : Path.Combine(path, CFGName);
        var cfgStatement = program.entryPoint is null ? null : program.methodBodies[program.entryPoint];

        if (cfgStatement is not null) {
            var cfg = ControlFlowGraph.Create(cfgStatement);

            using var streamWriter = new StreamWriter(cfgPath);
            cfg.WriteTo(streamWriter);
        }
#endif
    }

    private void EmitBoundProgram(string path) {
        const string BoundProgramName = "BoundProgram.g.blt";
        var boundProgramPath = path is null ? BoundProgramName : Path.Combine(path, BoundProgramName);

        var program = boundProgram;
        Console.WriteLine($"Dumping bound program to \"{boundProgramPath}\"");

        var displayText = new DisplayText();

        foreach (var pair in program.methodBodies) {
            if (pair.Key.IsFromCompilation(this))
                CompilationExtensions.EmitTree(pair.Key, displayText, program);
        }

        using var streamWriter = new StreamWriter(boundProgramPath);
        var segments = displayText.Flush();

        foreach (var segment in segments) {
            if (segment.classification == Classification.Line)
                streamWriter.WriteLine();
            else if (segment.classification == Classification.Indent)
                streamWriter.Write(new string(' ', 4));
            else
                streamWriter.Write(segment.text);
        }

        streamWriter.Close();
    }

    private static string GetProjectPath(string fileName) {
        var appPath = Environment.GetCommandLineArgs()[0];
        var appDirectory = Path.GetDirectoryName(appPath);
        return Path.Combine(appDirectory, fileName);
    }

    private static void Log(bool log, Stopwatch timer, BelteDiagnosticQueue diagnostics, string message) {
        if (log) {
            timer.Stop();
            diagnostics.Push(new BelteDiagnostic(DiagnosticSeverity.Debug, message));
            timer.Restart();
        }
    }
}
