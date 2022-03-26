using System;
using System.Linq;
using System.Collections.Generic;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Binding;

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
        public string link_output_filename;
        public List<byte> link_output_content;
        public FileState[] tasks;
    }

    internal class EvaluationResult {
        public DiagnosticQueue diagnostics;
        public object value;

        internal EvaluationResult(object value_, DiagnosticQueue diagnostics_) {
            value = value_;
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(diagnostics_);
        }
    }

    internal class Compilation {
        public DiagnosticQueue diagnostics;
        public SyntaxTree tree;
        private BoundExpression expr_;

        public Compilation(string text) {
            diagnostics = new DiagnosticQueue();
            tree = SyntaxTree.Parse(text);
        }

        public Compilation(string[] text) : this(string.Join('\n', text)) { }

        public EvaluationResult Evaluate(Dictionary<VariableSymbol, object> variables) {
            Binder binder = new Binder(variables);
            expr_ = binder.BindExpression(tree.root);
            diagnostics.Move(tree.diagnostics);
            diagnostics.Move(binder.diagnostics);
            Evaluator eval = new Evaluator(expr_, variables);
            return new EvaluationResult(eval.Evaluate(), diagnostics);
        }

        public string[] Compile() {
            List<string> lines = new List<string>();
            return lines.ToArray();
        }
    }

    public class Compiler {
        const int SUCCESS_EXIT_CODE = 0;
        const int ERROR_EXIT_CODE = 1;
        const int FATAL_EXIT_CODE = 2;

        public CompilerState state;
        public string me;
        public DiagnosticQueue diagnostics;

        public Compiler() {
            diagnostics = new DiagnosticQueue();
        }

        private int CheckErrors() {
            foreach (Diagnostic diagnostic in diagnostics) {
                if (diagnostic.type == DiagnosticType.error) return ERROR_EXIT_CODE;
            }
            return SUCCESS_EXIT_CODE;
        }

        private void ExternalAssembler() {
            diagnostics.Push(DiagnosticType.warning, "assembling not supported (yet); skipping");
        }

        private void ExternalLinker() {
            diagnostics.Push(DiagnosticType.warning, "linking not supported (yet); skipping");
        }

        private void Preprocess() {
            diagnostics.Push(DiagnosticType.warning, "preprocessing not supported (yet); skipping");
        }

        private void PrintTree(Node root) {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            root.WriteTo(Console.Out);
            Console.ResetColor();
        }

        public delegate int ErrorHandle(Compiler compiler, string line=null);

        private void InternalCompiler() {
            for (int i=0; i<state.tasks.Length; i++) {
                if (state.tasks[i].stage == CompilerStage.preprocessed) {
                    Compilation compilation = new Compilation(state.tasks[i].file_content.lines.ToArray());
                    state.tasks[i].file_content.lines = compilation.Compile().ToList();
                    state.tasks[i].stage = CompilerStage.compiled;
                    diagnostics.Move(compilation.diagnostics);
                }
            }
        }

        private void Repl(ErrorHandle callback) {
            state.link_output_content = null;
            diagnostics.Clear();
            bool showTree = false;
            var variables = new Dictionary<VariableSymbol, object>();

            while (true) {
                Console.Write("> ");
                string line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) return;
                if (line.Length - line.Replace("\t", "").Length != 0) {
                    diagnostics.Push(new Diagnostic(DiagnosticType.warning, null,
                        "using tabs is unsupported, and may produce unexpected results"));
                }

                // repl specific zulu statements
                if (line == "#showTree") {
                    showTree = !showTree;
                    Console.WriteLine(showTree ? "Parse-trees visible" : "Parse-trees hidden");
                    continue;
                } else if (line == "#clear" || line == "#cls") {
                    Console.Clear();
                    continue;
                }

                var compilation = new Compilation(line);
                diagnostics.Move(compilation.diagnostics);

                if (showTree) PrintTree(compilation.tree.root);

                if (diagnostics.Any()) {
                    if (callback != null)
                        callback(this, line);
                    continue;
                }

                var result = compilation.Evaluate(variables);

                diagnostics.Move(result.diagnostics);
                if (diagnostics.Any()) {
                    if (callback != null)
                        callback(this, line);
                } else Console.WriteLine(result.value);
            }
        }

        public int Compile(ErrorHandle callback=null) {
            int err;

            Preprocess();
            err = CheckErrors();
            if (err > 0) return err;

            if (state.finish_stage == CompilerStage.preprocessed)
                return SUCCESS_EXIT_CODE;

            // InternalCompiler();
            Repl(callback);
            err = CheckErrors();
            // if (err > 0) return err;
            return err;

            /*
            if (state.finish_stage == CompilerStage.compiled)
                return SUCCESS_EXIT_CODE;

            ExternalAssembler();
            err = CheckErrors();
            if (err > 0) return err;

            if (state.finish_stage == CompilerStage.assembled)
                return SUCCESS_EXIT_CODE;

            ExternalLinker();
            err = CheckErrors();
            if (err > 0) return err;

            if (state.finish_stage == CompilerStage.linked)
                return SUCCESS_EXIT_CODE;

            return FATAL_EXIT_CODE;
            // */
        }
    }
}
