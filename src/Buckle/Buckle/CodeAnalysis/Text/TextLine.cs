
namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A complete line of text.
/// </summary>
public sealed class TextLine {
    /// <summary>
    /// Creates a <see cref="TextLine" /> instance from a <see cref="SourceText" /> and bounds.
    /// </summary>
    /// <param name="text"><see cref="SourceText" /> to reference.</param>
    /// <param name="start">Start index of the line.</param>
    /// <param name="length">Length of the line.</param>
    /// <param name="lengthWithBreak">Length of the line including line break.</param>
    internal TextLine(SourceText text, int start, int length, int lengthWithBreak) {
        this.text = text;
        this.start = start;
        this.length = length;
        this.lengthWithBreak = lengthWithBreak;
    }

    /// <summary>
    /// <see cref="SourceText" /> the line resides in.
    /// </summary>
    public SourceText text { get; }

    /// <summary>
    /// Start index of the line relative to the entire <see cref="SourceText" />.
    /// </summary>
    public int start { get; }

    /// <summary>
    /// Length of the line.
    /// </summary>
    public int length { get; }

    /// <summary>
    /// End index of the line relative to the entire <see cref="SourceText" />.
    /// </summary>
    public int end => start + length;

    /// <summary>
    /// Length of the line including the line break.
    /// </summary>
    public int lengthWithBreak { get; }

    /// <summary>
    /// A <see cref="TextSpan" /> of the entire line.
    /// </summary>
    public TextSpan span => new TextSpan(start, length);

    /// <summary>
    /// A <see cref="TextSpan" /> of the entire line including the line break.
    /// </summary>
    public TextSpan spanWithBreak => new TextSpan(start, lengthWithBreak);

    public override string ToString() => text.ToString(span);
}
