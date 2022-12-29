using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Preprocessing;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Diagnostics;

namespace Buckle;

/// <summary>
/// The current step in compilation a source file is.
/// </summary>
public enum CompilerStage {
    Raw,
    Preprocessed,
    Compiled,
    Assembled,
    Linked,
}

/// <summary>
/// Contents of a file either represented as text or bytes.
/// </summary>
public struct FileContent {
    /// <summary>
    /// Text representation of file.
    /// </summary>
    public string text;

    /// <summary>
    /// Byte representation of file (usually only used with .o or .exe files).
    /// </summary>
    public List<byte> bytes;
}

/// <summary>
/// The state of a source file.
/// </summary>
public struct FileState {
    /// <summary>
    /// Original name of source file.
    /// </summary>
    public string inputFilename;

    /// <summary>
    /// Current stage of the file (see <see cref="CompilerStage" />).
    /// Not related to the stage of the compiler as a whole.
    /// </summary>
    public CompilerStage stage;

    /// <summary>
    /// Name of the file that the new contents will be put into (if applicable).
    /// </summary>
    public string outputFilename;

    /// <summary>
    /// The content of the file (not just of the original file).
    /// </summary>
    public FileContent fileContent;
}

/// <summary>
/// A type of compilation that will be performed, only one per compilation.
/// </summary>
public enum BuildMode {
    Repl,
    Interpreter,
    Independent,
    Dotnet,
}

/// <summary>
/// State of a single <see cref="Compiler" />.
/// </summary>
public struct CompilerState {
    /// <summary>
    /// What the <see cref="Compiler" /> will target.
    /// </summary>
    public BuildMode buildMode;

    /// <summary>
    /// The name of the final executable/application (if applicable).
    /// </summary>
    public string moduleName;

    /// <summary>
    /// External references (usually .NET) the compilation uses.
    /// </summary>
    public string[] references;

    /// <summary>
    /// Compile time options (see <see cref="BuckleCommandLine" />).
    /// </summary>
    public string[] options;

    /// <summary>
    /// Where the application will start.
    /// </summary>
    public string entryPoint;

    /// <summary>
    /// At what point to stop compilation (usually unrestricted).
    /// </summary>
    public CompilerStage finishStage;

    /// <summary>
    /// The name of the final executable/application.
    /// </summary>
    public string outputFilename;

    /// <summary>
    /// Final file content if stopped after link stage.
    /// </summary>
    public List<byte> linkOutputContent;

    /// <summary>
    /// All files to be managed/modified during compilation.
    /// </summary>
    public FileState[] tasks;
}

/// <summary>
/// Handles compiling and handling a single <see cref="CompilerState" />.
/// Multiple can be created and run asynchronously.
/// </summary>
public sealed class Compiler {
    private const int SuccessExitCode = 0;
    private const int ErrorExitCode = 1;
    private const int FatalExitCode = 2;

    /// <summary>
    /// Creates a new <see cref="Compiler" />, state needs to be set separately.
    /// </summary>
    public Compiler(DiagnosticHandle handle) {
        diagnostics = new BelteDiagnosticQueue();
        this.diagnosticHandle = handle;
    }

    /// <summary>
    /// Callback to handle Diagnostics, be it logging or displaying to the console.
    /// </summary>
    /// <param name="compiler"><see cref="Compiler" /> object representing entirety of compilation.</param>
    /// <param name="me">Display name of the program.</param>
    /// <returns>C-Style error code of most severe <see cref="Diagnostic" />.</returns>
    public delegate int DiagnosticHandle(Compiler compiler);

    /// <summary>
    /// Compiler specific state that determines what to compile and how.
    /// Required to compile.
    /// </summary>
    public CompilerState state { get; set; }

    /// <summary>
    /// Callback to handle Diagnostics, be it logging or displaying to the console.
    /// </summary>
    /// <param name="compiler"><see cref="Compiler" /> object representing entirety of compilation.</param>
    /// <returns>C-Style error code of most severe <see cref="Diagnostic" />.</returns>
    public DiagnosticHandle diagnosticHandle { get; set; }

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

        if (state.buildMode == BuildMode.Interpreter) {
            InternalInterpreter();
            return CheckErrors();
        } else if (state.buildMode == BuildMode.Dotnet) {
            InternalCompilerNet();
            return CheckErrors();
        }

        diagnostics.Push(Error.Unsupported.IndependentCompilation());

        return CheckErrors();

        // * This code is only relevant when independent compilation becomes supported
        // InternalCompiler();
        // err = CheckErrors();
        // if (err != SuccessExitCode)
        //     return err;

        // if (state.finishStage == CompilerStage.Compiled)
        //     return SuccessExitCode;

        // ExternalAssembler();
        // err = CheckErrors();
        // if (err != SuccessExitCode)
        //     return err;

        // if (state.finishStage == CompilerStage.Assembled)
        //     return SuccessExitCode;

        // ExternalLinker();
        // err = CheckErrors();
        // if (err != SuccessExitCode)
        //     return err;

        // if (state.finishStage == CompilerStage.Linked)
        //     return SuccessExitCode;

        // return FatalExitCode;
    }

    private int CheckErrors() {
        var worst = SuccessExitCode;

        foreach (Diagnostic diagnostic in diagnostics)
            if (diagnostic.info.severity == DiagnosticType.Error)
                worst = ErrorExitCode;

        diagnosticHandle(this);

        return worst;
    }

    private void ExternalAssembler() {
        diagnostics.Push(Warning.Unsupported.Assembling());
    }

    private void ExternalLinker() {
        diagnostics.Push(Warning.Unsupported.Linking());
    }

    private void InternalPreprocessor() {
        var preprocessor = new Preprocessor();

        for (int i=0; i<state.tasks.Length; i++) {
            ref FileState task = ref state.tasks[i];

            if (task.stage == CompilerStage.Raw)
                task.stage = CompilerStage.Preprocessed;

            var text = preprocessor.PreprocessText(task.inputFilename, task.fileContent.text);
            task.fileContent.text = text;
        }

        diagnostics.Move(preprocessor.diagnostics);
    }

    private void InternalInterpreter() {
        diagnostics.Clear(DiagnosticType.Warning);
        var syntaxTrees = new List<SyntaxTree>();

        for (int i=0; i<state.tasks.Length; i++) {
            ref FileState task = ref state.tasks[i];

            if (task.stage == CompilerStage.Preprocessed) {
                var syntaxTree = SyntaxTree.Load(task.inputFilename, task.fileContent.text);
                syntaxTrees.Add(syntaxTree);
                task.stage = CompilerStage.Compiled;
            }
        }

        var compilation = Compilation.Create(syntaxTrees.ToArray());
        diagnostics.Move(compilation.diagnostics);

        if (!state.options.Contains("all") && !state.options.Contains("error"))
            diagnostics = diagnostics.FilterOut(DiagnosticType.Warning);

        if ((diagnostics.FilterOut(DiagnosticType.Warning).Any()) ||
            (diagnostics.Any() && state.options.Contains("error")))
            return;

        var _ = false; // Unused, just to satisfy ref parameter
        var result = compilation.Evaluate(
            new Dictionary<VariableSymbol, EvaluatorObject>(), ref _, state.options.Contains("error")
        );

        if (!state.options.Contains("all") && !state.options.Contains("error"))
            diagnostics.Move(result.diagnostics.FilterOut(DiagnosticType.Warning));
        else
            diagnostics.Move(result.diagnostics);
    }

    private void InternalCompiler() { }

    private void InternalCompilerNet() {
        var syntaxTrees = new List<SyntaxTree>();

        for (int i=0; i<state.tasks.Length; i++) {
            ref FileState task = ref state.tasks[i];

            if (task.stage == CompilerStage.Preprocessed) {
                var syntaxTree = SyntaxTree.Load(task.inputFilename, task.fileContent.text);
                syntaxTrees.Add(syntaxTree);
                task.stage = CompilerStage.Compiled;
            }
        }

        var compilation = Compilation.Create(syntaxTrees.ToArray());
        var result = compilation.Emit(
            state.moduleName, state.references, state.outputFilename, state.options.Contains("error")
        );

        diagnostics.Move(result);
    }
}
