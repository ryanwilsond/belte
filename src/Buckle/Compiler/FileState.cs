
namespace Buckle;

/// <summary>
/// The state of a source file.
/// </summary>
public struct FileState {
    /// <summary>
    /// Original name of source file.
    /// </summary>
    public string inputFileName;

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
