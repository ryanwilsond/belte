
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
    /// Compile time options.
    /// </summary>
    public string[] options;

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
}
