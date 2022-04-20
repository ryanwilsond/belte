using System.Collections.Generic;
using System;

namespace Buckle.CodeAnalysis.Syntax {

    internal static class SyntaxFacts {

        public static int GetBinaryPrecedence(this SyntaxType type) {
            switch (type) {
                case SyntaxType.ASTERISK_ASTERISK_TOKEN:
                    return 11;
                case SyntaxType.ASTERISK_TOKEN:
                case SyntaxType.SLASH_TOKEN:
                    return 10;
                case SyntaxType.PLUS_TOKEN:
                case SyntaxType.MINUS_TOKEN:
                    return 9;
                case SyntaxType.LESS_THAN_LESS_THAN_TOKEN:
                case SyntaxType.GREATER_THAN_GREATER_THAN_TOKEN:
                    return 8;
                case SyntaxType.LESS_THAN_TOKEN:
                case SyntaxType.GREATER_THAN_TOKEN:
                case SyntaxType.LESS_THAN_EQUALS_TOKEN:
                case SyntaxType.GREATER_THAN_EQUALS_TOKEN:
                    return 7;
                case SyntaxType.EQUALS_EQUALS_TOKEN:
                case SyntaxType.EXCLAMATION_EQUALS_TOKEN:
                    return 6;
                case SyntaxType.AMPERSAND_TOKEN:
                    return 5;
                case SyntaxType.CARET_TOKEN:
                    return 4;
                case SyntaxType.PIPE_TOKEN:
                    return 3;
                case SyntaxType.AMPERSAND_AMPERSAND_TOKEN:
                    return 2;
                case SyntaxType.PIPE_PIPE_TOKEN:
                    return 1;
                default: return 0;
            }
        }

        public static int GetUnaryPrecedence(this SyntaxType type) {
            switch (type) {
                case SyntaxType.PLUS_TOKEN:
                case SyntaxType.MINUS_TOKEN:
                case SyntaxType.EXCLAMATION_TOKEN:
                case SyntaxType.TILDE_TOKEN:
                    return 12;
                default: return 0;
            }
        }

        public static SyntaxType GetKeywordType(string text) {
            switch (text) {
                case "true": return SyntaxType.TRUE_KEYWORD;
                case "false": return SyntaxType.FALSE_KEYWORD;
                case "auto": return SyntaxType.AUTO_KEYWORD;
                case "let": return SyntaxType.LET_KEYWORD;
                case "if": return SyntaxType.IF_KEYWORD;
                case "else": return SyntaxType.ELSE_KEYWORD;
                case "while": return SyntaxType.WHILE_KEYWORD;
                case "for": return SyntaxType.FOR_KEYWORD;
                case "do": return SyntaxType.DO_KEYWORD;
                case "break": return SyntaxType.BREAK_KEYWORD;
                case "continue": return SyntaxType.CONTINUE_KEYWORD;
                case "return": return SyntaxType.RETURN_KEYWORD;
                default: return SyntaxType.IDENTIFIER_TOKEN;
            }
        }

        public static string GetText(SyntaxType type) {
            switch (type) {
                case SyntaxType.COMMA_TOKEN: return ",";
                case SyntaxType.PLUS_TOKEN: return "+";
                case SyntaxType.MINUS_TOKEN: return "-";
                case SyntaxType.ASTERISK_TOKEN: return "*";
                case SyntaxType.SLASH_TOKEN: return "/";
                case SyntaxType.OPEN_PAREN_TOKEN: return "(";
                case SyntaxType.CLOSE_PAREN_TOKEN: return ")";
                case SyntaxType.EQUALS_TOKEN: return "=";
                case SyntaxType.TILDE_TOKEN: return "~";
                case SyntaxType.CARET_TOKEN: return "^";
                case SyntaxType.AMPERSAND_TOKEN: return "&";
                case SyntaxType.PIPE_TOKEN: return "|";
                case SyntaxType.LESS_THAN_LESS_THAN_TOKEN: return "<<";
                case SyntaxType.GREATER_THAN_GREATER_THAN_TOKEN: return ">>";
                case SyntaxType.EXCLAMATION_TOKEN: return "!";
                case SyntaxType.AMPERSAND_AMPERSAND_TOKEN: return "&&";
                case SyntaxType.PIPE_PIPE_TOKEN: return "||";
                case SyntaxType.ASTERISK_ASTERISK_TOKEN: return "**";
                case SyntaxType.OPEN_BRACE_TOKEN: return "{";
                case SyntaxType.CLOSE_BRACE_TOKEN: return "}";
                case SyntaxType.SEMICOLON_TOKEN: return ";";
                case SyntaxType.EQUALS_EQUALS_TOKEN: return "==";
                case SyntaxType.EXCLAMATION_EQUALS_TOKEN: return "!=";
                case SyntaxType.LESS_THAN_TOKEN: return "<";
                case SyntaxType.GREATER_THAN_TOKEN: return ">";
                case SyntaxType.LESS_THAN_EQUALS_TOKEN: return "<=";
                case SyntaxType.GREATER_THAN_EQUALS_TOKEN: return ">=";
                case SyntaxType.TRUE_KEYWORD: return "true";
                case SyntaxType.FALSE_KEYWORD: return "false";
                case SyntaxType.AUTO_KEYWORD: return "auto";
                case SyntaxType.LET_KEYWORD: return "let";
                case SyntaxType.IF_KEYWORD: return "if";
                case SyntaxType.ELSE_KEYWORD: return "else";
                case SyntaxType.WHILE_KEYWORD: return "while";
                case SyntaxType.FOR_KEYWORD: return "for";
                case SyntaxType.DO_KEYWORD: return "do";
                case SyntaxType.BREAK_KEYWORD: return "break";
                case SyntaxType.CONTINUE_KEYWORD: return "continue";
                case SyntaxType.RETURN_KEYWORD: return "return";
                default: return null;
            }
        }

        public static IEnumerable<SyntaxType> GetUnaryOperatorTypes() {
            var types = (SyntaxType[])Enum.GetValues(typeof(SyntaxType));
            foreach (var type in types) {
                if (GetUnaryPrecedence(type) > 0)
                    yield return type;
            }
        }

        public static IEnumerable<SyntaxType> GetBinaryOperatorTypes() {
            var types = (SyntaxType[])Enum.GetValues(typeof(SyntaxType));
            foreach (var type in types) {
                if (GetBinaryPrecedence(type) > 0)
                    yield return type;
            }
        }

        public static bool IsKeyword(this SyntaxType type) {
            return type.ToString().EndsWith("KEYWORD");
        }

        public static bool IsToken(this SyntaxType type) {
            return !type.IsTrivia() && (type.IsKeyword() || type.ToString().EndsWith("TOKEN"));
        }

        public static bool IsTrivia(this SyntaxType type) {
            return type.ToString().EndsWith("TRIVIA");
        }

        public static bool IsComment(this SyntaxType type) {
            return type == SyntaxType.SINGLELINE_COMMENT_TRIVIA || type == SyntaxType.MULTILINE_COMMENT_TRIVIA;
        }
    }
}
