using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Text {

    public sealed class SourceText {
        public ImmutableArray<TextLine> lines { get; }
        private readonly string text_;

        public char this[int index] => text_[index];

        public int length => text_.Length;

        private SourceText(string text) {
            lines = ParseLines(this, text);
            text_ = text;
        }

        public int GetLineIndex(int position) {
            int lower = 0;
            int upper = lines.Length - 1;

            while (lower <= upper) {
                int index = lower + (upper - lower) / 2;
                int start = lines[index].start;

                if (position == start) return index;
                if (start > position) upper = index - 1;
                else lower = index + 1;
            }

            return lower - 1;
        }

        private static ImmutableArray<TextLine> ParseLines(SourceText pointer, string text) {
            var result = ImmutableArray.CreateBuilder<TextLine>();

            int position = 0;
            int lineStart = 0;

            while (position < text.Length) {
                var linebreakWidth = GetLineBreakWidth(text, position);

                if (linebreakWidth == 0) position++;
                else {
                    AddLine(result, pointer, position, lineStart, linebreakWidth);

                    position += linebreakWidth;
                    lineStart = position;
                }
            }

            if (position >= lineStart)
                AddLine(result, pointer, position, lineStart, 0);

            return result.ToImmutable();
        }

        private static void AddLine(ImmutableArray<TextLine>.Builder result, SourceText pointer,
            int position, int lineStart, int linebreakWidth) {
            var lineLength = position - lineStart;
            var line = new TextLine(pointer, lineStart, lineLength, lineLength + linebreakWidth);
            result.Add(line);
        }

        private static int GetLineBreakWidth(string text, int i) {
            var c = text[i];
            var l = i + 1 >= text.Length ? '\0' : text[i+1];

            if (c == '\r' && l == '\n') return 2;
            if (c == '\r' || c == '\n') return 1;
            return 0;
        }

        public static SourceText From(string text) {
            return new SourceText(text);
        }

        public override string ToString() => text_;
        public string ToString(int start, int length) => text_.Substring(start, length);
        public string ToString(TextSpan span) => ToString(span.start, span.length);

        public bool IsAtEndOfInput(TextSpan span) {
            if (span.start == text_.Length) return true;
            return false;
        }
    }
}
