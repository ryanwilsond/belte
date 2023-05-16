
namespace Buckle.CodeAnalysis.Text;

internal sealed partial class TextChangeRange {
    private readonly struct UnadjustedNewChange {
        internal readonly int spanStart { get; }
        internal readonly int spanLength { get; }
        internal readonly int newLength { get; }

        internal int spanEnd => spanStart + spanLength;

        internal UnadjustedNewChange(int spanStart, int spanLength, int newLength) {
            this.spanStart = spanStart;
            this.spanLength = spanLength;
            this.newLength = newLength;
        }

        internal UnadjustedNewChange(TextChangeRange range)
            : this(range.span.start, range.span.length, range.newLength) { }
    }
}
