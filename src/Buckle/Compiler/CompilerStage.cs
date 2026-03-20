
namespace Buckle;

/// <summary>
/// The current step in compilation a source file is.
/// </summary>
public enum CompilerStage : byte {
    Raw,
    Compiled,
    Assembled,
    Finished,
}
