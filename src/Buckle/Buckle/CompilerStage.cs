
namespace Buckle;

/// <summary>
/// The current step in compilation a source file is.
/// </summary>
public enum CompilerStage {
    Raw,
    Preprocessed,
    Compiled,
    Assembled,
    Finished,
}
