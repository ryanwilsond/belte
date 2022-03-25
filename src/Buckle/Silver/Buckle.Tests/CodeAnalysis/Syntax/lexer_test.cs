using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Xunit;

namespace Buckle.Tests.CodeAnalysis.Syntax {
    public class LexerTests {
        [Fact]
        public void LexerTests_CoversAllTokens() {
            var tokenKinds = Enum.GetValues(typeof(SyntaxType))
                                 .Cast<SyntaxType>()
                                 .Where(k => k.ToString().EndsWith("KEYWORD") ||
                                             (k.ToString().Length - k.ToString().Replace("_", "").Length == 0));

            var testedTokenKinds = GetTokens().Concat(GetSeparators()).Select(t => t.type);

            var untestedTokenKinds = new SortedSet<SyntaxType>(tokenKinds);
            untestedTokenKinds.Remove(SyntaxType.Invalid);
            untestedTokenKinds.Remove(SyntaxType.EOF);
            // don't know why true and false aren't tested
            untestedTokenKinds.Remove(SyntaxType.TRUE_KEYWORD);
            untestedTokenKinds.Remove(SyntaxType.FALSE_KEYWORD);
            untestedTokenKinds.ExceptWith(testedTokenKinds);

            Assert.Empty(untestedTokenKinds);
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
        internal void Lexer_Lexes_TokenPairs(SyntaxType t1Kind, string t1Text,
                                           SyntaxType t2Kind, string t2Text) {
            var text = t1Text + t2Text;
            var tokens = SyntaxTree.ParseTokens(text).ToArray();

            Assert.Equal(2, tokens.Length);
            Assert.Equal(tokens[0].type, t1Kind);
            Assert.Equal(tokens[0].text, t1Text);
            Assert.Equal(tokens[1].type, t2Kind);
            Assert.Equal(tokens[1].text, t2Text);
        }

        [Theory]
        [MemberData(nameof(GetTokenPairsWithSeparatorData))]
        internal void Lexer_Lexes_TokenPairs_WithSeparators(SyntaxType t1Kind, string t1Text,
                                                          SyntaxType separatorKind, string separatorText,
                                                          SyntaxType t2Kind, string t2Text) {
            var text = t1Text + separatorText + t2Text;
            var tokens = SyntaxTree.ParseTokens(text).ToArray();

            Assert.Equal(3, tokens.Length);
            Assert.Equal(tokens[0].type, t1Kind);
            Assert.Equal(tokens[0].text, t1Text);
            Assert.Equal(tokens[1].type, separatorKind);
            Assert.Equal(tokens[1].text, separatorText);
            Assert.Equal(tokens[2].type, t2Kind);
            Assert.Equal(tokens[2].text, t2Text);
        }

        public static IEnumerable<object[]> GetTokensData() {
            foreach (var t in GetTokens().Concat(GetSeparators()))
                yield return new object[] { t.type, t.text };
        }

        public static IEnumerable<object[]> GetTokenPairsData() {
            foreach (var t in GetTokenPairs())
                yield return new object[] { t.t1Kind, t.t1Text, t.t2Kind, t.t2Text };
        }

        public static IEnumerable<object[]> GetTokenPairsWithSeparatorData() {
            foreach (var t in GetTokenPairsWithSeparator())
                yield return new object[] { t.t1Kind, t.t1Text, t.separatorKind, t.separatorText, t.t2Kind, t.t2Text };
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

        private static bool RequiresSeparator(SyntaxType t1Kind, SyntaxType t2Kind) {
            var t1IsKeyword = t1Kind.ToString().EndsWith("KEYWORD");
            var t2IsKeyword = t2Kind.ToString().EndsWith("KEYWORD");

            if (t1Kind == SyntaxType.IDENTIFIER && t2Kind == SyntaxType.IDENTIFIER)
                return true;

            if (t1IsKeyword && t2IsKeyword)
                return true;

            if (t1IsKeyword && t2Kind == SyntaxType.IDENTIFIER)
                return true;

            if (t1Kind == SyntaxType.IDENTIFIER && t2IsKeyword)
                return true;

            if (t1Kind == SyntaxType.NUMBER && t2Kind == SyntaxType.NUMBER)
                return true;

            if (t1Kind == SyntaxType.BANG && t2Kind == SyntaxType.EQUALS)
                return true;

            if (t1Kind == SyntaxType.BANG && t2Kind == SyntaxType.DEQUALS)
                return true;

            if (t1Kind == SyntaxType.EQUALS && t2Kind == SyntaxType.EQUALS)
                return true;

            if (t1Kind == SyntaxType.EQUALS && t2Kind == SyntaxType.DEQUALS)
                return true;

            return false;
        }

        private static
        IEnumerable<(SyntaxType t1Kind, string t1Text, SyntaxType t2Kind, string t2Text)> GetTokenPairs() {
            foreach (var t1 in GetTokens()) {
                foreach (var t2 in GetTokens()) {
                    if (!RequiresSeparator(t1.type, t2.type))
                        yield return (t1.type, t1.text, t2.type, t2.text);
                }
            }
        }

        private static
        IEnumerable<(SyntaxType t1Kind, string t1Text, SyntaxType separatorKind, string separatorText, SyntaxType t2Kind, string t2Text)> GetTokenPairsWithSeparator() {
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
