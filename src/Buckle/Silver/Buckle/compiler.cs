using System;
using System.Collections.Generic;
using System.Text;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle {

    /// <summary>
    /// Handles compiling and handling a single CompilerState
    /// </summary>
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
            foreach (Diagnostic diagnostic in diagnostics)
                if (diagnostic.type == DiagnosticType.Error) return ERROR_EXIT_CODE;

            return SUCCESS_EXIT_CODE;
        }

        private void ExternalAssembler() {
            diagnostics.Push(DiagnosticType.Warning, "assembling not supported (yet); skipping");
        }

        private void ExternalLinker() {
            diagnostics.Push(DiagnosticType.Warning, "linking not supported (yet); skipping");
        }

        private void Preprocess() {
            diagnostics.Push(DiagnosticType.Warning, "preprocessing not supported (yet); skipping");
        }

        private void PrintTree(Node root) {
            Console.ForegroundColor = ConsoleColor.DarkGray;
            root.WriteTo(Console.Out);
            Console.ResetColor();
        }

        public delegate int ErrorHandle(Compiler compiler);

        private void InternalCompiler() {
            for (int i=0; i<state.tasks.Length; i++) {
                if (state.tasks[i].stage == CompilerStage.Preprocessed) {
                    // ...
                }
            }
        }

        private void Repl(ErrorHandle callback) {
            state.linkOutputContent = null;
            diagnostics.Clear();
            bool showTree = false;
            bool showProgramTree = false;
            var variables = new Dictionary<VariableSymbol, object>();
            var textBuilder = new StringBuilder();
            Compilation previousCompilation = null;

            while (true) {
                Console.ForegroundColor = ConsoleColor.Green;

                if (textBuilder.Length == 0)
                    Console.Write("» ");
                else
                    Console.Write("· ");

                Console.ResetColor();

                string line = Console.ReadLine();
                bool isBlank = string.IsNullOrWhiteSpace(line);

                if (textBuilder.Length == 0) {
                    if (isBlank) {
                        break;
                    } else if (line == "#showTree") {
                        showTree = !showTree;
                        Console.WriteLine(showTree ? "Parse-trees visible" : "Parse-trees hidden");
                        continue;
                    } else if (line == "#showProgramTree") {
                        showProgramTree = !showProgramTree;
                        Console.WriteLine(showProgramTree ? "Bound-trees visible" : "Bound-trees hidden");
                        continue;
                    } else if (line == "#clear" || line == "#cls") {
                        Console.Clear();
                        continue;
                    } else if (line == "#reset") {
                        previousCompilation = null;
                        continue;
                    }
                }

                textBuilder.AppendLine(line);
                string text = textBuilder.ToString();
                var syntaxTree = SyntaxTree.Parse(text);
                if (!isBlank && syntaxTree.diagnostics.Any()) continue;

                var compilation = previousCompilation == null
                    ? new Compilation(syntaxTree)
                    : previousCompilation.ContinueWith(syntaxTree);

                state.sourceText = compilation.tree.text;

                if (showTree) syntaxTree.root.WriteTo(Console.Out);
                if (showProgramTree) compilation.EmitTree(Console.Out);

                var result = compilation.Evaluate(variables);

                diagnostics.Move(result.diagnostics);
                if (diagnostics.Any()) {
                    if (callback != null)
                        callback(this);
                } else {
                    Console.ForegroundColor = ConsoleColor.Magenta;
                    Console.WriteLine(result.value);
                    Console.ResetColor();
                    previousCompilation = compilation; // prevents chaining a statement that had errors
                }

                textBuilder.Clear();
            }
        }

        /// <summary>
        /// Handles preprocessing, compiling, assembling, and linking of a set of files
        /// </summary>
        /// <param name="callback">temp</param>
        /// <returns>error</returns>
        public int Compile(ErrorHandle callback=null) {
            int err;

            Preprocess();
            err = CheckErrors();
            if (err > 0) return err;

            if (state.finishStage == CompilerStage.Preprocessed)
                return SUCCESS_EXIT_CODE;

            // InternalCompiler();
            Repl(callback);
            err = CheckErrors();
            // if (err > 0) return err;
            return err;

            /*
            if (state.finishStage == CompilerStage.Compiled)
                return SUCCESS_EXIT_CODE;

            ExternalAssembler();
            err = CheckErrors();
            if (err > 0) return err;

            if (state.finishStage == CompilerStage.Assembled)
                return SUCCESS_EXIT_CODE;

            ExternalLinker();
            err = CheckErrors();
            if (err > 0) return err;

            if (state.finishStage == CompilerStage.Linked)
                return SUCCESS_EXIT_CODE;

            return FATAL_EXIT_CODE;
            // */
        }
    }
}
