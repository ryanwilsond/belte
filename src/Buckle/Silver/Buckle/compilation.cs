using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Text;
using Buckle.CodeAnalysis.Syntax;
using System.IO;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Buckle.IO;
using System.Linq;

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
        public bool useRepl;
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

    public sealed class Compilation {
        private BoundGlobalScope globalScope_;
        public DiagnosticQueue diagnostics;
        internal SyntaxTree tree;
        public Compilation previous;

        internal BoundGlobalScope globalScope {
            get {
                if (globalScope_ == null) {
                    var tempScope = Binder.BindGlobalScope(previous?.globalScope, tree.root);
                    // makes assignment thread-safe
                    // so if multiple threads try and initialize they use whoever did it first
                    Interlocked.CompareExchange(ref globalScope_, tempScope, null);
                }

                return globalScope_;
            }
        }

        internal Compilation(SyntaxTree tree) : this(null, tree) { }

        private Compilation(Compilation previous_, SyntaxTree tree_) {
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(tree_.diagnostics);
            previous = previous_;
            tree = tree_;
        }

        internal EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables) {
            diagnostics.Move(tree.diagnostics);
            diagnostics.Move(globalScope.diagnostics);
            if (diagnostics.Any())
                return new EvaluationResult(null, diagnostics);

            var program = Binder.BindProgram(globalScope);
            if (program.diagnostics.Any())
                return new EvaluationResult(null, program.diagnostics);

            var eval = new Evaluator(program, variables);
            var result = new EvaluationResult(eval.Evaluate(), diagnostics);
            return result;
        }

        internal Compilation ContinueWith(SyntaxTree tree) {
            return new Compilation(this, tree);
        }

        public void EmitTree(TextWriter writer) {
            var program = Binder.BindProgram(globalScope);

            if (program.statement.statements.Any()) {
                program.statement.WriteTo(writer);
            } else {
                foreach (var functionBody in program.functionBodies) {
                    if (!globalScope.functions.Contains(functionBody.Key))
                        continue;

                    functionBody.Key.WriteTo(writer);
                    functionBody.Value.WriteTo(writer);
                }
            }
        }
    }

    internal sealed class CompilationUnit : Node {
        public ImmutableArray<Member> members { get; }
        public Token endOfFile { get; }
        public override SyntaxType type => SyntaxType.COMPILATION_UNIT;

        public CompilationUnit(ImmutableArray<Member> members_, Token endOfFile_) {
            members = members_;
            endOfFile = endOfFile_;
        }
    }
}
