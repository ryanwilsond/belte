
namespace Buckle.CodeAnalysis.Syntax {

    internal static class SyntaxFacts {

        public static int GetPrimaryPrecedence(this SyntaxType type) {
            switch(type) {
                case SyntaxType.DMINUS:
                case SyntaxType.DPLUS:
                    return 7;
                default: return 0;
            }
        }

        public static int GetBinaryPrecedence(this SyntaxType type) {
            switch(type) {
                case SyntaxType.ASTERISK:
                case SyntaxType.SOLIDUS:
                    return 5;
                case SyntaxType.PLUS:
                case SyntaxType.MINUS:
                    return 4;
                case SyntaxType.DEQUALS:
                case SyntaxType.BANGEQUALS:
                    return 3;
                case SyntaxType.DAMPERSAND:
                    return 2;
                case SyntaxType.DPIPE:
                    return 1;
                default: return 0;
            }
        }

        public static int GetUnaryPrecedence(this SyntaxType type) {
            switch(type) {
                case SyntaxType.PLUS:
                case SyntaxType.MINUS:
                case SyntaxType.BANG:
                case SyntaxType.DMINUS:
                case SyntaxType.DPLUS:
                    return 6;
                default: return 0;
            }
        }

        public static SyntaxType GetKeywordType(string text) {
            switch (text) {
                case "true": return SyntaxType.TRUE_KEYWORD;
                case "false": return SyntaxType.FALSE_KEYWORD;
                default: return SyntaxType.IDENTIFIER;
            }
        }
    }
}
