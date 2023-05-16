using System.IO;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// Writes to a <see cref="SourceText" />.
/// </summary>
internal abstract class SourceTextWriter : TextWriter {
    /// <summary>
    /// Converts the writer into a <see cref="SourceText" />.
    /// </summary>
    internal abstract SourceText ToSourceText();

    /// <summary>
    /// Creates a writer. Will create different writers depending on the given length.
    /// </summary>
    internal static SourceTextWriter Create(int length) {
        if (length < SourceText.LargeObjectHeapLimitInChars)
            return new StringTextWriter(length);
        else
            return new LargeTextWriter(length);
    }
}
