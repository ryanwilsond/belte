
namespace Buckle.CodeAnalysis.Text {

    public class TextSpan {
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
    }
}
