
namespace Buckle.CodeAnalysis.Text {
    public sealed class TextLocation {
        public SourceText text { get; }
        public TextSpan span { get; }
        public string fileName => text.fileName;
        public int startLine => text.GetLineIndex(span.start);
        public int endline => text.GetLineIndex(span.end);
        public int startCharacter => span.start - text.lines[startLine].start;
        public int endCharacter => span.end - text.lines[startLine].start;

        public TextLocation(SourceText text_, TextSpan span_) {
            text = text_;
            span = span_;
        }
    }
}
