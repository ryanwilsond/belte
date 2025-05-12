using System;
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
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;
using Shared;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Handles evaluation of program, and keeps track of Symbols.
/// </summary>
public sealed class Compilation {
    private readonly static Predicate<Symbol> SkipLibrariesFilter
        = type => type is not SynthesizedFinishedNamedTypeSymbol;

    private readonly SyntaxAndDeclarationManager _syntax;
    private NamespaceSymbol _lazyGlobalNamespace;
    private WeakReference<BinderFactory>[] _binderFactories;
    private BelteDiagnosticQueue _lazyDeclarationDiagnostics;
    private BoundProgram _lazyBoundProgram;
    private BelteDiagnosticQueue _lazyMethodDiagnostics;
    private List<LocalFunctionRewriter.Analysis> _lazyPreviousAnalyses;
    private MethodSymbol _lazyEntryPoint;
    private NamedTypeSymbol _lazyScriptClass;

    private Compilation(
        string assemblyName,
        CompilationOptions options,
        Compilation previous,
        SyntaxAndDeclarationManager syntax) {
        this.assemblyName = assemblyName;
        this.options = options;
        this.previous = previous;
        _syntax = syntax;
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

    internal static bool KeepLookingForCorTypes => CorLibrary.StillLookingForSpecialTypes();

    internal NamespaceSymbol globalNamespaceInternal {
        get {
            if (_lazyGlobalNamespace is null) {
                var extent = new NamespaceExtent(this);
                var result = new GlobalNamespaceSymbol(extent, _syntax.state.declarationTable.GetMergedRoot(this));
                Interlocked.CompareExchange(ref _lazyGlobalNamespace, result, null);
            }

            return _lazyGlobalNamespace;
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

    public ImmutableArray<ISymbol> GetSymbols(bool includePreviousCompilations = false, bool includeExternal = false) {
        if (!includePreviousCompilations)
            return globalNamespace.GetMembers();

        // TODO Cache this lookup?
        // TODO Eventually flesh out this function to support more options (filtering, sorting, etc.)
        var builder = ArrayBuilder<ISymbol>.GetInstance();
        var current = this;

        while (current is not null) {
            if (includeExternal) {
                builder.AddRange(current.globalNamespace.GetMembers());
            } else {
                foreach (var member in current.globalNamespace.GetMembers()) {
                    if (member is not SynthesizedFinishedNamedTypeSymbol)
                        builder.Add(member);
                }
            }

            current = current.previous;
        }

        return builder.ToImmutableAndFree();
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

    public EvaluationResult Evaluate(ValueWrapper<bool> abort, bool logTime = false) {
        return Evaluate(new EvaluatorContext(options), abort, logTime);
    }

    public EvaluationResult Evaluate(
        EvaluatorContext context,
        ValueWrapper<bool> abort,
        bool logTime = false) {
        var timer = logTime ? Stopwatch.StartNew() : null;
        var builder = GetDiagnostics();
        var program = boundProgram;

#if DEBUG
        if (options.enableOutput) {
            if (!builder.AnyErrors())
                EmitCFG();

            EmitBoundProgram();
        }
#endif

        Log(logTime, timer, builder, $"Bound the program in {timer?.ElapsedMilliseconds} ms");

        if (logTime) {
            timer.Stop();
            builder.Push(new BelteDiagnostic(
                DiagnosticSeverity.Debug,
                $"Bound the program in {timer.ElapsedMilliseconds} ms"
            ));
            timer.Restart();
        }

        if (builder.AnyErrors())
            return EvaluationResult.Failed(builder);

        var evaluator = new Evaluator(program, context, options.arguments);
        var evalResult = evaluator.Evaluate(abort, out var hasValue);

        Log(logTime, timer, builder, $"Evaluated the program in {timer?.ElapsedMilliseconds} ms");

        var result = new EvaluationResult(
            evalResult,
            hasValue,
            builder,
            evaluator.exceptions,
            evaluator.lastOutputWasPrint,
            evaluator.containsIO
        );

        return result;
    }

    public BelteDiagnosticQueue Emit(string outputPath, bool logTime = false) {
        var timer = logTime ? Stopwatch.StartNew() : null;
        var builder = GetDiagnostics();
        var program = boundProgram;

        Log(logTime, timer, builder, $"Compiled in {timer?.ElapsedMilliseconds} ms");

        if (builder.AnyErrors())
            return builder;

        if (options.buildMode == BuildMode.Dotnet)
            // return ILEmitter.Emit(program, moduleName, references, outputPath);
            builder.Push(Fatal.Unsupported.DotnetCompilation());
        else if (options.buildMode == BuildMode.CSharpTranspile)
            return CSharpEmitter.Emit(program, outputPath);
        else if (options.buildMode == BuildMode.Independent)
            builder.Push(Fatal.Unsupported.IndependentCompilation());

        if (options.buildMode is BuildMode.Dotnet or BuildMode.CSharpTranspile)
            Log(logTime, timer, builder, $"Emitted the program in {timer?.ElapsedMilliseconds} ms");

        return builder;
    }

    public BelteDiagnosticQueue Execute() {
        var builder = GetDiagnostics();

        if (builder.AnyErrors())
            return builder;

#if DEBUG
        if (options.enableOutput) {
            if (!builder.AnyErrors())
                EmitCFG();

            EmitBoundProgram();
        }
#endif

        Executor.Execute(boundProgram, options.arguments);
        return builder;
    }

    public EvaluationResult Interpret(ValueWrapper<bool> abort) {
        return Interpreter.Interpret(_syntax.syntaxTrees[0], options, abort);
    }

    public string EmitToString(out BelteDiagnosticQueue diagnostics, BuildMode? alternateBuildMode = null) {
        var buildMode = alternateBuildMode ?? options.buildMode;
        diagnostics = GetDiagnostics();
        var program = boundProgram;

        if (diagnostics.AnyErrors())
            return null;

        if (buildMode == BuildMode.CSharpTranspile) {
            var content = CSharpEmitter.Emit(program, assemblyName, out var emitterDiagnostics);
            diagnostics.Move(emitterDiagnostics);
            return content;
        } else if (buildMode == BuildMode.Dotnet) {
            var content = ILEmitter.Emit(program, assemblyName, options.references, out var emitterDiagnostics);
            diagnostics.Move(emitterDiagnostics);
            return content;
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

    internal MethodSymbol GetEntryPoint(BelteDiagnosticQueue diagnostics) {
        if (_lazyEntryPoint is null) {
            var simpleEntryPoint = SynthesizedEntryPoint.GetSimpleProgramEntryPoint(this);
            Interlocked.CompareExchange(ref _lazyEntryPoint, FindEntryPoint(simpleEntryPoint, diagnostics), null);
        }

        return _lazyEntryPoint;
    }

    private MethodSymbol FindEntryPoint(
        SynthesizedEntryPoint simpleEntryPoint,
        BelteDiagnosticQueue diagnostics) {
        var builder = ArrayBuilder<MethodSymbol>.GetInstance();

        var programClasses = globalNamespaceInternal
            .GetMembers(WellKnownMemberNames.TopLevelStatementsEntryPointTypeName);

        if (programClasses.Length > 0) {
            var programClass = (NamedTypeSymbol)programClasses[0];

            foreach (var member in programClass.GetMembers(WellKnownMemberNames.EntryPointMethodName)) {
                if (member is MethodSymbol m and not SynthesizedEntryPoint && HasEntryPointSignature(m))
                    builder.Add(m);
            }
        }

        builder.AddIfNotNull(simpleEntryPoint);
        var entryPointCandidates = builder.ToImmutableAndFree();
        var expectedCount = simpleEntryPoint is null ? 1 : 2;

        MethodSymbol entryPoint = null;

        if (entryPointCandidates.Length == 0 && !options.isScript && options.outputKind != OutputKind.Library) {
            diagnostics.Push(Error.NoSuitableEntryPoint());
        } else if (entryPointCandidates.Length == 1) {
            entryPoint = entryPointCandidates[0];
        } else if (entryPointCandidates.Length > 1) {
            if (entryPointCandidates.Length > expectedCount)
                diagnostics.Push(Error.MultipleMains(entryPointCandidates[0].location));

            if (entryPointCandidates.Length > 1 && simpleEntryPoint is not null)
                diagnostics.Push(Error.MainAndGlobals(entryPointCandidates[0].location));
        }

        return entryPoint;
    }

    private static bool HasEntryPointSignature(MethodSymbol method) {
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

        if (firstType.specialType != SpecialType.List)
            return false;

        var elementType = ((NamedTypeSymbol)firstType).templateArguments[0].type;

        if (elementType.specialType != SpecialType.String)
            return false;

        return true;
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

    internal BinderFactory GetBinderFactory(SyntaxTree syntaxTree) {
        var treeOrdinal = GetSyntaxTreeOrdinal(syntaxTree);
        var binderFactories = _binderFactories;

        if (binderFactories is null) {
            binderFactories = new WeakReference<BinderFactory>[syntaxTrees.Length];
            binderFactories = Interlocked.CompareExchange(ref _binderFactories, binderFactories, null)
                ?? binderFactories;
        }

        var previousWeakReference = binderFactories[treeOrdinal];

        if (previousWeakReference is not null && previousWeakReference.TryGetTarget(out var previousFactory))
            return previousFactory;

        return AddNewFactory(syntaxTree, ref binderFactories[treeOrdinal]);
    }

    internal int GetSyntaxTreeOrdinal(SyntaxTree syntaxTree) {
        return _syntax.state.ordinalMap[syntaxTree];
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

    private Compilation Update(SyntaxAndDeclarationManager syntax) {
        return new Compilation(assemblyName, options, previous, syntax);
    }

    private BinderFactory AddNewFactory(SyntaxTree syntaxTree, ref WeakReference<BinderFactory> slot) {
        var newFactory = new BinderFactory(this, syntaxTree);
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

    private static Compilation Create(
        string assemblyName,
        CompilationOptions options,
        Compilation previous,
        IEnumerable<SyntaxTree> syntaxTrees) {
        var compilation = new Compilation(
            assemblyName,
            options,
            previous,
            new SyntaxAndDeclarationManager([], null)
        );

        if (syntaxTrees is not null)
            compilation = compilation.AddSyntaxTrees(syntaxTrees);

        return compilation;
    }

    private BelteDiagnosticQueue GetDiagnostics(bool includeParse, bool includeDeclaration, bool includeMethods) {
        var builder = new BelteDiagnosticQueue();

        if (includeParse) {
            foreach (var syntaxTree in _syntax.syntaxTrees)
                builder.PushRange(syntaxTree.GetDiagnostics());
        }

        if (includeDeclaration) {
            globalNamespaceInternal.ForceComplete(null);
            builder.PushRange(declarationDiagnostics);
        }

        if (includeMethods)
            builder.PushRange(methodDiagnostics);

        return builder;
    }

    private void EnsureBoundProgramAndMethodDiagnostics() {
        if (_lazyBoundProgram is null)
            CreateBoundProgramAndMethodDiagnostics();
    }

    private void CreateBoundProgramAndMethodDiagnostics() {
        var emitting = options.buildMode is not BuildMode.CSharpTranspile;
        _lazyMethodDiagnostics = new BelteDiagnosticQueue();
        _lazyBoundProgram = MethodCompiler.CompileMethodBodies(
            this,
            _lazyMethodDiagnostics,
            SkipLibrariesFilter,
            emitting
        );
    }

    private void EmitCFG() {
        var program = boundProgram;
        var cfgPath = GetProjectPath("cfg.dot");
        var cfgStatement = program.entryPoint is null ? null : program.methodBodies[program.entryPoint];

        if (cfgStatement is not null) {
            var cfg = ControlFlowGraph.Create(cfgStatement);

            using var streamWriter = new StreamWriter(cfgPath);
            cfg.WriteTo(streamWriter);
        }
    }

    private void EmitBoundProgram() {
        var program = boundProgram;
        var programPath = GetProjectPath("program.blt");
        var displayText = new DisplayText();

        foreach (var pair in program.methodBodies)
            CompilationExtensions.EmitTree(pair.Key, displayText, program);

        using var streamWriter = new StreamWriter(programPath);
        var segments = displayText.Flush();

        foreach (var segment in segments) {
            if (segment.classification == Classification.Line)
                streamWriter.WriteLine();
            else if (segment.classification == Classification.Indent)
                streamWriter.Write(new string(' ', 4));
            else
                streamWriter.Write(segment.text);
        }
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
