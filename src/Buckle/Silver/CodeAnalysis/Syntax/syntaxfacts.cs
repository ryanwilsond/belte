
namespace Buckle.CodeAnalysis.Syntax {

    internal static class SyntaxFacts {

        public static int GetBinaryPrecedence(this SyntaxType type) {
            switch(type) {
                case SyntaxType.PLUS:
                case SyntaxType.MINUS:
                    return 1;
                case SyntaxType.ASTERISK:
                case SyntaxType.SOLIDUS:
                    return 2;
                default: return 0;
            }
        }

        public static int GetUnaryPrecedence(this SyntaxType type) {
            switch(type) {
                case SyntaxType.PLUS:
                case SyntaxType.MINUS:
                    return 3;
                default: return 0;
            }
        }
    }
}
