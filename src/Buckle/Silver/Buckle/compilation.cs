using System.Collections.Generic;
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
            Evaluator eval = new Evaluator(global_scope.expr, variables);
            return new EvaluationResult(eval.Evaluate(), diagnostics);
        }

        public Compilation ContinueWith(SyntaxTree tree) {
            return new Compilation(this, tree);
        }
    }

    internal sealed class CompilationUnit : Node {
        public Expression expr { get; }
        public Token eof { get; }
        public override SyntaxType type => SyntaxType.CompilationUnit;

        public CompilationUnit(Expression expr_, Token eof_) {
            expr = expr_;
            eof = eof_;
        }
    }
}
