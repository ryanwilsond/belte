
namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A specific location in a source file.
/// </summary>
public sealed class TextLocation {
    /// <summary>
    /// Creates a text location.
    /// </summary>
    /// <param name="text">Source text the location is referencing</param>
    /// <param name="span">Span of how much text location is referencing</param>
    public TextLocation(SourceText text, TextSpan span) {
        this.text = text;
        this.span = span;
    }

    /// <summary>
    /// Source text the location resides in.
    /// </summary>
    public SourceText text { get; }

    /// <summary>
    /// The amount of text the location is referring to.
    /// </summary>
    public TextSpan span { get; }

    /// <summary>
    /// The filename of the source text.
    /// </summary>
    public string fileName => text.fileName;

    /// <summary>
    /// Checks what line (divided by line breaks) the text location refers to by start of span.
    /// If the text location refers to multiple lines, it returns the first line's index.
    /// </summary>
    /// <returns>Index of line in source text</returns>
    public int startLine => text.GetLineIndex(span.start);

    /// <summary>
    /// Checks what line (divided by line breaks) the text location refers to by end of span.
    /// If the text location refers to multiple lines, it returns the last line's index.
    /// </summary>
    /// <returns>Index of line in source text</returns>
    public int endline => text.GetLineIndex(span.end);

    /// <summary>
    /// Index of the first character relative to the line (not entire source text).
    /// </summary>
    public int startCharacter => span.start - text.lines[startLine].start;

    /// <summary>
    /// Index of the last character relative to the line (not entire source text).
    /// </summary>
    public int endCharacter => span.end - text.lines[startLine].start;
}
