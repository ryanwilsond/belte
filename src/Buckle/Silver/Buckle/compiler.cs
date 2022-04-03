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

        private void InternalCompiler() {
            for (int i=0; i<state.tasks.Length; i++) {
                if (state.tasks[i].stage == CompilerStage.Preprocessed) {
                    // ...
                }
            }
        }

        /// <summary>
        /// Handles preprocessing, compiling, assembling, and linking of a set of files
        /// </summary>
        /// <returns>error</returns>
        public int Compile() {
            int err;

            Preprocess();
            err = CheckErrors();
            if (err != SUCCESS_EXIT_CODE) return err;

            if (state.finishStage == CompilerStage.Preprocessed)
                return SUCCESS_EXIT_CODE;

            InternalCompiler();
            err = CheckErrors();
            if (err != SUCCESS_EXIT_CODE) return err;

            if (state.finishStage == CompilerStage.Compiled)
                return SUCCESS_EXIT_CODE;

            ExternalAssembler();
            err = CheckErrors();
            if (err != SUCCESS_EXIT_CODE) return err;

            if (state.finishStage == CompilerStage.Assembled)
                return SUCCESS_EXIT_CODE;

            ExternalLinker();
            err = CheckErrors();
            if (err != SUCCESS_EXIT_CODE) return err;

            if (state.finishStage == CompilerStage.Linked)
                return SUCCESS_EXIT_CODE;

            return FATAL_EXIT_CODE;
        }
    }
}
