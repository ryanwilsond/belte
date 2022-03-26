
namespace Buckle.CodeAnalysis.Text {

    public class TextLine {
        public SourceText text { get; }
        public int start { get; }
        public int length { get; }
        public int end => start + length;
        public int lenwbreak { get; }
        public TextSpan span => new TextSpan(start, length);
        public TextSpan spanwbreak => new TextSpan(start, lenwbreak);

        public TextLine(SourceText text_, int start_, int length_, int lenwbreak_) {
            text = text_;
            start = start_;
            length = length_;
            lenwbreak = lenwbreak_;
        }

        public override string ToString() => text.ToString(span);
    }
}
