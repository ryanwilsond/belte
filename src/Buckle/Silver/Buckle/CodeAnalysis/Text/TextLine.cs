
namespace Buckle.CodeAnalysis.Text;

public sealed class TextLine {
    public SourceText text { get; }
    public int start { get; }
    public int length { get; }
    public int end => start + length;
    public int lengthWithBreak { get; }
    public TextSpan span => new TextSpan(start, length);
    public TextSpan spanWithBreak => new TextSpan(start, lengthWithBreak);

    public TextLine(SourceText text_, int start_, int length_, int lengthWithBreak_) {
        text = text_;
        start = start_;
        length = length_;
        lengthWithBreak = lengthWithBreak_;
    }

    public override string ToString() => text.ToString(span);
}
