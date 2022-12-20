using System;
using System.Linq;
using System.Collections.Generic;
using Xunit;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.Tests.CodeAnalysis.Syntax;

public class LexerTests {
    [Fact]
    public void Lexer_Lexes_UnterminatedString() {
        const string text = "\"test";
        var tokens = SyntaxTree.ParseTokens(text, out var diagnostics);
        var token = Assert.Single(tokens);
        Assert.Equal(SyntaxType.StringLiteralToken, token.type);
        Assert.Equal(text, token.text);
        Assert.Equal(1, diagnostics.count);
        var diagnostic = diagnostics.Pop();
        Assert.Equal(0, diagnostic.location.span.start);
        Assert.Equal(1, diagnostic.location.span.length);
        Assert.Equal("unterminated string literal", diagnostic.message);
    }

    [Fact]
    public void Lexer_Covers_AllTokens() {
        var tokenTypes = Enum.GetValues(typeof(SyntaxType))
            .Cast<SyntaxType>()
            .Where(k => k.IsToken());

        var testedTokenTypes = GetTokens().Concat(GetSeparators()).Select(t => t.type);

        var untestedTokenTypes = new SortedSet<SyntaxType>(tokenTypes);
        untestedTokenTypes.Remove(SyntaxType.BadToken);
        untestedTokenTypes.Remove(SyntaxType.EndOfFileToken);
        untestedTokenTypes.Remove(SyntaxType.SingleLineCommentTrivia);
        untestedTokenTypes.Remove(SyntaxType.MultiLineCommentTrivia);
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
        Assert.Equal(SyntaxType.IdentifierToken, token.type);
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
            (SyntaxType.NumericLiteralToken, "1"),
            (SyntaxType.NumericLiteralToken, "123"),
            (SyntaxType.IdentifierToken, "a"),
            (SyntaxType.IdentifierToken, "abc"),
            (SyntaxType.StringLiteralToken, "\"Test\""),
            (SyntaxType.StringLiteralToken, "\"Te\"\"st\""),
        };

        return fixedTokens.Concat(dynamicTokens);
    }

    private static IEnumerable<(SyntaxType type, string text)> GetSeparators() {
        return new[] {
            (SyntaxType.WhitespaceTrivia, " "),
            (SyntaxType.WhitespaceTrivia, "  "),
            (SyntaxType.EndOfLineTrivia, "\r"),
            (SyntaxType.EndOfLineTrivia, "\n"),
            (SyntaxType.EndOfLineTrivia, "\r\n")
        };
    }

    private static bool RequiresSeparator(SyntaxType t1Type, SyntaxType t2Type) {
        var t1IsKeyword = t1Type.IsKeyword();
        var t2IsKeyword = t2Type.IsKeyword();

        if (t1Type == SyntaxType.IdentifierToken && t2Type == SyntaxType.IdentifierToken)
            return true;
        if (t1IsKeyword && t2IsKeyword)
            return true;
        if (t1IsKeyword && t2Type == SyntaxType.IdentifierToken)
            return true;
        if (t1Type == SyntaxType.IdentifierToken && t2IsKeyword)
            return true;
        if (t1Type == SyntaxType.NumericLiteralToken && t2Type == SyntaxType.NumericLiteralToken)
            return true;
        if (t1Type == SyntaxType.ExclamationToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.ExclamationToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.EqualsToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.EqualsToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.AsteriskToken && t2Type == SyntaxType.AsteriskToken)
            return true;
        if (t1Type == SyntaxType.AsteriskAsteriskToken && t2Type == SyntaxType.AsteriskToken)
            return true;
        if (t1Type == SyntaxType.AsteriskToken && t2Type == SyntaxType.AsteriskAsteriskToken)
            return true;
        if (t1Type == SyntaxType.AsteriskAsteriskToken && t2Type == SyntaxType.AsteriskAsteriskToken)
            return true;
        if (t1Type == SyntaxType.LessThanToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.LessThanToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.PipeToken && t2Type == SyntaxType.PipeToken)
            return true;
        if (t1Type == SyntaxType.PipeToken && t2Type == SyntaxType.PipePipeToken)
            return true;
        if (t1Type == SyntaxType.PipePipeToken && t2Type == SyntaxType.PipeToken)
            return true;
        if (t1Type == SyntaxType.AmpersandToken && t2Type == SyntaxType.AmpersandToken)
            return true;
        if (t1Type == SyntaxType.AmpersandToken && t2Type == SyntaxType.AmpersandAmpersandToken)
            return true;
        if (t1Type == SyntaxType.AmpersandAmpersandToken && t2Type == SyntaxType.AmpersandToken)
            return true;
        if (t1Type == SyntaxType.LessThanToken && t2Type == SyntaxType.LessThanEqualsToken)
            return true;
        if (t1Type == SyntaxType.LessThanToken && t2Type == SyntaxType.LessThanLessThanToken)
            return true;
        if (t1Type == SyntaxType.LessThanLessThanToken && t2Type == SyntaxType.LessThanEqualsToken)
            return true;
        if (t1Type == SyntaxType.LessThanToken && t2Type == SyntaxType.LessThanToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanToken && t2Type == SyntaxType.GreaterThanEqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanToken && t2Type == SyntaxType.GreaterThanGreaterThanToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanGreaterThanToken && t2Type == SyntaxType.GreaterThanEqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanToken && t2Type == SyntaxType.GreaterThanToken)
            return true;
        if (t1Type == SyntaxType.StringLiteralToken && t2Type == SyntaxType.StringLiteralToken)
            return true;
        if (t1Type == SyntaxType.SlashToken && t2Type == SyntaxType.SlashToken)
            return true;
        if (t1Type == SyntaxType.SlashToken && t2Type == SyntaxType.AsteriskToken)
            return true;
        if (t1Type == SyntaxType.SlashToken && t2Type == SyntaxType.AsteriskAsteriskToken)
            return true;
        if (t1Type == SyntaxType.SlashToken && t2Type == SyntaxType.MultiLineCommentTrivia)
            return true;
        if (t1Type == SyntaxType.SlashToken && t2Type == SyntaxType.SingleLineCommentTrivia)
            return true;
        if (t1Type == SyntaxType.SlashToken && t2Type == SyntaxType.AsteriskAsteriskEqualsToken)
            return true;
        if (t1Type == SyntaxType.IdentifierToken && t2Type == SyntaxType.NumericLiteralToken)
            return true;
        if (t1IsKeyword && t2Type == SyntaxType.NumericLiteralToken)
            return true;
        if (t1Type == SyntaxType.PlusToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.PlusToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.PlusToken && t2Type == SyntaxType.PlusToken)
            return true;
        if (t1Type == SyntaxType.PlusToken && t2Type == SyntaxType.PlusEqualsToken)
            return true;
        if (t1Type == SyntaxType.PlusToken && t2Type == SyntaxType.PlusPlusToken)
            return true;
        if (t1Type == SyntaxType.MinusToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.MinusToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.MinusToken && t2Type == SyntaxType.MinusToken)
            return true;
        if (t1Type == SyntaxType.MinusToken && t2Type == SyntaxType.MinusEqualsToken)
            return true;
        if (t1Type == SyntaxType.MinusToken && t2Type == SyntaxType.MinusMinusToken)
            return true;
        if (t1Type == SyntaxType.AsteriskToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.AsteriskToken && t2Type == SyntaxType.AsteriskEqualsToken)
            return true;
        if (t1Type == SyntaxType.AsteriskToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.SlashToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.SlashToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.AmpersandToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.AmpersandToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.AmpersandToken && t2Type == SyntaxType.AmpersandEqualsToken)
            return true;
        if (t1Type == SyntaxType.PipeToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.PipeToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.PipeToken && t2Type == SyntaxType.PipeEqualsToken)
            return true;
        if (t1Type == SyntaxType.CaretToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.CaretToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.SlashToken && t2Type == SyntaxType.SlashEqualsToken)
            return true;
        if (t1Type == SyntaxType.SlashToken && t2Type == SyntaxType.AsteriskEqualsToken)
            return true;
        if (t1Type == SyntaxType.AsteriskAsteriskToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.AsteriskAsteriskToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.AsteriskToken && t2Type == SyntaxType.AsteriskAsteriskEqualsToken)
            return true;
        if (t1Type == SyntaxType.LessThanLessThanToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanGreaterThanToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.LessThanLessThanToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanGreaterThanToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.LessThanToken && t2Type == SyntaxType.LessThanLessThanEqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanToken && t2Type == SyntaxType.GreaterThanGreaterThanEqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanToken && t2Type == SyntaxType.GreaterThanGreaterThanGreaterThanEqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanToken && t2Type == SyntaxType.GreaterThanGreaterThanGreaterThanToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanGreaterThanToken && t2Type ==
            SyntaxType.GreaterThanGreaterThanGreaterThanEqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanGreaterThanToken && t2Type ==
            SyntaxType.GreaterThanGreaterThanGreaterThanToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanGreaterThanToken && t2Type == SyntaxType.GreaterThanGreaterThanEqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanGreaterThanToken && t2Type == SyntaxType.GreaterThanEqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanGreaterThanToken && t2Type == SyntaxType.GreaterThanToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanGreaterThanToken && t2Type == SyntaxType.GreaterThanGreaterThanToken)
            return true;
        if (t1Type == SyntaxType.PercentToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.PercentToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanGreaterThanGreaterThanToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.GreaterThanGreaterThanGreaterThanToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.QuestionQuestionToken && t2Type == SyntaxType.EqualsToken)
            return true;
        if (t1Type == SyntaxType.QuestionQuestionToken && t2Type == SyntaxType.EqualsEqualsToken)
            return true;
        if (t1Type == SyntaxType.QuestionToken && t2Type == SyntaxType.QuestionToken)
            return true;
        if (t1Type == SyntaxType.QuestionToken && t2Type == SyntaxType.QuestionQuestionToken)
            return true;
        if (t1Type == SyntaxType.QuestionToken && t2Type == SyntaxType.QuestionQuestionEqualsToken)
            return true;
        if (t1Type == SyntaxType.NumericLiteralToken && t2Type == SyntaxType.PeriodToken)
            return true;

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
