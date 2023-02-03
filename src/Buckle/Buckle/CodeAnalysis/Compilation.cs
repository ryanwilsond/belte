using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Emitting;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Diagnostics;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;
using static Buckle.Utilities.FunctionUtilities;

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
        var seenSymbolNames = new HashSet<string>();
        var builtins = BuiltinFunctions.GetAll();

        while (submission != null) {
            foreach (var function in submission.functions)
                if (seenSymbolNames.Add(function.SignatureNoReturnNoParameterNames()) && function is T)
                    yield return function as T;

            foreach (var builtin in builtins)
                if (seenSymbolNames.Add(builtin.SignatureNoReturnNoParameterNames()) && builtin is T)
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
        Dictionary<VariableSymbol, EvaluatorObject> variables, ref bool abort, bool wError = false) {
        if (globalScope.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return new EvaluationResult(null, false, globalScope.diagnostics, null);

        var program = GetProgram();
        // * Only for debugging purposes
        // CreateCfg(program);

        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any() || (program.diagnostics.Any() && wError))
            return new EvaluationResult(null, false, program.diagnostics, null);

        diagnostics.Move(program.diagnostics);
        var eval = new Evaluator(program, variables);
        var evalResult = eval.Evaluate(ref abort, out var hasValue);

        if (eval.hasPrint)
            Console.WriteLine();

        diagnostics.Move(eval.diagnostics);
        var result = new EvaluationResult(evalResult, hasValue, diagnostics, eval.exceptions);
        return result;
    }

    // TODO Consider moving these methods to an extensions class under the Display namespace
    /// <summary>
    /// Emits the parse tree of the compilation.
    /// </summary>
    /// <param name="text">Out.</param>
    internal void EmitTree(DisplayText text) {
        if (globalScope.mainFunction != null) {
            EmitTree(globalScope.mainFunction, text);
        } else if (globalScope.scriptFunction != null) {
            EmitTree(globalScope.scriptFunction, text);
        } else {
            var program = GetProgram();

            foreach (var pair in program.functionBodies.OrderBy(p => p.Key.name))
                EmitTree(pair.Key, text);
        }
    }

    /// <summary>
    /// Emits the parse tree of a single <see cref="FunctionSymbol" /> after attempting to find it based on name.
    /// Note: this only searches for functions, so if the name of another type of <see cref="Symbol" /> is passed it
    /// will not be found.
    /// </summary>
    /// <param name="name">
    /// The name of the <see cref="FunctionSymbol" /> to search for and then print. If not found, throws.
    /// </param>
    /// <param name="text">Out.</param>
    internal void EmitTree(string name, DisplayText text) {
        var program = GetProgram();
        var pair = LookupMethodFromParentsFromName(program, name);
        SymbolDisplay.DisplaySymbol(text, pair.Item1);
        text.Write(CreateSpace());
        DisplayText.DisplayNode(text, pair.Item2);
    }

    /// <summary>
    /// Emits the parse tree of a single <see cref="Symbol" />.
    /// </summary>
    /// <param name="symbol"><see cref="Symbol" /> to be the root of the <see cref="SyntaxTree" /> displayed.</param>
    /// <param name="text">Out.</param>
    internal void EmitTree(Symbol symbol, DisplayText text) {
        var program = GetProgram();

        void WriteStructMembers(StructSymbol @struct, bool writeEnding = true) {
            try {
                var members = program.structMembers[@struct];
                text.Write(CreateSpace());
                text.Write(CreatePunctuation(SyntaxKind.OpenBraceToken));
                text.Write(CreateLine());
                text.indent++;

                foreach (var field in members) {
                    SymbolDisplay.DisplaySymbol(text, field);
                    text.Write(CreateLine());
                }

                text.indent--;
                text.Write(CreatePunctuation(SyntaxKind.CloseBraceToken));
                text.Write(CreateLine());
            } catch (BelteInternalException) {
                if (writeEnding) {
                    text.Write(CreatePunctuation(SyntaxKind.SemicolonToken));
                    text.Write(CreateLine());
                }
            }
        }

        if (symbol is FunctionSymbol f) {
            SymbolDisplay.DisplaySymbol(text, f);

            try {
                var body = LookupMethodFromParents(program, f);
                text.Write(CreateSpace());
                DisplayText.DisplayNode(text, body);
            } catch (BelteInternalException) {
                // If the body could not be found, it probably means it is a builtin
                // In that case only showing the signature is what we want
                text.Write(CreatePunctuation(SyntaxKind.SemicolonToken));
                text.Write(CreateLine());
            }
        } else if (symbol is StructSymbol t) {
            SymbolDisplay.DisplaySymbol(text, t);
            WriteStructMembers(t);
        } else if (symbol is VariableSymbol v) {
            SymbolDisplay.DisplaySymbol(text, v);

            if (v.type.typeSymbol is StructSymbol s && v.type.dimensions == 0)
                WriteStructMembers(s);
            else
                text.Write(CreateLine());
        }
    }

    /// <summary>
    /// Emits the program to an assembly.
    /// </summary>
    /// <param name="buildMode">Which emitter to use.</param>
    /// <param name="moduleName">Application name.</param>
    /// <param name="references">All external references (.NET).</param>
    /// <param name="outputPath">Where to put the application once assembled.</param>
    /// <param name="wError">If warnings should be treated as errors.</param>
    /// <param name="finishStage">
    /// What stage to finish at (only applicable if <param name="buildMode" /> is set to
    /// <see cref="BuildMode.Independent" />.
    /// </param>
    /// <returns>Diagnostics.</returns>
    internal BelteDiagnosticQueue Emit(
        BuildMode buildMode, string moduleName, string[] references,
        string outputPath, bool wError, CompilerStage finishStage) {
        foreach (var syntaxTree in syntaxTrees)
            diagnostics.Move(syntaxTree.diagnostics);

        if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return diagnostics;

        var program = GetProgram();
        program.diagnostics.Move(diagnostics);

        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any() || (program.diagnostics.Any() && wError))
            return program.diagnostics;

        if (buildMode == BuildMode.Dotnet)
            return ILEmitter.Emit(program, moduleName, references, outputPath);
        else if (buildMode == BuildMode.Independent)
            return NativeEmitter.Emit(program, outputPath, finishStage);
        else // buildMode == BuildMode.CSharpTranspile
            return CSharpEmitter.Emit(program, outputPath);
    }

    /// <summary>
    /// Emits the program to a string.
    /// NOTE: Only the CSharpTranspile build mode is currently supported. Passing in any other build mode will cause the
    /// method to return null.
    /// </summary>
    /// <param name="buildMode">Which emitter to use.</param>
    /// <param name="moduleName">
    /// Name of the module. If <param name="buildMode" /> is set to <see cref="BuildMode.CSharpTranspile" /> this is
    /// used as the namespace name.
    /// </param>
    /// <param name="wError">If warnings should be treated as errors.</param>
    /// <returns>Emitted program as a string. Diagnostics must be accessed manually off of this.</returns>
    internal string EmitToString(BuildMode buildMode, string moduleName, bool wError) {
        foreach (var syntaxTree in syntaxTrees)
            diagnostics.Move(syntaxTree.diagnostics);

        if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return null;

        var program = GetProgram();
        program.diagnostics.Move(diagnostics);

        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any() || (program.diagnostics.Any() && wError))
            return null;

        if (buildMode == BuildMode.CSharpTranspile) {
            var content = CSharpEmitter.Emit(program, moduleName, out var emitterDiagnostics);
            diagnostics.Move(emitterDiagnostics);
            return content;
        }

        return null;
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
