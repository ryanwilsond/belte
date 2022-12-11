using System;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.IO;
using Buckle.Diagnostics;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Emitting;
using Diagnostics;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Result of an evaluation, including diagnostics.
/// </summary>
internal sealed class EvaluationResult {
    /// <summary>
    /// Creates an evaluation result, given the result and diagnostics (does no computation).
    /// </summary>
    /// <param name="value">Result of evaluation</param>
    /// <param name="diagnostics">Diagnostics associated with value</param>
    internal EvaluationResult(object value, BelteDiagnosticQueue diagnostics) {
        this.value = value;
        this.diagnostics = new BelteDiagnosticQueue();
        this.diagnostics.Move(diagnostics);
    }

    /// <summary>
    /// Creates an empty evaluation result.
    /// </summary>
    internal EvaluationResult() : this(null, null) { }

    /// <summary>
    /// Diagnostics related to a single evaluation.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// Value resulting from evaluation.
    /// </summary>
    internal object value { get; set; }
}

/// <summary>
/// Handles evaluation of program, and keeps track of symbols (mainly for REPL use).
/// </summary>
public sealed class Compilation {
    private BoundGlobalScope globalScope_;

    private Compilation(bool isScript, Compilation previous, params SyntaxTree[] syntaxTrees) {
        this.isScript = isScript;
        this.previous = previous;
        diagnostics = new BelteDiagnosticQueue();

        foreach (var syntaxTree in syntaxTrees)
            diagnostics.Move(syntaxTree.diagnostics);

        this.syntaxTrees = syntaxTrees.ToImmutableArray<SyntaxTree>();
    }

    /// <summary>
    /// Diagnostics relating to compilation.
    /// </summary>
    public BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// The main function/entry point of the program.
    /// </summary>
    internal FunctionSymbol mainFunction => globalScope.mainFunction;

    /// <summary>
    /// All function symbols in the global scope.
    /// </summary>
    internal ImmutableArray<FunctionSymbol> functions => globalScope.functions;

    /// <summary>
    /// All variable symbols in the global scope.
    /// </summary>
    internal ImmutableArray<VariableSymbol> variables => globalScope.variables;

    /// <summary>
    /// The syntax trees of the parsed source files.
    /// </summary>
    internal ImmutableArray<SyntaxTree> syntaxTrees { get; }

    /// <summary>
    /// Previous compilation (used for REPL submission chaining).
    /// </summary>
    internal Compilation previous { get; }

    /// <summary>
    /// The compilation is a script to run top down versus being an application with an entry point.
    /// </summary>
    internal bool isScript { get; }

    /// <summary>
    /// The global scope (top level) of the program, contains symbols.
    /// </summary>
    internal BoundGlobalScope globalScope {
        get {
            if (globalScope_ == null) {
                var tempScope = Binder.BindGlobalScope(isScript, previous?.globalScope, syntaxTrees);
                // Makes assignment thread-safe, if multiple threads try to initialize they use whoever did it first
                Interlocked.CompareExchange(ref globalScope_, tempScope, null);
            }

            return globalScope_;
        }
    }

    /// <summary>
    /// Creates a new compilation with syntax trees.
    /// </summary>
    /// <param name="syntaxTrees">Trees to use in compilation</param>
    /// <returns>New compilation</returns>
    internal static Compilation Create(params SyntaxTree[] syntaxTrees) {
        return new Compilation(false, null, syntaxTrees);
    }

    /// <summary>
    /// Creates a new script compilation with syntax trees, and the previous compilation.
    /// </summary>
    /// <param name="previous">Previous compilation</param>
    /// <param name="syntaxTrees">Trees to use in compilation</param>
    /// <returns></returns>
    internal static Compilation CreateScript(Compilation previous, params SyntaxTree[] syntaxTrees) {
        return new Compilation(true, previous, syntaxTrees);
    }

    /// <summary>
    /// Gets all symbols across submissions (only global scope).
    /// </summary>
    /// <returns>All symbols (checks all previous compilations)</returns>
    internal IEnumerable<Symbol> GetSymbols() {
        var submission = this;
        var seenSymbolNames = new HashSet<string>();
        var builtins = BuiltinFunctions.GetAll();
        // TODO Does not show overloads

        while (submission != null) {
            foreach (var function in submission.functions)
                if (seenSymbolNames.Add(function.name))
                    yield return function;

            foreach (var variable in submission.variables)
                if (seenSymbolNames.Add(variable.name))
                    yield return variable;

            foreach (var builtin in builtins)
                if (seenSymbolNames.Add(builtin.name))
                    yield return builtin;

            submission = submission.previous;
        }
    }

    /// <summary>
    /// Evaluates trees.
    /// </summary>
    /// <param name="variables">Existing variables to add to the scope</param>
    /// <returns>Result of evaluation (see EvaluationResult)</returns>
    internal EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables) {
        if (globalScope.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return new EvaluationResult(null, globalScope.diagnostics);

        var program = GetProgram();
        // * Only for debugging purposes
        // TODO Update this function to work
        // CreateCfg(program);

        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return new EvaluationResult(null, program.diagnostics);

        diagnostics.Move(program.diagnostics);
        var eval = new Evaluator(program, variables);
        var evalResult = eval.Evaluate();

        if (eval.hasPrint)
            Console.WriteLine();

        diagnostics.Move(eval.diagnostics);
        var result = new EvaluationResult(evalResult, diagnostics);
        return result;
    }

    /// <summary>
    /// Emits the parse tree of the compilation.
    /// </summary>
    /// <param name="writer">Out</param>
    internal void EmitTree(TextWriter writer) {
        if (globalScope.mainFunction != null)
            EmitTree(globalScope.mainFunction, writer);
        else if (globalScope.scriptFunction != null)
            EmitTree(globalScope.scriptFunction, writer);
    }

    /// <summary>
    /// Emits the parse tree of a single symbol.
    /// </summary>
    /// <param name="symbol">Symbol to be the root of the tree displayed</param>
    /// <param name="writer">Out</param>
    internal void EmitTree(Symbol symbol, TextWriter writer) {
        var program = GetProgram();

        if (symbol is FunctionSymbol f) {
            f.WriteTo(writer);
            if (!program.functionBodies.TryGetValue(f, out var body)) {
                writer.WriteLine();
                return;
            }

            body.WriteTo(writer);
        } else {
            symbol.WriteTo(writer);
        }
    }

    /// <summary>
    /// Emits the program to an assembly.
    /// </summary>
    /// <param name="moduleName">Application name</param>
    /// <param name="references">All external references (.NET)</param>
    /// <param name="outputPath">Where to put the application once assembled</param>
    /// <returns>Diagnostics</returns>
    internal BelteDiagnosticQueue Emit(string moduleName, string[] references, string outputPath) {
        foreach (var syntaxTree in syntaxTrees)
            diagnostics.Move(syntaxTree.diagnostics);

        if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return diagnostics;

        var program = GetProgram();
        program.diagnostics.Move(diagnostics);
        return Emitter.Emit(program, moduleName, references, outputPath);
    }

    private BoundProgram GetProgram() {
        var previous_ = previous == null ? null : previous.GetProgram();
        return Binder.BindProgram(isScript, previous_, globalScope);
    }

    private static void CreateCfg(BoundProgram program) {
        var appPath = Environment.GetCommandLineArgs()[0];
        var appDirectory = Path.GetDirectoryName(appPath);
        var cfgPath = Path.Combine(appDirectory, "cfg.dot");
        BoundBlockStatement cfgStatement = program.scriptFunction == null
            ? program.functionBodies[program.mainFunction]
            : program.functionBodies[program.scriptFunction];
        var cfg = ControlFlowGraph.Create(cfgStatement);

        using (var streamWriter = new StreamWriter(cfgPath))
            cfg.WriteTo(streamWriter);
    }
}
