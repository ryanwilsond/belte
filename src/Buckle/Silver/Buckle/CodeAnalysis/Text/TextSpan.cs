using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Text;

public sealed class TextSpan {
    public int start { get; }
    public int length { get; }
    public int end => start + length;

    public TextSpan(int start_, int length_) {
        start = start_;
        length = length_;
    }

    public static TextSpan FromBounds(int start, int end) {
        var length = end - start;
        return new TextSpan(start, length);
    }

    public bool OverlapsWith(TextSpan span) {
        return start < span.end && end > span.start;
    }

    public override string ToString() => $"{start}..{end}";
}

public class SpanComparer : IComparer<TextSpan> {
    public int Compare(TextSpan x, TextSpan y) {
        int cmp = x.start - y.start;

        if (cmp == 0)
            cmp = x.length - y.length;

        return cmp;
    }
}
