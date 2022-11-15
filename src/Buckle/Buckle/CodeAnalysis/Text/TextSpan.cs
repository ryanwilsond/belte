using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A segment of a line.
/// </summary>
public sealed class TextSpan {
    /// <summary>
    /// Creates a text span.
    /// </summary>
    /// <param name="start">Start index of the span (inclusive)</param>
    /// <param name="length">How long the text span lasts</param>
    public TextSpan(int start, int length) {
        this.start = start;
        this.length = length;
    }

    /// <summary>
    /// The start index of the span relative to a file (not a line).
    /// </summary>
    public int start { get; }

    /// <summary>
    /// The length of the span.
    /// </summary>
    public int length { get; }

    /// <summary>
    /// The end index of the span.
    /// </summary>
    public int end => start + length;

    /// <summary>
    /// Creates a span from bounds instead of a length.
    /// </summary>
    /// <param name="start">Start index of the span (inclusive)</param>
    /// <param name="end">End index of the span (inclusive)</param>
    /// <returns>New text span</returns>
    public static TextSpan FromBounds(int start, int end) {
        var length = end - start;
        return new TextSpan(start, length);
    }

    /// <summary>
    /// Checks if another span overlaps (at all) with this one.
    /// </summary>
    /// <param name="span">Span to compare with</param>
    /// <returns>If any overlap was found</returns>
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
    /// Checks how much two spans overlap.
    /// </summary>
    /// <param name="x">Span 1</param>
    /// <param name="y">Span 2</param>
    /// <returns>How much x and y overlap (by number of characters)</returns>
    public int Compare(TextSpan x, TextSpan y) {
        int cmp = x.start - y.start;

        if (cmp == 0)
            cmp = x.length - y.length;

        return cmp;
    }
}
