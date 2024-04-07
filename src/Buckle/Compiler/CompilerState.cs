using Diagnostics;

namespace Buckle;

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
    /// Lowest severity to report.
    /// </summary>
    public DiagnosticSeverity severity;

    /// <summary>
    /// At what point to stop compilation (usually unrestricted).
    /// </summary>
    public CompilerStage finishStage;

    /// <summary>
    /// The name of the final executable/application.
    /// </summary>
    public string outputFilename;

    /// <summary>
    /// All files to be managed/modified during compilation.
    /// </summary>
    public FileState[] tasks;

    /// <summary>
    /// Enable to disable any possible output, used for debugging.
    /// </summary>
    public bool noOut;

    /// <summary>
    /// Provided arguments for the program, such as command-line arguments, that are given to the program.
    /// </summary>
    public string[] arguments;
}
