
namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A segment of a line.
/// </summary>
public sealed class TextSpan {
    /// <summary>
    /// Creates a <see cref="TextSpan" />.
    /// </summary>
    /// <param name="start">Start index of the <see cref="TextSpan" /> (inclusive).</param>
    /// <param name="length">How long the <see cref="TextSpan" /> lasts.</param>
    public TextSpan(int start, int length) {
        this.start = start;
        this.length = length;
    }

    /// <summary>
    /// The start index of the <see cref="TextSpan" /> relative to a file (not a line).
    /// </summary>
    public int start { get; }

    /// <summary>
    /// The length of the <see cref="TextSpan" />.
    /// </summary>
    public int length { get; }

    /// <summary>
    /// The end index of the <see cref="TextSpan" />.
    /// </summary>
    public int end => start + length;

    /// <summary>
    /// Creates a <see cref="TextSpan" /> from bounds instead of a length.
    /// </summary>
    /// <param name="start">Start index of the <see cref="TextSpan" /> (inclusive).</param>
    /// <param name="end">End index of the <see cref="TextSpan" /> (inclusive).</param>
    /// <returns>New <see cref="TextSpan" />.</returns>
    public static TextSpan FromBounds(int start, int end) {
        var length = end - start;
        return new TextSpan(start, length);
    }

    /// <summary>
    /// Checks if another <see cref="TextSpan" /> overlaps (at all) with this one.
    /// </summary>
    /// <param name="span"><see cref="TextSpan" /> to compare with.</param>
    /// <returns>If any overlap was found.</returns>
    public bool OverlapsWith(TextSpan span) {
        return start < span.end && end > span.start;
    }

    /// <summary>
    /// Determines whether the position lies within the span.
    /// </summary>
    /// <param name="position">Position to check.</param>
    /// <returns>
    /// <c>true</c> if the position is greater than or equal to start and strictly less than end,
    /// otherwise <c>false</c>.
    /// </returns>
    internal bool Contains(int position) {
        return unchecked((uint)(position - start) < (uint)length);
    }

    public override string ToString() => $"{start}..{end}";
}
