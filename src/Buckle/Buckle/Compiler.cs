using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Preprocessing;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using Diagnostics;

namespace Buckle;

/// <summary>
/// Handles compiling and handling a single <see cref="CompilerState" />.
/// Multiple can be created and run asynchronously.
/// </summary>
public sealed class Compiler {
    private const int SuccessExitCode = 0;
    private const int ErrorExitCode = 1;
    private const int FatalExitCode = 2;
    // Eventually have these automatically calculated to be optimal
    private const int InterpreterMaxTextLength = 4096;
    private const int EvaluatorMaxTextLength = 4096 * 4;

    private CompilationOptions _options => new CompilationOptions(state.buildMode, false, !state.noOut);

    /// <summary>
    /// Creates a new <see cref="Compiler" />, state needs to be set separately.
    /// </summary>
    public Compiler() {
        diagnostics = new BelteDiagnosticQueue();
    }

    /// <summary>
    /// Compiler specific state that determines what to compile and how.
    /// Required to compile.
    /// </summary>
    public CompilerState state { get; set; }

    /// <summary>
    /// The name of the compiler (usually displayed with diagnostics).
    /// </summary>
    public string me { get; set; }

    /// <summary>
    /// Where the diagnostics are stored for the compiler before being displayed or logged.
    /// </summary>
    public BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// Handles preprocessing, compiling, assembling, and linking of a set of files.
    /// </summary>
    /// <returns>Error code, 0 = success.</returns>
    public int Compile() {
        int err;

        InternalPreprocessor();

        err = CheckErrors();
        if (err != SuccessExitCode)
            return err;

        if (state.finishStage == CompilerStage.Preprocessed)
            return SuccessExitCode;

        if (state.buildMode is BuildMode.AutoRun or BuildMode.Interpret or BuildMode.Evaluate or BuildMode.Execute) {
            InternalInterpreter();
            return CheckErrors();
        } else {
            InternalCompiler();
            return CheckErrors();
        }
    }

    private int CheckErrors() {
        var worst = SuccessExitCode;

        foreach (Diagnostic diagnostic in diagnostics) {
            if (diagnostic.info.severity == DiagnosticSeverity.Error)
                worst = ErrorExitCode;
        }

        return worst;
    }

    private void InternalPreprocessor() {
        var preprocessor = new Preprocessor();

        for (var i = 0; i < state.tasks.Length; i++) {
            ref var task = ref state.tasks[i];

            if (task.stage == CompilerStage.Raw) {
                var text = preprocessor.PreprocessText(task.inputFileName, task.fileContent.text);
                task.fileContent.text = text;
                task.stage = CompilerStage.Preprocessed;
            }
        }

        diagnostics.Move(preprocessor.diagnostics);
    }

    private void InternalInterpreter() {
        diagnostics.Clear(DiagnosticSeverity.Warning);
        var textLength = 0;
        var textsCount = 0;

        foreach (var task in state.tasks) {
            if (task.fileContent.text != null) {
                textLength += task.fileContent.text.Length;
                textsCount++;
            }
        }

        var buildMode = state.buildMode == BuildMode.AutoRun ? textLength switch {
            <= InterpreterMaxTextLength when textsCount == 1 => BuildMode.Interpret,
            <= EvaluatorMaxTextLength => BuildMode.Evaluate,
            _ => BuildMode.Execute
        } : state.buildMode;

        if (buildMode is BuildMode.Evaluate or BuildMode.Execute) {
            var syntaxTrees = new List<SyntaxTree>();

            for (var i = 0; i < state.tasks.Length; i++) {
                ref var task = ref state.tasks[i];

                if (task.stage == CompilerStage.Preprocessed) {
                    var syntaxTree = SyntaxTree.Load(task.inputFileName, task.fileContent.text);
                    syntaxTrees.Add(syntaxTree);
                    task.stage = CompilerStage.Finished;
                }
            }

            var compilation = Compilation.Create(_options, syntaxTrees.ToArray());
            diagnostics.Move(compilation.diagnostics);

            if ((diagnostics.Errors().Any()))
                return;

            if (state.noOut)
                return;

            EvaluationResult result = null;

            void Wrapper(object parameter) {
                if (state.buildMode == BuildMode.Evaluate) {
                    result = compilation.Evaluate(
                        new Dictionary<IVariableSymbol, IEvaluatorObject>(),
                        (ValueWrapper<bool>)parameter
                    );
                } else {
                    compilation.Execute();
                }
            }

            InternalInterpreterStart(Wrapper);

            diagnostics.Move(result?.diagnostics);
        } else {
            Debug.Assert(state.tasks.Length == 1);

            var sourceText = new StringText(state.tasks[0].inputFileName, state.tasks[0].fileContent.text);
            var syntaxTree = new SyntaxTree(sourceText);

            state.tasks[0].stage = CompilerStage.Finished;

            var options = _options;
            options.isScript = true;
            var compilation = Compilation.Create(options, syntaxTree);
            EvaluationResult result = null;

            void Wrapper(object parameter) {
                result = compilation.Interpret(
                    new Dictionary<IVariableSymbol, IEvaluatorObject>(),
                    (ValueWrapper<bool>)parameter
                );
            }

            InternalInterpreterStart(Wrapper);

            diagnostics.Move(result?.diagnostics);
        }
    }

    private void InternalCompiler() {
        var syntaxTrees = new List<SyntaxTree>();

        for (var i = 0; i < state.tasks.Length; i++) {
            ref var task = ref state.tasks[i];

            if (task.stage == CompilerStage.Preprocessed) {
                var syntaxTree = SyntaxTree.Load(task.inputFileName, task.fileContent.text);
                syntaxTrees.Add(syntaxTree);
                task.stage = CompilerStage.Compiled;
            }
        }

        var compilation = Compilation.Create(_options, syntaxTrees.ToArray());

        if (state.noOut)
            return;

        var result = compilation.Emit(
            state.buildMode, state.moduleName, state.references, state.outputFilename, state.finishStage
        );

        diagnostics.Move(result);
    }

    private void InternalInterpreterStart(Action<object> wrapper) {
        ValueWrapper<bool> abort = false;

        void ctrlCHandler(object sender, ConsoleCancelEventArgs args) {
            if (state.buildMode != BuildMode.Execute) {
                abort.Value = true;
                args.Cancel = true;
            }
        }

        Console.CancelKeyPress += new ConsoleCancelEventHandler(ctrlCHandler);

        var wrapperReference = new ParameterizedThreadStart(wrapper);
        var wrapperThread = new Thread(wrapperReference);
        wrapperThread.Start(abort);

        while (wrapperThread.IsAlive)
            ;
    }
}
