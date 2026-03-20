using System.IO;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// Implementation of <see cref="SourceText" /> based on a string input.
/// </summary>
internal sealed class StringText : SourceText {
    /// <summary>
    /// Creates a <see cref="StringText" /> provided the file name and contents.
    /// </summary>
    /// <param name="fileName">File name of the <see cref="StringText" /> (where the text came from).</param>
    /// <param name="text">The contents of the file the <see cref="StringText" /> comes from.</param>
    internal StringText(string fileName, string source) {
        _lines = ParseLines(this, source);
        this.source = source;
        this.fileName = fileName;
    }

    public override int lineCount => _lines.GetValueOrDefault().Length;

    public override char this[int index] => source[index];

    public override int length => source.Length;

    /// <summary>
    /// The text contents of the source file.
    /// </summary>
    internal string source { get; }

    /// <summary>
    /// The file name of the source file.
    /// </summary>
    internal string fileName { get; }

    public override string ToString(TextSpan span) {
        if (span.start == 0 && span.length == length)
            return source;

        return source.Substring(span.start, span.length);
    }

    public override string ToString() {
        return source;
    }

    public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) {
        source.CopyTo(sourceIndex, destination, destinationIndex, count);
    }

    public override void Write(TextWriter writer) {
        writer.Write(source);
    }

    private protected override void EnsureLines() {
        _lines ??= [];
    }
}
