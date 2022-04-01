using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Syntax;

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

    public struct CompilerState {
        public CompilerStage finishStage;
        public SourceText sourceText;
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

    internal sealed class Compilation {
        private BoundGlobalScope globalScope_;
        public DiagnosticQueue diagnostics;
        public SyntaxTree tree;
        public Compilation prev;

        internal BoundGlobalScope globalScope {
            get {
                if (globalScope_ == null) {
                    var tempScope = Binder.BindGlobalScope(prev?.globalScope, tree.root);
                    // makes assignment thread-safe
                    // so if multiple threads try and initialize they use whoever did it first
                    Interlocked.CompareExchange(ref globalScope_, tempScope, null);
                }

                return globalScope_;
            }
        }

        public Compilation(SyntaxTree tree) : this(null, tree) { }

        private Compilation(Compilation prev_, SyntaxTree tree_) {
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(tree_.diagnostics);
            prev = prev_;
            tree = tree_;
        }

        public EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables) {
            diagnostics.Move(tree.diagnostics);
            diagnostics.Move(globalScope.diagnostics);
            if (diagnostics.Any())
                return new EvaluationResult(null, diagnostics);

            EvaluationResult lastValue_ = new EvaluationResult();
            DiagnosticQueue pastDiagnostics = new DiagnosticQueue();
            pastDiagnostics.Move(diagnostics);

            foreach(var statement in globalScope.statements) {
                Evaluator eval = new Evaluator(statement, variables);
                lastValue_ = new EvaluationResult(eval.Evaluate(), pastDiagnostics);
                pastDiagnostics.Move(lastValue_.diagnostics);
            }

            lastValue_.diagnostics.Move(pastDiagnostics);
            return lastValue_;
        }

        public Compilation ContinueWith(SyntaxTree tree) {
            return new Compilation(this, tree);
        }
    }

    internal sealed class CompilationUnit : Node {
        public ImmutableArray<Statement> statements { get; }
        public Token endOfFile { get; }
        public override SyntaxType type => SyntaxType.COMPILATION_UNIT;

        public CompilationUnit(ImmutableArray<Statement> statements_, Token endOfFile_) {
            statements = statements_;
            endOfFile = endOfFile_;
        }
    }
}
