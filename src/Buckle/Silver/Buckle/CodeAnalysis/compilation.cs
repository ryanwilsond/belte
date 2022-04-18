using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;
using System.IO;
using Buckle.CodeAnalysis.Symbols;
using Buckle.IO;
using System.Linq;
using BindingFlags = System.Reflection.BindingFlags;
using Buckle.CodeAnalysis.Emitting;

namespace Buckle.CodeAnalysis {

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
        internal FunctionSymbol mainFunction => globalScope.mainFunction;
        internal ImmutableArray<FunctionSymbol> functions => globalScope.functions;
        internal ImmutableArray<VariableSymbol> variables => globalScope.variables;
        internal ImmutableArray<SyntaxTree> trees { get; }
        internal Compilation previous { get; }
        internal bool isScript { get; }

        internal BoundGlobalScope globalScope {
            get {
                if (globalScope_ == null) {
                    var tempScope = Binder.BindGlobalScope(isScript, previous?.globalScope, trees);
                    // makes assignment thread-safe, if multiple threads try to initialize they use whoever did it first
                    Interlocked.CompareExchange(ref globalScope_, tempScope, null);
                }

                return globalScope_;
            }
        }

        private Compilation(bool isScript_, Compilation previous_, params SyntaxTree[] syntaxTrees) {
            isScript = isScript_;
            previous = previous_;
            diagnostics = new DiagnosticQueue();

            foreach (var tree in syntaxTrees)
                diagnostics.Move(tree.diagnostics);

            trees = syntaxTrees.ToImmutableArray();
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

            while (submission != null) {
                var builtins = typeof(BuiltinFunctions)
                    .GetFields(BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                    .Where(fi => fi.FieldType == typeof(FunctionSymbol))
                    .Select(fi => (FunctionSymbol)fi.GetValue(null))
                    .ToList();

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
            foreach (var tree in trees)
                diagnostics.Move(tree.diagnostics);

            diagnostics.Move(globalScope.diagnostics);
            if (diagnostics.Any())
                return new EvaluationResult(null, diagnostics);

            var program = GetProgram();
            // CreateCfg(program);

            if (program.diagnostics.Any())
                return new EvaluationResult(null, program.diagnostics);

            var eval = new Evaluator(program, variables);
            var result = new EvaluationResult(eval.Evaluate(), diagnostics);
            return result;
        }

        private static void CreateCfg(BoundProgram program) {
            // TODO
            /*
            var appPath = Environment.GetCommandLineArgs()[0];
            var appDirectory = Path.GetDirectoryName(appPath);
            var cfgPath = Path.Combine(appDirectory, "cfg.dot");
            var cfgStatement = !program..statements.Any() && program.functions.Any()
            /   ? program.functions.Last().Value
                : program.statement;
            var cfg = ControlFlowGraph.Create(cfgStatement);

            using (var streamWriter = new StreamWriter(cfgPath))
                cfg.WriteTo(streamWriter);
            */
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
                if (!program.functions.TryGetValue(f, out var body))
                    return;

                body.WriteTo(writer);
            } else {
                symbol.WriteTo(writer);
            }
        }

        internal DiagnosticQueue Emit(string moduleName, string[] references, string outputPath) {
            var program = GetProgram();
            return Emitter.Emit(program, moduleName, references, outputPath);
        }
    }
}
