using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using System.IO;
using Buckle.CodeAnalysis.Symbols;
using Buckle.IO;
using System.Linq;
using System;

namespace Buckle {

    public enum CompilerStage {
        Raw,
        Preprocessed,
        Compiled,
        Assembled,
        Linked,
    }

    public struct FileContent {
        public List<string> lines;
        public List<byte> bytes;
    }

    public struct FileState {
        public string inputFilename;
        public CompilerStage stage;
        public string outputFilename;
        public FileContent fileContent;
    }

    public enum BuildMode {
        Repl,
        Interpreter,
        Independent,
        Dotnet,
    }

    public struct CompilerState {
        public BuildMode buildMode;
        public CompilerStage finishStage;
        public string linkOutputFilename;
        public List<byte> linkOutputContent;
        public FileState[] tasks;
    }

    internal sealed class EvaluationResult {
        public DiagnosticQueue diagnostics;
        public object value;

        internal EvaluationResult(object value_, DiagnosticQueue diagnostics_) {
            value = value_;
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(diagnostics_);
        }

        internal EvaluationResult() : this(null, null) { }
    }

    public sealed class Compilation {
        private BoundGlobalScope globalScope_;
        public DiagnosticQueue diagnostics;
        internal ImmutableArray<SyntaxTree> trees;
        internal ImmutableArray<FunctionSymbol> functions => globalScope.functions;
        internal ImmutableArray<VariableSymbol> variables => globalScope.variables;
        public Compilation previous;

        internal BoundGlobalScope globalScope {
            get {
                if (globalScope_ == null) {
                    var tempScope = Binder.BindGlobalScope(previous?.globalScope, trees);
                    // makes assignment thread-safe, if multiple threads try to initialize they use whoever did it first
                    Interlocked.CompareExchange(ref globalScope_, tempScope, null);
                }

                return globalScope_;
            }
        }

        internal Compilation(params SyntaxTree[] trees) : this(null, trees) { }

        private Compilation(Compilation previous_, params SyntaxTree[] trees_) {
            diagnostics = new DiagnosticQueue();
            foreach (var tree in trees_)
                diagnostics.Move(tree.diagnostics);
            previous = previous_;
            trees = trees_.ToImmutableArray();
        }

        internal IEnumerable<Symbol> GetSymbols() {
            var submission = this;
            var seenSymbolNames = new HashSet<string>();

            while (submission != null) {
                foreach (var function in submission.functions)
                    if (seenSymbolNames.Add(function.name))
                        yield return function;

                foreach (var variable in submission.variables)
                    if (seenSymbolNames.Add(variable.name))
                        yield return variable;

                submission = submission.previous;
            }
        }

        internal EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables) {
            foreach (var tree in trees)
                diagnostics.Move(tree.diagnostics);

            diagnostics.Move(globalScope.diagnostics);
            if (diagnostics.Any())
                return new EvaluationResult(null, diagnostics);

            var program = Binder.BindProgram(globalScope);

            var appPath = Environment.GetCommandLineArgs()[0];
            var appDirectory = Path.GetDirectoryName(appPath);
            var cfgPath = Path.Combine(appDirectory, "cfg.dot");
            var cfgStatement = !program.statement.statements.Any() && program.functions.Any()
                ? program.functions.Last().Value
                : program.statement;
            var cfg = ControlFlowGraph.Create(cfgStatement);

            using (var streamWriter = new StreamWriter(cfgPath))
                cfg.WriteTo(streamWriter);

            if (program.diagnostics.Any())
                return new EvaluationResult(null, program.diagnostics);

            var eval = new Evaluator(program, variables);
            var result = new EvaluationResult(eval.Evaluate(), diagnostics);
            return result;
        }

        internal Compilation ContinueWith(params SyntaxTree[] trees) {
            return new Compilation(this, trees);
        }

        internal void EmitTree(TextWriter writer) {
            var program = Binder.BindProgram(globalScope);

            if (program.statement.statements.Any()) {
                program.statement.WriteTo(writer);
            } else {
                foreach (var functionBody in program.functions) {
                    if (!globalScope.functions.Contains(functionBody.Key))
                        continue;

                    functionBody.Key.WriteTo(writer);
                    functionBody.Value.WriteTo(writer);
                }
            }
        }

        internal void EmitTree(Symbol symbol, TextWriter writer) {
            var program = Binder.BindProgram(globalScope);

            if (symbol is FunctionSymbol f) {
                if (!program.functions.TryGetValue(f, out var body))
                    return;

                f.WriteTo(writer);
                body.WriteTo(writer);
            } else {
                symbol.WriteTo(writer);
            }
        }
    }
}
