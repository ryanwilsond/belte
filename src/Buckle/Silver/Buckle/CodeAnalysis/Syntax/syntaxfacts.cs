using System.Collections.Generic;
using System;

namespace Buckle.CodeAnalysis.Syntax {

    internal static class SyntaxFacts {

        // public static int GetPrimaryPrecedence(this SyntaxType type) {
        //     switch(type) {
        //         case SyntaxType.DMINUS:
        //         case SyntaxType.DPLUS:
        //             return 13;
        //         default: return 0;
        //     }
        // }

        public static int GetBinaryPrecedence(this SyntaxType type) {
            switch(type) {
                case SyntaxType.DASTERISK:
                    return 11;
                case SyntaxType.ASTERISK:
                case SyntaxType.SLASH:
                    return 10;
                case SyntaxType.PLUS:
                case SyntaxType.MINUS:
                    return 9;
                case SyntaxType.SHIFTLEFT:
                case SyntaxType.SHIFTRIGHT:
                    return 8;
                case SyntaxType.LANGLEBRACKET:
                case SyntaxType.RANGLEBRACKET:
                case SyntaxType.LESSEQUAL:
                case SyntaxType.GREATEQUAL:
                    return 7;
                case SyntaxType.DEQUALS:
                case SyntaxType.BANGEQUALS:
                    return 6;
                case SyntaxType.AMPERSAND:
                    return 5;
                case SyntaxType.CARET:
                    return 4;
                case SyntaxType.PIPE:
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
                case SyntaxType.TILDE:
                // case SyntaxType.DMINUS:
                // case SyntaxType.DPLUS:
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
                default: return SyntaxType.IDENTIFIER;
            }
        }

        public static string GetText(SyntaxType type) {
            switch(type) {
                case SyntaxType.PLUS: return "+";
                case SyntaxType.MINUS: return "-";
                case SyntaxType.ASTERISK: return "*";
                case SyntaxType.SLASH: return "/";
                case SyntaxType.LPAREN: return "(";
                case SyntaxType.RPAREN: return ")";
                case SyntaxType.EQUALS: return "=";
                case SyntaxType.TILDE: return "~";
                case SyntaxType.CARET: return "^";
                case SyntaxType.AMPERSAND: return "&";
                case SyntaxType.PIPE: return "|";
                case SyntaxType.SHIFTLEFT: return "<<";
                case SyntaxType.SHIFTRIGHT: return ">>";
                case SyntaxType.BANG: return "!";
                case SyntaxType.DAMPERSAND: return "&&";
                case SyntaxType.DPIPE: return "||";
                // case SyntaxType.DMINUS: return "--";
                // case SyntaxType.DPLUS: return "++";
                case SyntaxType.DASTERISK: return "**";
                case SyntaxType.LBRACE: return "{";
                case SyntaxType.RBRACE: return "}";
                case SyntaxType.SEMICOLON: return ";";
                case SyntaxType.DEQUALS: return "==";
                case SyntaxType.BANGEQUALS: return "!=";
                case SyntaxType.LANGLEBRACKET: return "<";
                case SyntaxType.RANGLEBRACKET: return ">";
                case SyntaxType.LESSEQUAL: return "<=";
                case SyntaxType.GREATEQUAL: return ">=";
                case SyntaxType.TRUE_KEYWORD: return "true";
                case SyntaxType.FALSE_KEYWORD: return "false";
                case SyntaxType.AUTO_KEYWORD: return "auto";
                case SyntaxType.LET_KEYWORD: return "let";
                case SyntaxType.IF_KEYWORD: return "if";
                case SyntaxType.ELSE_KEYWORD: return "else";
                case SyntaxType.WHILE_KEYWORD: return "while";
                case SyntaxType.FOR_KEYWORD: return "for";
                default: return null;
            }
        }

        public static IEnumerable<SyntaxType> GetUnaryOperatorTypes() {
            var types = (SyntaxType[]) Enum.GetValues(typeof(SyntaxType));
            foreach (var type in types) {
                if (GetUnaryPrecedence(type) > 0)
                    yield return type;
            }
        }

        public static IEnumerable<SyntaxType> GetBinaryOperatorTypes() {
            var types = (SyntaxType[]) Enum.GetValues(typeof(SyntaxType));
            foreach (var type in types) {
                if (GetBinaryPrecedence(type) > 0)
                    yield return type;
            }
        }
    }
}
