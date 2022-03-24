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
        public List<FileState> tasks;
    }

    public enum DiagnosticType {
        error,
        warning,
        fatal,
        unknown,
    }

    public class Diagnostic {
        public DiagnosticType type { get; }
        public string msg { get; }

        public Diagnostic(DiagnosticType type_, string msg_) {
            type = type_;
            msg = msg_;
        }
    }

    public class Compiler {
        const int SUCCESS_EXIT_CODE = 0;
        const int ERROR_EXIT_CODE = 1;
        const int FATAL_EXIT_CODE = 2;

        public CompilerState state;
        public string me;
        public List<Diagnostic> diagnostics;

        public Compiler() {
            diagnostics = new List<Diagnostic>();
        }

        private int CheckErrors() {
            foreach (Diagnostic diagnostic in diagnostics) {
                if (diagnostic.type == DiagnosticType.error) return ERROR_EXIT_CODE;
            }
            return SUCCESS_EXIT_CODE;
        }

        private void ExternalAssembler() {
            diagnostics.Add(new Diagnostic(DiagnosticType.warning, "assembling not supported (yet); skipping"));
        }

        private void ExternalLinker() {
            diagnostics.Add(new Diagnostic(DiagnosticType.warning, "linking not supported (yet); skipping"));
        }

        private void Preprocess() {
            diagnostics.Add(new Diagnostic(DiagnosticType.warning, "preprocessing not supported (yet); skipping"));
        }

        private void PrettyPrint(Node node, string indent = "", bool islast=true) {
            string marker = islast ? "└─" : "├─";
            Console.Write($"{indent}{marker}{node.type}");

            if (node is Token t && t.value != null)
                Console.Write($" {t.value}");

            Console.WriteLine();
            indent += islast ? "  " : "│ ";
            var lastChild = node.GetChildren().LastOrDefault();

            foreach (var child in node.GetChildren())
                PrettyPrint(child, indent, child == lastChild);
        }

        public delegate int ErrorHandle(Compiler compiler);

        private void Repl(ErrorHandle callback) {
            state.link_output_content = null;
            diagnostics.Clear();
            bool showTree = false;

            while (true) {
                Console.Write("> ");
                string line = Console.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) return;

                // repl specific zulu statements
                if (line == "#showTree") {
                    showTree = !showTree;
                    Console.WriteLine(showTree ? "Parse-trees visible" : "Parse-trees hidden");
                    continue;
                } else if (line == "#clear" || line == "#cls") {
                    Console.Clear();
                    continue;
                }

                SyntaxTree tree = SyntaxTree.Parse(line);
                Binder binder = new Binder();
                var boundexpr = binder.BindExpression(tree.root);
                diagnostics.AddRange(tree.diagnostics);
                diagnostics.AddRange(binder.diagnostics);

                if (showTree) {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    PrettyPrint(tree.root);
                    Console.ResetColor();
                }

                if (diagnostics.Any()) {
                    if (callback != null)
                        callback(this);
                    diagnostics.Clear();
                    continue;
                }

                Evaluator eval = new Evaluator(boundexpr);
                var result = eval.Evaluate();

                diagnostics.AddRange(eval.diagnostics);
                if (diagnostics.Any()) {
                    if (callback != null)
                        callback(this);
                    diagnostics.Clear();
                    continue;
                }

                Console.WriteLine(result);
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
