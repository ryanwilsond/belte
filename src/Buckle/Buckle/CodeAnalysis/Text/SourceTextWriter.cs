using System.IO;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// Writes to a <see cref="SourceText" />.
/// </summary>
internal abstract class SourceTextWriter : TextWriter {
    internal abstract SourceText ToSourceText();

    internal static SourceTextWriter Create(int length) {
        if (length < SourceText.LargeObjectHeapLimitInChars)
            return new StringTextWriter(length);
        else
            return new LargeTextWriter(length);
    }
}
