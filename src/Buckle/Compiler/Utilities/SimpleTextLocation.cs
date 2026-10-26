
namespace Buckle;

public readonly struct SimpleTextLocation {
    public readonly int syntaxTreeOrdinal;
    public readonly int spanStart;
    public readonly int spanLength;

    public SimpleTextLocation(int syntaxTreeOrdinal, int spanStart, int spanLength) {
        this.syntaxTreeOrdinal = syntaxTreeOrdinal;
        this.spanStart = spanStart;
        this.spanLength = spanLength;
    }
}
