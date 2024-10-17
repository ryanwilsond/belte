using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Xunit;

namespace Buckle.Tests.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Tests on the <see cref="Buckle.CodeAnalysis.Syntax.InternalSyntax.Lexer" /> class.
/// </summary>
public sealed class LexerTests {
    [Fact]
    public void Lexer_Lexes_UnterminatedString() {
        var text = "\"test";
        var tokens = SyntaxTreeExtensions.ParseTokens(text);
        Assert.Equal(1, tokens.Count);
        var token = tokens[0];
        Assert.Equal(SyntaxKind.StringLiteralToken, token.kind);
        Assert.Equal(text, token.text);
        Assert.True(token.containsDiagnostics);
        var diagnostic = (SyntaxDiagnostic)token.GetDiagnostics()[0];
        Assert.Equal(0, diagnostic.offset);
        Assert.Equal(1, diagnostic.width);
        Assert.Equal("unterminated string literal", diagnostic.message);
    }

    [Fact]
    public void Lexer_Covers_AllTokens() {
        var tokenTypes = Enum.GetValues(typeof(SyntaxKind))
            .Cast<SyntaxKind>()
            .Where(k => k.IsToken());

        var testedTokenTypes = GetTokens().Concat(GetSeparators()).Select(t => t.kind);

        var untestedTokenTypes = new SortedSet<SyntaxKind>(tokenTypes);
        untestedTokenTypes.Remove(SyntaxKind.BadToken);
        untestedTokenTypes.Remove(SyntaxKind.EndOfFileToken);
        untestedTokenTypes.Remove(SyntaxKind.SingleLineCommentTrivia);
        untestedTokenTypes.Remove(SyntaxKind.MultiLineCommentTrivia);
        untestedTokenTypes.Remove(SyntaxKind.GreaterThanGreaterThanToken);
        untestedTokenTypes.Remove(SyntaxKind.GreaterThanGreaterThanGreaterThanToken);
        untestedTokenTypes.Remove(SyntaxKind.HashToken);
        untestedTokenTypes.Remove(SyntaxKind.EndOfDirectiveToken);
        untestedTokenTypes.ExceptWith(testedTokenTypes);

        Assert.Empty(untestedTokenTypes);
    }

    [Theory]
    [MemberData(nameof(GetTokensData))]
    internal void Lexer_Lexes_Token(SyntaxKind kind, string text) {
        var tokens = SyntaxTreeExtensions.ParseTokens(text);

        Assert.Equal(1, tokens.Count);
        var token = tokens[0];
        Assert.Equal(kind, token.kind);
        Assert.Equal(text, token.text);
    }

    [Theory]
    [MemberData(nameof(GetSeparatorsData))]
    internal void Lexer_Lexes_Separator(SyntaxKind kind, string text) {
        var tokens = SyntaxTreeExtensions.ParseTokens(text, true);

        Assert.Equal(1, tokens.Count);
        var token = tokens[0];
        Assert.Equal(1, token.leadingTrivia.Count);
        var trivia = (Buckle.CodeAnalysis.Syntax.InternalSyntax.SyntaxTrivia)token.leadingTrivia[0];
        Assert.Equal(kind, trivia.kind);
        Assert.Equal(text, trivia.text);
    }

    [Theory]
    [MemberData(nameof(GetTokenPairsData))]
    internal void Lexer_Lexes_TokenPairs(SyntaxKind t1Kind, string t1Text,
                                        SyntaxKind t2Kind, string t2Text) {
        var text = t1Text + t2Text;
        var tokens = SyntaxTreeExtensions.ParseTokens(text);

        Assert.Equal(2, tokens.Count);
        Assert.Equal(tokens[0].kind, t1Kind);
        Assert.Equal(tokens[0].text, t1Text);
        Assert.Equal(tokens[1].kind, t2Kind);
        Assert.Equal(tokens[1].text, t2Text);
    }

    [Theory]
    [MemberData(nameof(GetTokenPairsWithSeparatorData))]
    internal void Lexer_Lexes_TokenPairs_WithSeparators(SyntaxKind t1Kind, string t1Text,
                                                        SyntaxKind separatorKind, string separatorText,
                                                        SyntaxKind t2Kind, string t2Text) {
        var text = t1Text + separatorText + t2Text;
        var tokens = SyntaxTreeExtensions.ParseTokens(text);

        Assert.Equal(2, tokens.Count);
        Assert.Equal(tokens[0].kind, t1Kind);
        Assert.Equal(tokens[0].text, t1Text);

        Assert.Equal(1, tokens[0].trailingTrivia.Count);
        var separator = (Buckle.CodeAnalysis.Syntax.InternalSyntax.SyntaxTrivia)tokens[0].trailingTrivia[0];
        Assert.Equal(separator.text, separatorText);
        Assert.Equal(separator.kind, separatorKind);

        Assert.Equal(tokens[1].kind, t2Kind);
        Assert.Equal(tokens[1].text, t2Text);
    }

    [Theory]
    [InlineData("foo")]
    [InlineData("foo42")]
    [InlineData("foo_42")]
    [InlineData("_foo")]
    public void Lexer_Lexes_Identifiers(string name) {
        var tokens = SyntaxTreeExtensions.ParseTokens(name);

        Assert.Equal(1, tokens.Count);

        var token = tokens[0];
        Assert.Equal(SyntaxKind.IdentifierToken, token.kind);
        Assert.Equal(name, token.text);
    }

    public static IEnumerable<object[]> GetTokensData() {
        foreach (var t in GetTokens())
            yield return new object[] { t.kind, t.text };
    }

    public static IEnumerable<object[]> GetSeparatorsData() {
        foreach (var t in GetSeparators())
            yield return new object[] { t.kind, t.text };
    }

    public static IEnumerable<object[]> GetTokenPairsData() {
        foreach (var t in GetTokenPairs())
            yield return new object[] { t.t1Kind, t.t1Text, t.t2Kind, t.t2Text };
    }

    public static IEnumerable<object[]> GetTokenPairsWithSeparatorData() {
        foreach (var t in GetTokenPairsWithSeparator())
            yield return new object[] { t.t1Kind, t.t1Text, t.separatorKind, t.separatorText, t.t2Kind, t.t2Text };
    }

    private static IEnumerable<(SyntaxKind kind, string text)> GetTokens() {
        var fixedTokens = Enum.GetValues(typeof(SyntaxKind))
            .Cast<SyntaxKind>()
            .Where(k => k is not SyntaxKind.GreaterThanGreaterThanToken
                         and not SyntaxKind.GreaterThanGreaterThanGreaterThanToken
                         and not SyntaxKind.HashToken)
            .Select(k => (kind: k, text: SyntaxFacts.GetText(k)))
            .Where(t => t.text is not null);

        var dynamicTokens = new[] {
            (SyntaxKind.NumericLiteralToken, "1"),
            (SyntaxKind.NumericLiteralToken, "123"),
            (SyntaxKind.IdentifierToken, "a"),
            (SyntaxKind.IdentifierToken, "abc"),
            (SyntaxKind.StringLiteralToken, "\"Test\""),
            (SyntaxKind.StringLiteralToken, "\"Te\"\"st\""),
            (SyntaxKind.CharacterLiteralToken, "\'H\'")
        };

        return fixedTokens.Concat(dynamicTokens);
    }

    private static IEnumerable<(SyntaxKind kind, string text)> GetSeparators() {
        return new[] {
            (SyntaxKind.WhitespaceTrivia, " "),
            (SyntaxKind.WhitespaceTrivia, "  "),
            (SyntaxKind.EndOfLineTrivia, "\r"),
            (SyntaxKind.EndOfLineTrivia, "\n"),
            (SyntaxKind.EndOfLineTrivia, "\r\n")
        };
    }

    private static bool RequiresSeparator(SyntaxKind t1Kind, SyntaxKind t2Kind) {
        var t1IsKeyword = t1Kind.IsKeyword();
        var t2IsKeyword = t2Kind.IsKeyword();

        if (t1Kind == SyntaxKind.IdentifierToken && t2Kind == SyntaxKind.IdentifierToken)
            return true;
        if (t1IsKeyword && t2IsKeyword)
            return true;
        if (t1IsKeyword && t2Kind == SyntaxKind.IdentifierToken)
            return true;
        if (t1Kind == SyntaxKind.IdentifierToken && t2IsKeyword)
            return true;
        if (t1Kind == SyntaxKind.NumericLiteralToken && t2Kind == SyntaxKind.NumericLiteralToken)
            return true;
        if (t1Kind == SyntaxKind.ExclamationToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.ExclamationToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.EqualsToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.EqualsToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.AsteriskToken && t2Kind == SyntaxKind.AsteriskToken)
            return true;
        if (t1Kind == SyntaxKind.AsteriskAsteriskToken && t2Kind == SyntaxKind.AsteriskToken)
            return true;
        if (t1Kind == SyntaxKind.AsteriskToken && t2Kind == SyntaxKind.AsteriskAsteriskToken)
            return true;
        if (t1Kind == SyntaxKind.AsteriskAsteriskToken && t2Kind == SyntaxKind.AsteriskAsteriskToken)
            return true;
        if (t1Kind == SyntaxKind.LessThanToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.LessThanToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.GreaterThanToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.GreaterThanToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.PipeToken)
            return true;
        if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.PipePipeToken)
            return true;
        if (t1Kind == SyntaxKind.PipePipeToken && t2Kind == SyntaxKind.PipeToken)
            return true;
        if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.AmpersandToken)
            return true;
        if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.AmpersandAmpersandToken)
            return true;
        if (t1Kind == SyntaxKind.AmpersandAmpersandToken && t2Kind == SyntaxKind.AmpersandToken)
            return true;
        if (t1Kind == SyntaxKind.LessThanToken && t2Kind == SyntaxKind.LessThanEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.LessThanToken && t2Kind == SyntaxKind.LessThanLessThanToken)
            return true;
        if (t1Kind == SyntaxKind.LessThanLessThanToken && t2Kind == SyntaxKind.LessThanEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.LessThanToken && t2Kind == SyntaxKind.LessThanToken)
            return true;
        if (t1Kind == SyntaxKind.GreaterThanToken && t2Kind == SyntaxKind.GreaterThanEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.StringLiteralToken && t2Kind == SyntaxKind.StringLiteralToken)
            return true;
        if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.SlashToken)
            return true;
        if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.AsteriskToken)
            return true;
        if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.AsteriskAsteriskToken)
            return true;
        if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.MultiLineCommentTrivia)
            return true;
        if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.SingleLineCommentTrivia)
            return true;
        if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.AsteriskAsteriskEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.IdentifierToken && t2Kind == SyntaxKind.NumericLiteralToken)
            return true;
        if (t1IsKeyword && t2Kind == SyntaxKind.NumericLiteralToken)
            return true;
        if (t1Kind == SyntaxKind.PlusToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.PlusToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.PlusToken && t2Kind == SyntaxKind.PlusToken)
            return true;
        if (t1Kind == SyntaxKind.PlusToken && t2Kind == SyntaxKind.PlusEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.PlusToken && t2Kind == SyntaxKind.PlusPlusToken)
            return true;
        if (t1Kind == SyntaxKind.MinusToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.MinusToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.MinusToken && t2Kind == SyntaxKind.MinusToken)
            return true;
        if (t1Kind == SyntaxKind.MinusToken && t2Kind == SyntaxKind.MinusEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.MinusToken && t2Kind == SyntaxKind.MinusMinusToken)
            return true;
        if (t1Kind == SyntaxKind.AsteriskToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.AsteriskToken && t2Kind == SyntaxKind.AsteriskEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.AsteriskToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.AmpersandToken && t2Kind == SyntaxKind.AmpersandEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.PipeToken && t2Kind == SyntaxKind.PipeEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.CaretToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.CaretToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.SlashEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.SlashToken && t2Kind == SyntaxKind.AsteriskEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.AsteriskAsteriskToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.AsteriskAsteriskToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.AsteriskToken && t2Kind == SyntaxKind.AsteriskAsteriskEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.LessThanLessThanToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.LessThanLessThanToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.LessThanToken && t2Kind == SyntaxKind.LessThanLessThanEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.GreaterThanToken && t2Kind == SyntaxKind.GreaterThanGreaterThanEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.GreaterThanToken && t2Kind == SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.PercentToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.PercentToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.QuestionQuestionToken && t2Kind == SyntaxKind.EqualsToken)
            return true;
        if (t1Kind == SyntaxKind.QuestionQuestionToken && t2Kind == SyntaxKind.EqualsEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.QuestionToken && t2Kind == SyntaxKind.QuestionToken)
            return true;
        if (t1Kind == SyntaxKind.QuestionToken && t2Kind == SyntaxKind.QuestionQuestionToken)
            return true;
        if (t1Kind == SyntaxKind.QuestionToken && t2Kind == SyntaxKind.QuestionQuestionEqualsToken)
            return true;
        if (t1Kind == SyntaxKind.QuestionToken && t2Kind == SyntaxKind.PeriodToken)
            return true;
        if (t1Kind == SyntaxKind.QuestionToken && t2Kind == SyntaxKind.QuestionPeriodToken)
            return true;
        if (t1Kind == SyntaxKind.QuestionToken && t2Kind == SyntaxKind.OpenBracketToken)
            return true;
        if (t1Kind == SyntaxKind.QuestionToken && t2Kind == SyntaxKind.QuestionOpenBracketToken)
            return true;
        if (t1Kind == SyntaxKind.NumericLiteralToken && t2Kind == SyntaxKind.PeriodToken)
            return true;

        return false;
    }

    private static
    IEnumerable<(SyntaxKind t1Kind, string t1Text, SyntaxKind t2Kind, string t2Text)> GetTokenPairs() {
        foreach (var t1 in GetTokens()) {
            foreach (var t2 in GetTokens()) {
                if (!RequiresSeparator(t1.kind, t2.kind))
                    yield return (t1.kind, t1.text, t2.kind, t2.text);
            }
        }
    }

    private static
        IEnumerable<(SyntaxKind t1Kind, string t1Text, SyntaxKind separatorKind,
        string separatorText, SyntaxKind t2Kind, string t2Text)>
        GetTokenPairsWithSeparator() {
        foreach (var t1 in GetTokens()) {
            foreach (var t2 in GetTokens()) {
                if (RequiresSeparator(t1.kind, t2.kind)) {
                    foreach (var s in GetSeparators())
                        yield return (t1.kind, t1.text, s.kind, s.text, t2.kind, t2.text);
                }
            }
        }
    }
}
