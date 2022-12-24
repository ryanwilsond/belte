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
using Buckle.CodeAnalysis.Evaluating;
using static Buckle.Utilities.FunctionUtilities;
using Diagnostics;
using System.CodeDom.Compiler;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Handles evaluation of program, and keeps track of Symbols (mainly for <see cref="BelteRepl" /> use).
/// </summary>
public sealed class Compilation {
    private BoundGlobalScope _globalScope;

    private Compilation(bool isScript, Compilation previous, params SyntaxTree[] syntaxTrees) {
        this.isScript = isScript;
        this.previous = previous;
        diagnostics = new BelteDiagnosticQueue();

        foreach (var syntaxTree in syntaxTrees)
            diagnostics.Move(syntaxTree.diagnostics);

        this.syntaxTrees = syntaxTrees.ToImmutableArray<SyntaxTree>();
    }

    /// <summary>
    /// Diagnostics relating to the <see cref="Compilation" />.
    /// </summary>
    public BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// The main function/entry point of the program.
    /// </summary>
    internal FunctionSymbol mainFunction => globalScope.mainFunction;

    /// <summary>
    /// All FunctionSymbols in the global scope.
    /// </summary>
    internal ImmutableArray<FunctionSymbol> functions => globalScope.functions;

    /// <summary>
    /// All VariableSymbols in the global scope.
    /// </summary>
    internal ImmutableArray<VariableSymbol> variables => globalScope.variables;

    /// <summary>
    /// All TypeSymbols in the global scope
    /// </summary>
    internal ImmutableArray<TypeSymbol> types => globalScope.types;

    /// <summary>
    /// The SyntaxTrees of the parsed source files.
    /// </summary>
    internal ImmutableArray<SyntaxTree> syntaxTrees { get; }

    /// <summary>
    /// Previous <see cref="Compilation" /> (used for <see cref="BelteRepl" /> submission chaining).
    /// </summary>
    internal Compilation previous { get; }

    /// <summary>
    /// If the compilation is a script to run top down versus being an application with an entry point.
    /// </summary>
    internal bool isScript { get; }

    /// <summary>
    /// The global scope (top level) of the program, contains Symbols.
    /// </summary>
    internal BoundGlobalScope globalScope {
        get {
            if (_globalScope == null) {
                var tempScope = Binder.BindGlobalScope(isScript, previous?.globalScope, syntaxTrees);
                // Makes assignment thread-safe, if multiple threads try to initialize they use whoever did it first
                Interlocked.CompareExchange(ref _globalScope, tempScope, null);
            }

            return _globalScope;
        }
    }

    /// <summary>
    /// Creates a new <see cref="Compilation" /> with SyntaxTrees.
    /// </summary>
    /// <param name="syntaxTrees">SyntaxTrees to use during compilation.</param>
    /// <returns>New <see cref="Compilation" />.</returns>
    internal static Compilation Create(params SyntaxTree[] syntaxTrees) {
        return new Compilation(false, null, syntaxTrees);
    }

    /// <summary>
    /// Creates a new script <see cref="Compilation" /> with SyntaxTrees, and the previous <see cref="Compilation" />.
    /// </summary>
    /// <param name="previous">Previous <see cref="Compilation" />.</param>
    /// <param name="syntaxTrees">SyntaxTrees to use during compilation.</param>
    /// <returns>.</returns>
    internal static Compilation CreateScript(Compilation previous, params SyntaxTree[] syntaxTrees) {
        return new Compilation(true, previous, syntaxTrees);
    }

    /// <summary>
    /// Gets all Symbols across submissions (only global scope).
    /// </summary>
    /// <returns>All Symbols (checks all previous Compilations).</returns>
    internal IEnumerable<Symbol> GetSymbols() => GetSymbols<Symbol>();

    /// <summary>
    /// Gets all Symbols of certain child type across submissions (only global scope).
    /// </summary>
    /// <typeparam name="T">Type of <see cref="Symbol" /> to get.</typeparam>
    /// <returns>Found symbols.</returns>
    internal IEnumerable<T> GetSymbols<T>() where T : Symbol {
        var submission = this;
        var seenFunctions = new HashSet<FunctionSymbol>();
        var seenSymbolNames = new HashSet<string>();
        var builtins = BuiltinFunctions.GetAll();

        while (submission != null) {
            foreach (var function in submission.functions)
                if (seenFunctions.Add(function) && function is T)
                    yield return function as T;

            foreach (var builtin in builtins)
                if (seenFunctions.Add(builtin) && builtin is T)
                    yield return builtin as T;

            foreach (var variable in submission.variables)
                if (seenSymbolNames.Add(variable.name) && variable is T)
                    yield return variable as T;

            foreach (var @type in submission.types)
                if (seenSymbolNames.Add(@type.name) && @type is T)
                    yield return @type as T;

            submission = submission.previous;
        }
    }

    /// <summary>
    /// Evaluates SyntaxTrees.
    /// </summary>
    /// <param name="variables">Existing variables to add to the scope.</param>
    /// <returns>Result of evaluation (see <see cref="EvaluationResult" />).</returns>
    internal EvaluationResult Evaluate(
        Dictionary<VariableSymbol, EvaluatorObject> variables, bool wError = false) {
        if (globalScope.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return new EvaluationResult(null, false, globalScope.diagnostics, null);

        var program = GetProgram();
        // * Only for debugging purposes
        // CreateCfg(program);

        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any() || (program.diagnostics.Any() && wError))
            return new EvaluationResult(null, false, program.diagnostics, null);

        diagnostics.Move(program.diagnostics);
        var eval = new Evaluator(program, variables);
        var evalResult = eval.Evaluate(out var hasValue);

        if (eval.hasPrint)
            Console.WriteLine();

        diagnostics.Move(eval.diagnostics);
        var result = new EvaluationResult(evalResult, hasValue, diagnostics, eval.exceptions);
        return result;
    }

    /// <summary>
    /// Emits the parse tree of the compilation.
    /// </summary>
    /// <param name="writer">Out.</param>
    internal void EmitTree(TextWriter writer) {
        if (globalScope.mainFunction != null)
            EmitTree(globalScope.mainFunction, writer);
        else if (globalScope.scriptFunction != null)
            EmitTree(globalScope.scriptFunction, writer);
    }

    /// <summary>
    /// Emits the parse tree of a single <see cref="Symbol" />.
    /// </summary>
    /// <param name="symbol"><see cref="Symbol" /> to be the root of the <see cref="SyntaxTree" /> displayed.</param>
    /// <param name="writer">Out.</param>
    internal void EmitTree(Symbol symbol, TextWriter writer) {
        var program = GetProgram();

        void WriteStructMembers(StructSymbol @struct, bool writeEnding = true) {
            var indentedWriter = new IndentedTextWriter(writer);

            try {
                var members = program.structMembers[@struct];
                indentedWriter.WriteSpace();
                indentedWriter.WritePunctuation(SyntaxKind.OpenBraceToken);
                indentedWriter.WriteLine();
                indentedWriter.Indent++;

                foreach (var field in members) {
                    field.WriteTo(indentedWriter);
                    indentedWriter.WriteLine();
                }

                indentedWriter.Indent--;
                indentedWriter.WritePunctuation(SyntaxKind.CloseBraceToken);
                indentedWriter.WriteLine();
            } catch (BelteInternalException) {
                if (writeEnding) {
                    indentedWriter.WritePunctuation(SyntaxKind.SemicolonToken);
                    indentedWriter.WriteLine();
                }
            }
        }

        if (symbol is FunctionSymbol f) {
            f.WriteTo(writer);

            try {
                var body = LookupMethod(program.functionBodies, f);
                writer.WriteSpace();
                body.WriteTo(writer);
            } catch (BelteInternalException) {
                // If the body could not be found, it probably means it is a builtin
                // In that case only showing the signature is what we want
                writer.WritePunctuation(SyntaxKind.SemicolonToken);
                writer.WriteLine();
            }
        } else if (symbol is StructSymbol t) {
            t.WriteTo(writer);
            WriteStructMembers(t);
        } else if (symbol is VariableSymbol v) {
            v.WriteTo(writer);

            if (v.typeClause.type is StructSymbol s && v.typeClause.dimensions == 0)
                WriteStructMembers(s);
            else
                writer.WriteLine();
        }
    }

    /// <summary>
    /// Emits the program to an assembly.
    /// </summary>
    /// <param name="moduleName">Application name.</param>
    /// <param name="references">All external references (.NET).</param>
    /// <param name="outputPath">Where to put the application once assembled.</param>
    /// <returns>Diagnostics.</returns>
    internal BelteDiagnosticQueue Emit(string moduleName, string[] references, string outputPath, bool wError) {
        foreach (var syntaxTree in syntaxTrees)
            diagnostics.Move(syntaxTree.diagnostics);

        if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return diagnostics;

        var program = GetProgram();
        program.diagnostics.Move(diagnostics);

        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any() || (program.diagnostics.Any() && wError))
            return program.diagnostics;

        return Emitter.Emit(program, moduleName, references, outputPath);
    }

    private BoundProgram GetProgram() {
        var _previous = previous == null ? null : previous.GetProgram();
        return Binder.BindProgram(isScript, _previous, globalScope);
    }

    private static void CreateCfg(BoundProgram program) {
        var appPath = Environment.GetCommandLineArgs()[0];
        var appDirectory = Path.GetDirectoryName(appPath);
        var cfgPath = Path.Combine(appDirectory, "cfg.dot");
        var cfgStatement = program.scriptFunction == null && program.mainFunction == null
            ? null
            : program.scriptFunction == null
                ? program.functionBodies[program.mainFunction]
                : program.functionBodies[program.scriptFunction];

        if (cfgStatement != null) {
            var cfg = ControlFlowGraph.Create(cfgStatement);

            using (var streamWriter = new StreamWriter(cfgPath))
                cfg.WriteTo(streamWriter);
        }
    }
}
