using Diagnostics;

namespace Buckle;

/// <summary>
/// State of a single <see cref="Compiler" />.
/// </summary>
public class CompilerState {
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
    /// Enable to emit a .NET PDB file.
    /// </summary>
    public bool debugMode;

    /// <summary>
    /// Lowest severity to report.
    /// </summary>
    public DiagnosticSeverity severity;

    /// <summary>
    /// Highest warning level to report.
    /// </summary>
    public int warningLevel;

    /// <summary>
    /// Warnings to not suppress.
    /// </summary>
    public DiagnosticInfo[] includeWarnings;

    /// <summary>
    /// Warnings to suppress.
    /// </summary>
    public DiagnosticInfo[] excludeWarnings;

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

    /// <summary>
    /// The type of Belte project.
    /// </summary>
    public OutputKind projectType;

    /// <summary>
    /// Whether or not the compilation is in "verbose" mode, meaning it will log additional information about the
    /// compilation process.
    /// </summary>
    public bool verboseMode;

    /// <summary>
    /// Same as "verbose" mode without creating file artifacts.
    /// </summary>
    public bool reducedVerboseMode;

    /// <summary>
    /// The path to dump verbose output files.
    /// </summary>
    public string verbosePath;

    /// <summary>
    /// Whether or not the compiler will log timing data about each stage of compilation.
    /// </summary>
    public bool time;

    /// <summary>
    /// Whether or not the compiler will use multiple CPU cores.
    /// </summary>
    public bool concurrentBuild;

    /// <summary>
    /// Maximum number of CPU cores to use for concurrent builds.
    /// </summary>
    public int maxCores;
}
