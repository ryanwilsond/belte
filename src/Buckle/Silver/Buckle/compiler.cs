using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle {

    public sealed class Compiler {
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
                    // ...
                }
            }
        }

        private void Repl(ErrorHandle callback) {
            state.link_output_content = null;
            diagnostics.Clear();
            bool showTree = false;
            var variables = new Dictionary<VariableSymbol, object>();
            var textbuilder = new StringBuilder();
            Compilation prev = null;

            while (true) {
                Console.ForegroundColor = ConsoleColor.Green;

                if (textbuilder.Length == 0)
                    Console.Write("» ");
                else
                    Console.Write("· ");

                Console.ResetColor();

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

                var compilation = prev == null ? new Compilation(syntaxTree) : prev.ContinueWith(syntaxTree);

                state.source_text = compilation.tree.text;

                if (showTree) compilation.tree.root.WriteTo(Console.Out);
                var result = compilation.Evaluate(variables);

                diagnostics.Move(result.diagnostics);
                if (diagnostics.Any()) {
                    if (callback != null)
                        callback(this);
                } else {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine(result.value);
                    Console.ResetColor();
                    prev = compilation; // prevents chaining a statement that had errors
                }

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
