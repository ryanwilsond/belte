using System.Collections.Generic;

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

    public override string ToString() => $"{start}..{end}";
}

/// <summary>
/// Compares two text spans.
/// </summary>
public class SpanComparer : IComparer<TextSpan> {
    /// <summary>
    /// Checks how much two test spans overlap.
    /// </summary>
    /// <param name="a"><see cref="TextSpan" /> to compare.</param>
    /// <param name="b"><see cref="TextSpan" /> to compare.</param>
    /// <returns>How much x and y overlap (by number of characters).</returns>
    public int Compare(TextSpan a, TextSpan b) {
        int cmp = a.start - b.start;

        if (cmp == 0)
            cmp = a.length - b.length;

        return cmp;
    }
}
