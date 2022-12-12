using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Preprocessing;

/// <summary>
/// A line of text that is a preprocessor statement.
/// </summary>
internal abstract class PreprocessLine {
    /// <summary>
    /// Line of text as seen in the source code.
    /// </summary>
    internal TextLine text;

    /// <summary>
    /// Creates a PreprocessLine
    /// </summary>
    internal PreprocessLine() { }
}
