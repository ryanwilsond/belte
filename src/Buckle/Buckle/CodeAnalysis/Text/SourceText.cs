using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Text;

public sealed class SourceText {
    private readonly string text_;

    /// <summary>
    /// Creates a source text provided the file name and contents.
    /// </summary>
    /// <param name="fileName">File name of the source text (where the text came from)</param>
    /// <param name="text">The contents of the file the source text comes from</param>
    private SourceText(string fileName, string text) {
        lines = ParseLines(this, text);
        text_ = text;
        this.fileName = fileName;
    }

    /// <summary>
    /// All lines in the source text.
    /// </summary>
    public ImmutableArray<TextLine> lines { get; }

    /// <summary>
    /// The file name of the source file.
    /// </summary>
    public string fileName { get; }

    /// <summary>
    /// Indexing the source file contents.
    /// </summary>
    public char this[int index] => text_[index];

    /// <summary>
    /// The length of the entire source text.
    /// </summary>
    public int length => text_.Length;

    /// <summary>
    /// Creates a source text from a text, not necessarily relating to a source file.
    /// </summary>
    /// <param name="text">Text</param>
    /// <param name="filename">Optional filename if sourced from a file</param>
    /// <returns>New SourceText</returns>
    public static SourceText From(string text, string filename = "") {
        return new SourceText(filename, text);
    }

    /// <summary>
    /// Gets a line index based on an absolute index.
    /// </summary>
    /// <param name="position">Absolute index</param>
    /// <returns>Line index</returns>
    public int GetLineIndex(int position) {
        int lower = 0;
        int upper = lines.Length - 1;

        while (lower <= upper) {
            int index = lower + (upper - lower) / 2;
            int start = lines[index].start;

            if (position == start)
                return index;
            if (start > position)
                upper = index - 1;
            else
                lower = index + 1;
        }

        return lower - 1;
    }

    /// <summary>
    /// String representation of entire text.
    /// </summary>
    /// <returns>Entire text (not modified)</returns>
    public override string ToString() => text_;

    /// <summary>
    /// String representation of part of the text.
    /// </summary>
    /// <param name="start">Start index (absolute)</param>
    /// <param name="length">Length of text to grab</param>
    /// <returns>Substring of the text</returns>
    public string ToString(int start, int length) => text_.Substring(start, length);

    /// <summary>
    /// String representation of part of the text.
    /// </summary>
    /// <param name="span">Span of what text to grab (inclusive)</param>
    /// <returns>Substring of the text</returns>
    public string ToString(TextSpan span) => ToString(span.start, span.length);

    /// <summary>
    /// Checks if the span starts at the end of the text.
    /// Does not return true if the span reaches the end, only if it starts at the end.
    /// If the span extends beyond the text, the result will not be different.
    /// </summary>
    /// <param name="span">Span to check</param>
    /// <returns>If the span is at the end of the text</returns>
    public bool IsAtEndOfInput(TextSpan span) {
        if (span.start == text_.Length)
            return true;

        return false;
    }

    private static ImmutableArray<TextLine> ParseLines(SourceText pointer, string text) {
        var result = ImmutableArray.CreateBuilder<TextLine>();

        int position = 0;
        int lineStart = 0;

        while (position < text.Length) {
            var linebreakWidth = GetLineBreakWidth(text, position);

            if (linebreakWidth == 0)
                position++;
            else {
                AddLine(result, pointer, position, lineStart, linebreakWidth);
                position += linebreakWidth;
                lineStart = position;
            }
        }

        if (position >= lineStart)
            AddLine(result, pointer, position, lineStart, 0);

        return result.ToImmutable();
    }

    private static void AddLine(ImmutableArray<TextLine>.Builder result, SourceText pointer,
        int position, int lineStart, int linebreakWidth) {
        var lineLength = position - lineStart;
        var line = new TextLine(pointer, lineStart, lineLength, lineLength + linebreakWidth);
        result.Add(line);
    }

    private static int GetLineBreakWidth(string text, int i) {
        var c = text[i];
        var l = i + 1 >= text.Length ? '\0' : text[i + 1];

        if (c == '\r' && l == '\n')
            return 2;
        if (c == '\r' || c == '\n')
            return 1;
        return 0;
    }
}
