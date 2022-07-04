using System;
using System.IO;
using System.Linq;
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

internal sealed class EvaluationResult {
    public BelteDiagnosticQueue diagnostics;
    public object value;

    internal EvaluationResult(object value_, BelteDiagnosticQueue diagnostics_) {
        value = value_;
        diagnostics = new BelteDiagnosticQueue();
        diagnostics.Move(diagnostics_);
    }

    internal EvaluationResult() : this(null, null) { }
}

public sealed class Compilation {
    private BoundGlobalScope globalScope_;
    public BelteDiagnosticQueue diagnostics;
    internal FunctionSymbol mainFunction => globalScope.mainFunction;
    internal ImmutableArray<FunctionSymbol> functions => globalScope.functions;
    internal ImmutableArray<VariableSymbol> variables => globalScope.variables;
    internal ImmutableArray<SyntaxTree> syntaxTrees { get; }
    internal Compilation previous { get; }
    internal bool isScript { get; }

    internal BoundGlobalScope globalScope {
        get {
            if (globalScope_ == null) {
                var tempScope = Binder.BindGlobalScope(isScript, previous?.globalScope, syntaxTrees);
                // makes assignment thread-safe, if multiple threads try to initialize they use whoever did it first
                Interlocked.CompareExchange(ref globalScope_, tempScope, null);
            }

            return globalScope_;
        }
    }

    private Compilation(bool isScript_, Compilation previous_, params SyntaxTree[] syntaxTrees) {
        isScript = isScript_;
        previous = previous_;
        diagnostics = new BelteDiagnosticQueue();

        foreach (var syntaxTree in syntaxTrees)
            diagnostics.Move(syntaxTree.diagnostics);

        this.syntaxTrees = syntaxTrees.ToImmutableArray<SyntaxTree>();
    }

    internal static Compilation Create(params SyntaxTree[] syntaxTrees) {
        return new Compilation(false, null, syntaxTrees);
    }

    internal static Compilation CreateScript(Compilation previous, params SyntaxTree[] syntaxTrees) {
        return new Compilation(true, previous, syntaxTrees);
    }

    internal IEnumerable<Symbol> GetSymbols() {
        var submission = this;
        var seenSymbolNames = new HashSet<string>();
        var builtins = BuiltinFunctions.GetAll();

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

    private BoundProgram GetProgram() {
        var previous_ = previous == null ? null : previous.GetProgram();
        return Binder.BindProgram(isScript, previous_, globalScope);
    }

    internal EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables) {
        if (globalScope.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return new EvaluationResult(null, globalScope.diagnostics);

        var program = GetProgram();
        // CreateCfg(program);

        if (program.diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return new EvaluationResult(null, program.diagnostics);

        diagnostics.Move(program.diagnostics);
        var eval = new Evaluator(program, variables);
        var evalResult = eval.Evaluate();

        // TODO: hack to prevent repl overwriting text when user doesn't add newline
        if (eval.hasPrint)
            Console.WriteLine();

        diagnostics.Move(eval.diagnostics);
        var result = new EvaluationResult(evalResult, diagnostics);
        return result;
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

    internal void EmitTree(TextWriter writer) {
        if (globalScope.mainFunction != null)
            EmitTree(globalScope.mainFunction, writer);
        else if (globalScope.scriptFunction != null)
            EmitTree(globalScope.scriptFunction, writer);
    }

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

    internal BelteDiagnosticQueue Emit(string moduleName, string[] references, string outputPath) {
        foreach (var syntaxTree in syntaxTrees)
            diagnostics.Move(syntaxTree.diagnostics);

        if (diagnostics.FilterOut(DiagnosticType.Warning).Any())
            return diagnostics;

        var program = GetProgram();
        program.diagnostics.Move(diagnostics);
        return Emitter.Emit(program, moduleName, references, outputPath);
    }
}
