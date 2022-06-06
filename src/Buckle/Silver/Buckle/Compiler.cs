using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle;

public enum CompilerStage {
    Raw,
    Preprocessed,
    Compiled,
    Assembled,
    Linked,
}

public struct FileContent {
    public string text;
    public List<byte> bytes;
}

public struct FileState {
    public string inputFilename;
    public CompilerStage stage;
    public string outputFilename;
    public FileContent fileContent;
}

public enum BuildMode {
    Repl,
    Interpreter,
    Independent,
    Dotnet,
}

public struct CompilerState {
    public BuildMode buildMode;
    public string moduleName;
    public string[] references;
    public string[] options;
    public string entryPoint;
    public CompilerStage finishStage;
    public string outputFilename;
    public List<byte> linkOutputContent;
    public FileState[] tasks;
}

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
            if (diagnostic.info.severity == DiagnosticType.Error)
                return ERROR_EXIT_CODE;

        return SUCCESS_EXIT_CODE;
    }

    private void ExternalAssembler() {
        diagnostics.Push(DiagnosticType.Warning, "assembling not supported (yet); skipping");
    }

    private void ExternalLinker() {
        diagnostics.Push(DiagnosticType.Warning, "linking not supported (yet); skipping");
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

    /// <summary>
    /// Handles preprocessing, compiling, assembling, and linking of a set of files
    /// </summary>
    /// <returns>error</returns>
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

        diagnostics.Push(
            DiagnosticType.Fatal, "independent compilation not supported (yet); must specify '-i', '-d', or '-r'");

        return CheckErrors();

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
}
