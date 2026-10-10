
namespace Buckle;

/// <summary>
/// The current step in compilation a source file is.
/// </summary>
public enum CompilerStage : byte {
    /// Source file (.blt)
    Raw,

    /// Other
    Compiled,
    Assembled,
    Finished,
}
