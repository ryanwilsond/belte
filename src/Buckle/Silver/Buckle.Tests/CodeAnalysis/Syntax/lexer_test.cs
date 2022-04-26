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
            Assert.Equal(SyntaxType.STRING_LITERAL_TOKEN, token.type);
            Assert.Equal(text, token.text);
            Assert.Equal(1, diagnostics.count);
            var diagnostic = diagnostics.Pop();
            Assert.Equal(0, diagnostic.location.span.start);
            Assert.Equal(1, diagnostic.location.span.length);
            Assert.Equal("unterminated string literal", diagnostic.msg);
        }

        [Fact]
        public void Lexer_Covers_AllTokens() {
            var tokenTypes = Enum.GetValues(typeof(SyntaxType))
                .Cast<SyntaxType>()
                .Where(k => k.IsToken());

            var testedTokenTypes = GetTokens().Concat(GetSeparators()).Select(t => t.type);

            var untestedTokenTypes = new SortedSet<SyntaxType>(tokenTypes);
            untestedTokenTypes.Remove(SyntaxType.BAD_TOKEN);
            untestedTokenTypes.Remove(SyntaxType.END_OF_FILE_TOKEN);
            untestedTokenTypes.Remove(SyntaxType.SINGLELINE_COMMENT_TRIVIA);
            untestedTokenTypes.Remove(SyntaxType.MULTILINE_COMMENT_TRIVIA);
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
        [MemberData(nameof(GetSeparatorsData))]
        internal void Lexer_Lexes_Separator(SyntaxType type, string text) {
            var tokens = SyntaxTree.ParseTokens(text, true);

            var token = Assert.Single(tokens);
            var trivia = Assert.Single(token.leadingTrivia);
            Assert.Equal(type, trivia.type);
            Assert.Equal(text, trivia.text);
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

            Assert.Equal(2, tokens.Length);
            Assert.Equal(tokens[0].type, t1Type);
            Assert.Equal(tokens[0].text, t1Text);

            var separator = Assert.Single(tokens[0].trailingTrivia);
            Assert.Equal(separator.text, separatorText);
            Assert.Equal(separator.type, separatorType);

            Assert.Equal(tokens[1].type, t2Type);
            Assert.Equal(tokens[1].text, t2Text);
        }

        [Theory]
        [InlineData("foo")]
        [InlineData("foo42")]
        [InlineData("foo_42")]
        [InlineData("_foo")]
        public void Lexer_Lexes_Identifiers(string name) {
            var tokens = SyntaxTree.ParseTokens(name).ToArray();

            Assert.Single(tokens);

            var token = tokens[0];
            Assert.Equal(SyntaxType.IDENTIFIER_TOKEN, token.type);
            Assert.Equal(name, token.text);
        }

        public static IEnumerable<object[]> GetTokensData() {
            foreach (var t in GetTokens())
                yield return new object[] { t.type, t.text };
        }

        public static IEnumerable<object[]> GetSeparatorsData() {
            foreach (var t in GetSeparators())
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
                (SyntaxType.NUMBERIC_LITERAL_TOKEN, "1"),
                (SyntaxType.NUMBERIC_LITERAL_TOKEN, "123"),
                (SyntaxType.IDENTIFIER_TOKEN, "a"),
                (SyntaxType.IDENTIFIER_TOKEN, "abc"),
                (SyntaxType.STRING_LITERAL_TOKEN, "\"Test\""),
                (SyntaxType.STRING_LITERAL_TOKEN, "\"Te\"\"st\""),
            };

            return fixedTokens.Concat(dynamicTokens);
        }

        private static IEnumerable<(SyntaxType type, string text)> GetSeparators() {
            return new[] {
                (SyntaxType.WHITESPACE_TRIVIA, " "),
                (SyntaxType.WHITESPACE_TRIVIA, "  "),
                (SyntaxType.END_OF_LINE_TRIVIA, "\r"),
                (SyntaxType.END_OF_LINE_TRIVIA, "\n"),
                (SyntaxType.END_OF_LINE_TRIVIA, "\r\n")
            };
        }

        private static bool RequiresSeparator(SyntaxType t1Type, SyntaxType t2Type) {
            var t1IsKeyword = t1Type.IsKeyword();
            var t2IsKeyword = t2Type.IsKeyword();

            if (t1Type == SyntaxType.IDENTIFIER_TOKEN && t2Type == SyntaxType.IDENTIFIER_TOKEN) return true;
            if (t1IsKeyword && t2IsKeyword) return true;
            if (t1IsKeyword && t2Type == SyntaxType.IDENTIFIER_TOKEN) return true;
            if (t1Type == SyntaxType.IDENTIFIER_TOKEN && t2IsKeyword) return true;
            if (t1Type == SyntaxType.NUMBERIC_LITERAL_TOKEN && t2Type == SyntaxType.NUMBERIC_LITERAL_TOKEN) return true;
            if (t1Type == SyntaxType.EXCLAMATION_TOKEN && t2Type == SyntaxType.EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.EXCLAMATION_TOKEN && t2Type == SyntaxType.EQUALS_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.EQUALS_TOKEN && t2Type == SyntaxType.EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.EQUALS_TOKEN && t2Type == SyntaxType.EQUALS_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.ASTERISK_TOKEN && t2Type == SyntaxType.ASTERISK_TOKEN) return true;
            if (t1Type == SyntaxType.ASTERISK_ASTERISK_TOKEN && t2Type == SyntaxType.ASTERISK_TOKEN) return true;
            if (t1Type == SyntaxType.ASTERISK_TOKEN && t2Type == SyntaxType.ASTERISK_ASTERISK_TOKEN) return true;
            if (t1Type == SyntaxType.ASTERISK_ASTERISK_TOKEN && t2Type == SyntaxType.ASTERISK_ASTERISK_TOKEN)
                return true;
            if (t1Type == SyntaxType.LESS_THAN_TOKEN && t2Type == SyntaxType.EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.LESS_THAN_TOKEN && t2Type == SyntaxType.EQUALS_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.GREATER_THAN_TOKEN && t2Type == SyntaxType.EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.GREATER_THAN_TOKEN && t2Type == SyntaxType.EQUALS_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.PIPE_TOKEN && t2Type == SyntaxType.PIPE_TOKEN) return true;
            if (t1Type == SyntaxType.PIPE_TOKEN && t2Type == SyntaxType.PIPE_PIPE_TOKEN) return true;
            if (t1Type == SyntaxType.PIPE_PIPE_TOKEN && t2Type == SyntaxType.PIPE_TOKEN) return true;
            if (t1Type == SyntaxType.AMPERSAND_TOKEN && t2Type == SyntaxType.AMPERSAND_TOKEN) return true;
            if (t1Type == SyntaxType.AMPERSAND_TOKEN && t2Type == SyntaxType.AMPERSAND_AMPERSAND_TOKEN) return true;
            if (t1Type == SyntaxType.AMPERSAND_AMPERSAND_TOKEN && t2Type == SyntaxType.AMPERSAND_TOKEN) return true;
            if (t1Type == SyntaxType.LESS_THAN_TOKEN && t2Type == SyntaxType.LESS_THAN_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.LESS_THAN_TOKEN && t2Type == SyntaxType.LESS_THAN_LESS_THAN_TOKEN) return true;
            if (t1Type == SyntaxType.LESS_THAN_LESS_THAN_TOKEN && t2Type == SyntaxType.LESS_THAN_EQUALS_TOKEN)
                return true;
            if (t1Type == SyntaxType.LESS_THAN_TOKEN && t2Type == SyntaxType.LESS_THAN_TOKEN) return true;
            if (t1Type == SyntaxType.GREATER_THAN_TOKEN && t2Type == SyntaxType.GREATER_THAN_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.GREATER_THAN_TOKEN && t2Type == SyntaxType.GREATER_THAN_GREATER_THAN_TOKEN)
                return true;
            if (t1Type == SyntaxType.GREATER_THAN_GREATER_THAN_TOKEN && t2Type == SyntaxType.GREATER_THAN_EQUALS_TOKEN)
                return true;
            if (t1Type == SyntaxType.GREATER_THAN_TOKEN && t2Type == SyntaxType.GREATER_THAN_TOKEN) return true;
            if (t1Type == SyntaxType.STRING_LITERAL_TOKEN && t2Type == SyntaxType.STRING_LITERAL_TOKEN) return true;
            if (t1Type == SyntaxType.SLASH_TOKEN && t2Type == SyntaxType.SLASH_TOKEN) return true;
            if (t1Type == SyntaxType.SLASH_TOKEN && t2Type == SyntaxType.ASTERISK_TOKEN) return true;
            if (t1Type == SyntaxType.SLASH_TOKEN && t2Type == SyntaxType.ASTERISK_ASTERISK_TOKEN) return true;
            if (t1Type == SyntaxType.SLASH_TOKEN && t2Type == SyntaxType.MULTILINE_COMMENT_TRIVIA) return true;
            if (t1Type == SyntaxType.SLASH_TOKEN && t2Type == SyntaxType.SINGLELINE_COMMENT_TRIVIA) return true;
            if (t1Type == SyntaxType.IDENTIFIER_TOKEN && t2Type == SyntaxType.NUMBERIC_LITERAL_TOKEN) return true;
            if (t1IsKeyword && t2Type == SyntaxType.NUMBERIC_LITERAL_TOKEN) return true;
            if (t1Type == SyntaxType.PLUS_TOKEN && t2Type == SyntaxType.EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.PLUS_TOKEN && t2Type == SyntaxType.EQUALS_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.MINUS_TOKEN && t2Type == SyntaxType.EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.MINUS_TOKEN && t2Type == SyntaxType.EQUALS_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.ASTERISK_TOKEN && t2Type == SyntaxType.EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.ASTERISK_TOKEN && t2Type == SyntaxType.EQUALS_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.SLASH_TOKEN && t2Type == SyntaxType.EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.SLASH_TOKEN && t2Type == SyntaxType.EQUALS_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.AMPERSAND_TOKEN && t2Type == SyntaxType.EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.AMPERSAND_TOKEN && t2Type == SyntaxType.EQUALS_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.AMPERSAND_TOKEN && t2Type == SyntaxType.AMPERSAND_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.PIPE_TOKEN && t2Type == SyntaxType.EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.PIPE_TOKEN && t2Type == SyntaxType.EQUALS_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.PIPE_TOKEN && t2Type == SyntaxType.PIPE_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.CARET_TOKEN && t2Type == SyntaxType.EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.CARET_TOKEN && t2Type == SyntaxType.EQUALS_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.SLASH_TOKEN && t2Type == SyntaxType.SLASH_EQUALS_TOKEN) return true;
            if (t1Type == SyntaxType.SLASH_TOKEN && t2Type == SyntaxType.ASTERISK_EQUALS_TOKEN) return true;

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
            IEnumerable<(SyntaxType t1Type, string t1Text, SyntaxType separatorType,
            string separatorText, SyntaxType t2Type, string t2Text)>
            GetTokenPairsWithSeparator() {
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
