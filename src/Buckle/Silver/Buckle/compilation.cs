using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle {

    public enum CompilerStage {
        raw,
        preprocessed,
        compiled,
        assembled,
        linked,
    }

    public struct FileContent {
        public List<string> lines;
        public List<byte> bytes;
    }

    public struct FileState {
        public string in_filename;
        public CompilerStage stage;
        public string out_filename;
        public FileContent file_content;
    }

    public struct CompilerState {
        public CompilerStage finish_stage;
        public SourceText source_text;
        public string link_output_filename;
        public List<byte> link_output_content;
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
        private BoundGlobalScope global_scope_;
        public DiagnosticQueue diagnostics;
        public SyntaxTree tree;
        public Compilation prev;

        internal BoundGlobalScope global_scope {
            get {
                if (global_scope_ == null) {
                    var globalScope = Binder.BindGlobalScope(prev?.global_scope, tree.root);
                    // makes assignment thread-safe
                    // so if multiple threads try and initialize they use whoever did it first
                    Interlocked.CompareExchange(ref global_scope_, globalScope, null);
                }

                return global_scope_;
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
            diagnostics.Move(global_scope.diagnostics);
            if (diagnostics.Any())
                return new EvaluationResult(null, diagnostics);

            EvaluationResult last_value_ = new EvaluationResult();
            DiagnosticQueue past_diagnostics = new DiagnosticQueue();
            past_diagnostics.Move(diagnostics);

            foreach(var statement in global_scope.statements) {
                Evaluator eval = new Evaluator(statement, variables);
                last_value_ = new EvaluationResult(eval.Evaluate(), past_diagnostics);
                past_diagnostics.Move(last_value_.diagnostics);
            }

            last_value_.diagnostics.Move(past_diagnostics);
            return last_value_;
        }

        public Compilation ContinueWith(SyntaxTree tree) {
            return new Compilation(this, tree);
        }
    }

    internal sealed class CompilationUnit : Node {
        public ImmutableArray<Statement> statements { get; }
        public Token eof { get; }
        public override SyntaxType type => SyntaxType.COMPILATION_UNIT;

        public CompilationUnit(ImmutableArray<Statement> statements_, Token eof_) {
            statements = statements_;
            eof = eof_;
        }
    }
}
