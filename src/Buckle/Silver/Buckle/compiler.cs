using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
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

        private void InternalPreprocessor() {
            diagnostics.Push(DiagnosticType.Warning, "preprocessing not supported (yet); skipping");

            for (int i = 0; i < state.tasks.Length; i++) {
                if (state.tasks[i].stage == CompilerStage.Raw)
                    state.tasks[i].stage = CompilerStage.Preprocessed;
            }
        }

        private void InternalInterpreter() {
            diagnostics.Clear(DiagnosticType.Warning);

            var syntaxTrees = new List<SyntaxTree>();

            for (int i = 0; i < state.tasks.Length; i++) {
                ref FileState task = ref state.tasks[i];

                if (task.stage == CompilerStage.Preprocessed) {
                    var text = string.Join(Environment.NewLine, task.fileContent.lines);
                    var syntaxTree = SyntaxTree.Load(task.inputFilename, text);
                    syntaxTrees.Add(syntaxTree);
                    task.stage = CompilerStage.Compiled;
                }
            }

            var compilation = Compilation.Create(syntaxTrees.ToArray());
            diagnostics.Move(compilation.diagnostics);

            if (diagnostics.Any())
                return;

            var result = compilation.Evaluate(new Dictionary<VariableSymbol, object>());
            diagnostics.Move(result.diagnostics);
        }

        private void InternalCompiler() {
            var syntaxTrees = new List<SyntaxTree>();

            for (int i = 0; i < state.tasks.Length; i++) {
                ref FileState task = ref state.tasks[i];

                if (task.stage == CompilerStage.Preprocessed) {
                    var text = string.Join(Environment.NewLine, task.fileContent.lines);
                    var syntaxTree = SyntaxTree.Load(task.inputFilename, text);
                    syntaxTrees.Add(syntaxTree);
                    task.stage = CompilerStage.Compiled;
                }
            }

            var compilation = Compilation.Create(syntaxTrees.ToArray());
            var result = compilation.Emit(state.moduleName, state.references, state.linkOutputFilename);
            diagnostics.Move(result);
        }

        private void InternalCompilerNet() {

        }

        /// <summary>
        /// Handles preprocessing, compiling, assembling, and linking of a set of files
        /// </summary>
        /// <returns>error</returns>
        public int Compile() {
            int err;

            InternalPreprocessor();
            err = CheckErrors();
            if (err != SUCCESS_EXIT_CODE) return err;

            if (state.finishStage == CompilerStage.Preprocessed)
                return SUCCESS_EXIT_CODE;

            if (state.buildMode == BuildMode.Interpreter) {
                InternalInterpreter();
                return CheckErrors();
            } else if (state.buildMode == BuildMode.Dotnet) {
                InternalCompilerNet();
                return CheckErrors();
            }

            diagnostics.Push(DiagnosticType.Fatal, "independent compilation not supported (yet)");
            return CheckErrors();

            // InternalCompiler();
            // err = CheckErrors();
            // if (err != SUCCESS_EXIT_CODE) return err;

            // if (state.finishStage == CompilerStage.Compiled)
            //     return SUCCESS_EXIT_CODE;

            // ExternalAssembler();
            // err = CheckErrors();
            // if (err != SUCCESS_EXIT_CODE) return err;

            // if (state.finishStage == CompilerStage.Assembled)
            //     return SUCCESS_EXIT_CODE;

            // ExternalLinker();
            // err = CheckErrors();
            // if (err != SUCCESS_EXIT_CODE) return err;

            // if (state.finishStage == CompilerStage.Linked)
            //     return SUCCESS_EXIT_CODE;

            // return FATAL_EXIT_CODE;
        }
    }
}
