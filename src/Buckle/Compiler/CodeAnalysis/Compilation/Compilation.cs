using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Emitting;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.FlowAnalysis;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Shared;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Handles evaluation of program, and keeps track of Symbols.
/// </summary>
public sealed class Compilation {
    private BoundGlobalScope _globalScope;

    private Compilation(CompilationOptions options, Compilation previous, params SyntaxTree[] syntaxTrees) {
        this.previous = previous;
        this.options = options;
        diagnostics = new BelteDiagnosticQueue();

        foreach (var syntaxTree in syntaxTrees)
            diagnostics.Move(syntaxTree.GetDiagnostics());

        this.syntaxTrees = syntaxTrees.ToImmutableArray();
    }

    /// <summary>
    /// Diagnostics relating to the <see cref="Compilation" />.
    /// </summary>
    public BelteDiagnosticQueue diagnostics { get; }

    /// <summary>
    /// Options and flags for the compilation.
    /// </summary>
    internal CompilationOptions options { get; }

    /// <summary>
    /// The entry point of the program.
    /// </summary>
    internal MethodSymbol entryPoint => globalScope.entryPoint;

    /// <summary>
    /// All MethodSymbols in the global scope.
    /// </summary>
    internal ImmutableArray<MethodSymbol> methods => globalScope.methods;

    /// <summary>
    /// All VariableSymbols in the global scope.
    /// </summary>
    internal ImmutableArray<VariableSymbol> variables => globalScope.variables;

    /// <summary>
    /// All TypeSymbols in the global scope
    /// </summary>
    internal ImmutableArray<NamedTypeSymbol> types => globalScope.types;

    /// <summary>
    /// The SyntaxTrees of the parsed source files.
    /// </summary>
    internal ImmutableArray<SyntaxTree> syntaxTrees { get; }

    /// <summary>
    /// Previous <see cref="Compilation" />.
    /// </summary>
    internal Compilation previous { get; }

    /// <summary>
    /// The global scope (top level) of the program, contains Symbols.
    /// </summary>
    internal BoundGlobalScope globalScope {
        get {
            if (_globalScope is null)
                EnsureGlobalScope();

            return _globalScope;
        }
    }

    /// <summary>
    /// Creates a new <see cref="Compilation" /> with SyntaxTrees.
    /// </summary>
    /// <param name="transpilerMode">
    /// If the compiler output mode is a transpiler. Affects certain optimizations.
    /// </param>
    /// <param name="syntaxTrees">SyntaxTrees to use during compilation.</param>
    /// <returns>New <see cref="Compilation" />.</returns>
    public static Compilation Create(CompilationOptions options, params SyntaxTree[] syntaxTrees) {
        return new Compilation(options, null, syntaxTrees);
    }

    /// <summary>
    /// Creates a new script <see cref="Compilation" /> with SyntaxTrees, and the previous <see cref="Compilation" />.
    /// </summary>
    /// <param name="options">Additional flags and options for compilation.</param>
    /// <param name="previous">Previous <see cref="Compilation" />.</param>
    /// <param name="syntaxTrees">SyntaxTrees to use during compilation.</param>
    /// <returns>.</returns>
    public static Compilation CreateScript(
        CompilationOptions options, Compilation previous, params SyntaxTree[] syntaxTrees) {
        options.isScript = true;
        return new Compilation(options, previous, syntaxTrees);
    }

    /// <summary>
    /// Evaluates SyntaxTrees.
    /// </summary>
    /// <param name="variables">Existing variables to add to the scope.</param>
    /// <param name="abort">External flag used to cancel evaluation.</param>
    /// <returns>Result of evaluation (see <see cref="EvaluationResult" />).</returns>
    public EvaluationResult Evaluate(
        Dictionary<IVariableSymbol, IEvaluatorObject> variables, ValueWrapper<bool> abort) {
        if (globalScope.diagnostics.Errors().Any())
            return EvaluationResult.Failed(globalScope.diagnostics);

        var program = GetProgram();
#if DEBUG
        if (options.enableOutput)
            CreateCfg(program);
#endif

        if (program.diagnostics.Errors().Any())
            return EvaluationResult.Failed(program.diagnostics);

        diagnostics.Move(program.diagnostics);
        var eval = new Evaluator(program, variables);
        var evalResult = eval.Evaluate(abort, out var hasValue);

        diagnostics.Move(eval.diagnostics);
        var result = new EvaluationResult(evalResult, hasValue, diagnostics, eval.exceptions, eval.lastOutputWasPrint);
        return result;
    }

    /// <summary>
    /// Executes SyntaxTrees by creating an executable and running it.
    /// </summary>
    public void Execute() {
        if (globalScope.diagnostics.Errors().Any())
            return;

        var program = GetProgram();
#if DEBUG
        if (options.enableOutput)
            CreateCfg(program);
#endif

        if (program.diagnostics.Errors().Any())
            return;

        diagnostics.Move(program.diagnostics);
        Executor.Execute(program);
    }

    /// <summary>
    /// Compiles and evaluates SyntaxTrees chunk by chunk.
    /// </summary>
    /// <param name="variables">Existing variables to add to the scope.</param>
    /// <param name="abort">External flag used to cancel evaluation.</param>
    /// <returns>Result of evaluation (see <see cref="EvaluationResult" />).</returns>
    public EvaluationResult Interpret(
        Dictionary<IVariableSymbol, IEvaluatorObject> variables, ValueWrapper<bool> abort) {
        // syntaxTrees.Single() should have already been asserted by this point
        return Interpreter.Interpret(syntaxTrees[0], options, variables, abort);
    }

    /// <summary>
    /// Emits the program to a string.
    /// </summary>
    /// <param name="buildMode">Which emitter to use.</param>
    /// <param name="moduleName">
    /// Name of the module. If <param name="buildMode" /> is set to <see cref="BuildMode.CSharpTranspile" /> this is
    /// used as the namespace name instead.
    /// </param>
    /// <param name="references">
    /// .NET references, only applicable if <param name="buildMode" /> is set to <see cref="BuildMode.Dotnet" />.
    /// </param>
    /// <returns>Emitted program as a string. Diagnostics must be accessed manually off of this.</returns>
    public string EmitToString(BuildMode buildMode, string moduleName, string[] references = null) {
        if (diagnostics.Errors().Any())
            return null;

        var program = GetProgram();
        program.diagnostics.Move(diagnostics);

        if (program.diagnostics.Errors().Any())
            return null;

        if (buildMode == BuildMode.CSharpTranspile) {
            var content = CSharpEmitter.Emit(program, moduleName, out var emitterDiagnostics);
            diagnostics.Move(emitterDiagnostics);
            return content;
        } else if (buildMode == BuildMode.Dotnet) {
            var content = ILEmitter.Emit(program, moduleName, references, out var emitterDiagnostics);
            diagnostics.Move(emitterDiagnostics);
            return content;
        }

        return null;
    }

    /// <summary>
    /// Gets all Symbols across submissions (only global scope).
    /// </summary>
    /// <returns>All Symbols (checks all previous Compilations).</returns>
    public IEnumerable<ISymbol> GetSymbols() => GetSymbols<ISymbol>();

    /// <summary>
    /// Gets all Symbols of certain child type across submissions (only global scope).
    /// </summary>
    /// <typeparam name="T">Type of <see cref="Symbol" /> to get.</typeparam>
    /// <returns>Found symbols.</returns>
    public IEnumerable<T> GetSymbols<T>() where T : ISymbol {
        var submission = this;
        var seenSymbolNames = new HashSet<string>();
        var builtins = BuiltinMethods.GetAll();

        while (submission != null) {
            foreach (var method in submission.methods) {
                if (seenSymbolNames.Add(method.Signature()) && method is T t)
                    yield return t;
            }

            foreach (var builtin in builtins) {
                if (seenSymbolNames.Add(builtin.Signature()) && builtin is T t)
                    yield return t;
            }

            foreach (var variable in submission.variables) {
                if (seenSymbolNames.Add(variable.name) && variable is T t)
                    yield return t;
            }

            foreach (var type in submission.types) {
                if (seenSymbolNames.Add(type.name) && type is T t)
                    yield return t;
            }

            submission = submission.previous;
        }
    }

    /// <summary>
    /// Emits the program to an assembly.
    /// </summary>
    /// <param name="buildMode">Which emitter to use.</param>
    /// <param name="moduleName">Application name.</param>
    /// <param name="references">All external references (.NET).</param>
    /// <param name="outputPath">Where to put the application once assembled.</param>
    /// <param name="finishStage">
    /// What stage to finish at (only applicable if <param name="buildMode" /> is set to
    /// <see cref="BuildMode.Independent" />.
    /// </param>
    /// <returns>Diagnostics.</returns>
    internal BelteDiagnosticQueue Emit(
        BuildMode buildMode, string moduleName, string[] references, string outputPath, CompilerStage _) {
        if (diagnostics.Errors().Any())
            return diagnostics;

        var program = GetProgram();
        program.diagnostics.Move(diagnostics);

        if (program.diagnostics.Errors().Any())
            return program.diagnostics;

        if (buildMode == BuildMode.Dotnet)
            return ILEmitter.Emit(program, moduleName, references, outputPath);
        else if (buildMode == BuildMode.CSharpTranspile)
            return CSharpEmitter.Emit(program, outputPath);
        else
            diagnostics.Push(Fatal.Unsupported.IndependentCompilation());

        return diagnostics;
    }

    /// <summary>
    /// Gets the previous <see cref="BoundProgram" />, and binds a new one.
    /// </summary>
    /// <returns>Newly bound <see cref="BoundProgram" />.</returns>
    internal BoundProgram GetProgram() {
        var previous = this.previous?.GetProgram();
        return Binder.BindProgram(options, previous, globalScope);
    }

    /// <summary>
    /// Binds the global scope if it hasn't been bound already. Does not return anything to indicate if the global scope
    /// was bound or already bound, but after this method is called the global scope is guaranteed to have been bound.
    /// </summary>
    internal void EnsureGlobalScope() {
        var tempScope = Binder.BindGlobalScope(options, previous?.globalScope, syntaxTrees);
        // Makes assignment thread-safe, if multiple threads try to initialize they use whoever did it first
        Interlocked.CompareExchange(ref _globalScope, tempScope, null);
    }

    private static void CreateCfg(BoundProgram program) {
        var appPath = Environment.GetCommandLineArgs()[0];
        var appDirectory = Path.GetDirectoryName(appPath);
        var cfgPath = Path.Combine(appDirectory, "cfg.dot");
        var cfgStatement = program.entryPoint is null ? null : program.methodBodies[program.entryPoint];

        if (cfgStatement != null) {
            var cfg = ControlFlowGraph.Create(cfgStatement);

            using var streamWriter = new StreamWriter(cfgPath);
            cfg.WriteTo(streamWriter);
        }
    }
}
