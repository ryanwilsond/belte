using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Xunit;

namespace Buckle.Tests.CodeAnalysis.Syntax {
    public class LexerTests {
        [Fact]
        public void Lexer_Lexes_UnterminatedString() {
            const string text = "\"test";
            var tokens = SyntaxTree.ParseTokens(text, out var diagnostics);
            var token = Assert.Single(tokens);
            Assert.Equal(SyntaxType.STRING, token.type);
            Assert.Equal(text, token.text);
            Assert.Equal(1, diagnostics.count);
            var diagnostic = diagnostics.Pop();
            Assert.Equal(0, diagnostic.span.start);
            Assert.Equal(1, diagnostic.span.length);
            Assert.Equal("unterminated string literal", diagnostic.msg);
        }

        [Fact]
        public void Lexer_Covers_AllTokens() {
            var tokenTypes = Enum.GetValues(typeof(SyntaxType))
                .Cast<SyntaxType>()
                .Where(k => k.ToString().EndsWith("_KEYWORD") ||
                    (k.ToString().Length - k.ToString().Replace("_", "").Length == 0));

            var testedTokenTypes = GetTokens().Concat(GetSeparators()).Select(t => t.type);

            var untestedTokenTypes = new SortedSet<SyntaxType>(tokenTypes);
            untestedTokenTypes.Remove(SyntaxType.Invalid);
            untestedTokenTypes.Remove(SyntaxType.EOF);
            untestedTokenTypes.Remove(SyntaxType.PARAMETER); // not a token
            untestedTokenTypes.ExceptWith(testedTokenTypes);

            Assert.Empty(untestedTokenTypes);
        }

        [Theory]
        [MemberData(nameof(GetTokensData))]
        internal void Lexer_Lexes_Token(SyntaxType type, string text) {
            var tokens = SyntaxTree.ParseTokens(text);

            var token = Assert.Single(tokens);
            Assert.Equal(type, token.type);
            Assert.Equal(text, token.text);
        }

        [Theory]
        [MemberData(nameof(GetTokenPairsData))]
        internal void Lexer_Lexes_TokenPairs(SyntaxType t1Type, string t1Text,
                                           SyntaxType t2Type, string t2Text) {
            var text = t1Text + t2Text;
            var tokens = SyntaxTree.ParseTokens(text).ToArray();

            Assert.Equal(2, tokens.Length);
            Assert.Equal(tokens[0].type, t1Type);
            Assert.Equal(tokens[0].text, t1Text);
            Assert.Equal(tokens[1].type, t2Type);
            Assert.Equal(tokens[1].text, t2Text);
        }

        [Theory]
        [MemberData(nameof(GetTokenPairsWithSeparatorData))]
        internal void Lexer_Lexes_TokenPairs_WithSeparators(SyntaxType t1Type, string t1Text,
                                                          SyntaxType separatorType, string separatorText,
                                                          SyntaxType t2Type, string t2Text) {
            var text = t1Text + separatorText + t2Text;
            var tokens = SyntaxTree.ParseTokens(text).ToArray();

            Assert.Equal(3, tokens.Length);
            Assert.Equal(tokens[0].type, t1Type);
            Assert.Equal(tokens[0].text, t1Text);
            Assert.Equal(tokens[1].type, separatorType);
            Assert.Equal(tokens[1].text, separatorText);
            Assert.Equal(tokens[2].type, t2Type);
            Assert.Equal(tokens[2].text, t2Text);
        }

        public static IEnumerable<object[]> GetTokensData() {
            foreach (var t in GetTokens().Concat(GetSeparators()))
                yield return new object[] { t.type, t.text };
        }

        public static IEnumerable<object[]> GetTokenPairsData() {
            foreach (var t in GetTokenPairs())
                yield return new object[] { t.t1Type, t.t1Text, t.t2Type, t.t2Text };
        }

        public static IEnumerable<object[]> GetTokenPairsWithSeparatorData() {
            foreach (var t in GetTokenPairsWithSeparator())
                yield return new object[] { t.t1Type, t.t1Text, t.separatorType, t.separatorText, t.t2Type, t.t2Text };
        }

        private static IEnumerable<(SyntaxType type, string text)> GetTokens() {
            var fixedTokens = Enum.GetValues(typeof(SyntaxType))
                                  .Cast<SyntaxType>()
                                  .Select(k => (type: k, text: SyntaxFacts.GetText(k)))
                                  .Where(t => t.text != null);

            var dynamicTokens = new[] {
                (SyntaxType.NUMBER, "1"),
                (SyntaxType.NUMBER, "123"),
                (SyntaxType.IDENTIFIER, "a"),
                (SyntaxType.IDENTIFIER, "abc"),
                (SyntaxType.STRING, "\"Test\""),
                (SyntaxType.STRING, "\"Te\"\"st\""),
            };

            return fixedTokens.Concat(dynamicTokens);
        }

        private static IEnumerable<(SyntaxType type, string text)> GetSeparators() {
            return new[] {
                (SyntaxType.WHITESPACE, " "),
                (SyntaxType.WHITESPACE, "  "),
                (SyntaxType.WHITESPACE, "\r"),
                (SyntaxType.WHITESPACE, "\n"),
                (SyntaxType.WHITESPACE, "\r\n")
            };
        }

        private static bool RequiresSeparator(SyntaxType t1Type, SyntaxType t2Type) {
            var t1IsKeyword = t1Type.ToString().EndsWith("KEYWORD");
            var t2IsKeyword = t2Type.ToString().EndsWith("KEYWORD");

            if (t1Type == SyntaxType.IDENTIFIER && t2Type == SyntaxType.IDENTIFIER) return true;
            if (t1IsKeyword && t2IsKeyword) return true;
            if (t1IsKeyword && t2Type == SyntaxType.IDENTIFIER) return true;
            if (t1Type == SyntaxType.IDENTIFIER && t2IsKeyword) return true;
            if (t1Type == SyntaxType.NUMBER && t2Type == SyntaxType.NUMBER) return true;
            if (t1Type == SyntaxType.BANG && t2Type == SyntaxType.EQUALS) return true;
            if (t1Type == SyntaxType.BANG && t2Type == SyntaxType.DEQUALS) return true;
            if (t1Type == SyntaxType.EQUALS && t2Type == SyntaxType.EQUALS) return true;
            if (t1Type == SyntaxType.EQUALS && t2Type == SyntaxType.DEQUALS) return true;
            if (t1Type == SyntaxType.ASTERISK && t2Type == SyntaxType.ASTERISK) return true;
            if (t1Type == SyntaxType.DASTERISK && t2Type == SyntaxType.ASTERISK) return true;
            if (t1Type == SyntaxType.ASTERISK && t2Type == SyntaxType.DASTERISK) return true;
            if (t1Type == SyntaxType.DASTERISK && t2Type == SyntaxType.DASTERISK) return true;
            if (t1Type == SyntaxType.LANGLEBRACKET && t2Type == SyntaxType.EQUALS) return true;
            if (t1Type == SyntaxType.LANGLEBRACKET && t2Type == SyntaxType.DEQUALS) return true;
            if (t1Type == SyntaxType.RANGLEBRACKET && t2Type == SyntaxType.EQUALS) return true;
            if (t1Type == SyntaxType.RANGLEBRACKET && t2Type == SyntaxType.DEQUALS) return true;
            if (t1Type == SyntaxType.PIPE && t2Type == SyntaxType.PIPE) return true;
            if (t1Type == SyntaxType.PIPE && t2Type == SyntaxType.DPIPE) return true;
            if (t1Type == SyntaxType.DPIPE && t2Type == SyntaxType.PIPE) return true;
            if (t1Type == SyntaxType.AMPERSAND && t2Type == SyntaxType.AMPERSAND) return true;
            if (t1Type == SyntaxType.AMPERSAND && t2Type == SyntaxType.DAMPERSAND) return true;
            if (t1Type == SyntaxType.DAMPERSAND && t2Type == SyntaxType.AMPERSAND) return true;
            if (t1Type == SyntaxType.LANGLEBRACKET && t2Type == SyntaxType.LESSEQUAL) return true;
            if (t1Type == SyntaxType.LANGLEBRACKET && t2Type == SyntaxType.SHIFTLEFT) return true;
            if (t1Type == SyntaxType.SHIFTLEFT && t2Type == SyntaxType.LESSEQUAL) return true;
            if (t1Type == SyntaxType.LANGLEBRACKET && t2Type == SyntaxType.LANGLEBRACKET) return true;
            if (t1Type == SyntaxType.RANGLEBRACKET && t2Type == SyntaxType.GREATEQUAL) return true;
            if (t1Type == SyntaxType.RANGLEBRACKET && t2Type == SyntaxType.SHIFTRIGHT) return true;
            if (t1Type == SyntaxType.SHIFTRIGHT && t2Type == SyntaxType.GREATEQUAL) return true;
            if (t1Type == SyntaxType.RANGLEBRACKET && t2Type == SyntaxType.RANGLEBRACKET) return true;
            if (t1Type == SyntaxType.STRING && t2Type == SyntaxType.STRING) return true;

            return false;
        }

        private static
        IEnumerable<(SyntaxType t1Type, string t1Text, SyntaxType t2Type, string t2Text)> GetTokenPairs() {
            foreach (var t1 in GetTokens()) {
                foreach (var t2 in GetTokens()) {
                    if (!RequiresSeparator(t1.type, t2.type))
                        yield return (t1.type, t1.text, t2.type, t2.text);
                }
            }
        }

        private static
        IEnumerable<(SyntaxType t1Type, string t1Text, SyntaxType separatorType, string separatorText, SyntaxType t2Type, string t2Text)> GetTokenPairsWithSeparator() {
            foreach (var t1 in GetTokens()) {
                foreach (var t2 in GetTokens()) {
                    if (RequiresSeparator(t1.type, t2.type)) {
                        foreach (var s in GetSeparators())
                            yield return (t1.type, t1.text, s.type, s.text, t2.type, t2.text);
                    }
                }
            }
        }
    }
}
