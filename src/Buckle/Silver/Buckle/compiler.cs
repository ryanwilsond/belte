using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Text;

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

        public Compilation(SyntaxTree tree_) {
            diagnostics = new DiagnosticQueue();
            diagnostics.Move(tree_.diagnostics);
            tree = tree_;
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

        public delegate int ErrorHandle(Compiler compiler);

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
            var textbuilder = new StringBuilder();

            while (true) {
                if (textbuilder.Length == 0)
                    Console.Write("> ");
                else
                    Console.Write(". ");

                string line = Console.ReadLine();
                bool isblank = string.IsNullOrWhiteSpace(line);

                if (textbuilder.Length == 0) {
                    if (isblank) {
                        break;
                    } else if (line == "#showTree") {
                        showTree = !showTree;
                        Console.WriteLine(showTree ? "Parse-trees visible" : "Parse-trees hidden");
                        continue;
                    } else if (line == "#clear" || line == "#cls") {
                        Console.Clear();
                        continue;
                    }
                }

                textbuilder.AppendLine(line);
                string text = textbuilder.ToString();

                var syntaxTree = SyntaxTree.Parse(text);

                if (!isblank && syntaxTree.diagnostics.Any()) continue;

                var compilation = new Compilation(syntaxTree);
                state.source_text = compilation.tree.text;

                if (showTree) PrintTree(compilation.tree.root);

                var result = compilation.Evaluate(variables);

                diagnostics.Move(result.diagnostics);
                if (diagnostics.Any()) {
                    if (callback != null)
                        callback(this);
                } else Console.WriteLine(result.value);

                textbuilder.Clear();
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
