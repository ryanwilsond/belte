
namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A specific location in a source file.
/// </summary>
public sealed class TextLocation {
    /// <summary>
    /// Creates a <see cref="TextLocation" />.
    /// </summary>
    /// <param name="text"><see cref="StringText" /> the location is referencing.</param>
    /// <param name="span"><see cref="TextSpan" /> of how much <see cref="TextLocation" /> is referencing.</param>
    internal TextLocation(SourceText text, TextSpan span) {
        this.text = text;
        this.span = span;
    }

    /// <summary>
    /// <see cref="SourceText" /> the location resides in.
    /// </summary>
    public SourceText text { get; }

    /// <summary>
    /// The amount of text the location is referring to.
    /// </summary>
    public TextSpan span { get; }

    /// <summary>
    /// The filename of the source file.
    /// </summary>
    public string fileName {
        get {
            var stringText = text as StringText;
            return stringText?.fileName;
        }
    }

    /// <summary>
    /// Checks what line (divided by line breaks) the <see cref="TextLocation" /> refers to by start of
    /// <see cref="TextSpan" />.
    /// If the <see cref="TextLocation" /> refers to multiple lines, it returns the first line's index.
    /// </summary>
    /// <returns>Index of line in <see cref="SourceText" />.</returns>
    public int startLine => text.GetLineIndex(span.start);

    /// <summary>
    /// Checks what line (divided by line breaks) the <see cref="TextLocation" /> refers to by end of
    /// <see cref="TextSpan" />.
    /// If the <see cref="TextLocation" /> refers to multiple lines, it returns the last line's index.
    /// </summary>
    /// <returns>Index of line in <see cref="SourceText" />.</returns>
    public int endline => text.GetLineIndex(span.end);

    /// <summary>
    /// Index of the first character relative to the line (not entire <see cref="SourceText" />).
    /// </summary>
    public int startCharacter => span.start - text.lines[startLine].start;

    /// <summary>
    /// Index of the last character relative to the line (not entire <see cref="SourceText" />).
    /// </summary>
    public int endCharacter => span.end - text.lines[startLine].start;
}
