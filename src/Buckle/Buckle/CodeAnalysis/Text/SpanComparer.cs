using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// Compares two <see cref="TextSpans" />.
/// </summary>
public sealed class SpanComparer : IComparer<TextSpan> {
    /// <summary>
    /// Checks how much two <see cref="TextSpans" /> overlap.
    /// </summary>
    /// <param name="a"><see cref="TextSpan" /> to compare.</param>
    /// <param name="b"><see cref="TextSpan" /> to compare.</param>
    /// <returns>How much <paramref name="a" /> and <paramref name="b" /> overlap (by number of characters).</returns>
    public int Compare(TextSpan a, TextSpan b) {
        int cmp = a.start - b.start;

        if (cmp == 0)
            cmp = a.length - b.length;

        return cmp;
    }
}
