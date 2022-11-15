using System.Collections.Generic;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;
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
    /// Text representation of file
    /// </summary>
    public string text;

    /// <summary>
    /// Byte representation of file (usually only used with .o or .exe files)
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
    /// Current stage of the file (see CompilerStage).
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
/// State of a single compiler.
/// </summary>
public struct CompilerState {
    /// <summary>
    /// What the compiler will target.
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
    /// Compile time options (see BuckleCommandLine)
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
/// Handles compiling and handling a single CompilerState.
/// Multiple can be created and run asynchronously.
/// </summary>
public sealed class Compiler {
    private const int SUCCESS_EXIT_CODE = 0;
    private const int ERROR_EXIT_CODE = 1;
    private const int FATAL_EXIT_CODE = 2;

    /// <summary>
    /// Creates a new compiler, state needs to be set separately.
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
    /// <returns>Error code, 0 = success</returns>
    public int Compile() {
        int err;

        InternalPreprocessor();

        err = CheckErrors();
        if (err != SUCCESS_EXIT_CODE)
            return err;

        if (state.finishStage == CompilerStage.Preprocessed)
            return SUCCESS_EXIT_CODE;

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
        // if (err != SUCCESS_EXIT_CODE)
        //     return err;

        // if (state.finishStage == CompilerStage.Compiled)
        //     return SUCCESS_EXIT_CODE;

        // ExternalAssembler();
        // err = CheckErrors();
        // if (err != SUCCESS_EXIT_CODE)
        //     return err;

        // if (state.finishStage == CompilerStage.Assembled)
        //     return SUCCESS_EXIT_CODE;

        // ExternalLinker();
        // err = CheckErrors();
        // if (err != SUCCESS_EXIT_CODE)
        //     return err;

        // if (state.finishStage == CompilerStage.Linked)
        //     return SUCCESS_EXIT_CODE;

        // return FATAL_EXIT_CODE;
    }

    private int CheckErrors() {
        foreach (Diagnostic diagnostic in diagnostics)
            if (diagnostic.info.severity == DiagnosticType.Error)
                return ERROR_EXIT_CODE;

        return SUCCESS_EXIT_CODE;
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

        diagnostics = diagnostics.FilterOut(DiagnosticType.Warning);
        if (diagnostics.Any())
            return;

        var result = compilation.Evaluate(new Dictionary<VariableSymbol, object>());
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
        var result = compilation.Emit(state.moduleName, state.references, state.outputFilename);
        diagnostics.Move(result);
    }
}
