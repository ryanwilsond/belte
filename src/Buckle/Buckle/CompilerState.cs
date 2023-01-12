using System.Collections.Generic;

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
