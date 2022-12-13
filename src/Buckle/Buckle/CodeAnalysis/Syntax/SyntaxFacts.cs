using System;
using System.Collections.Generic;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Basic syntax facts references by the <see cref="Parser" /> and the <see cref="Lexer" />.
/// </summary>
internal static class SyntaxFacts {
    /// <summary>
    /// Gets binary operator precedence of a <see cref="SyntaxType" />.
    /// </summary>
    /// <param name="type"><see cref="SyntaxType" />.</param>
    /// <returns>Precedence.</returns>
    internal static int GetBinaryPrecedence(this SyntaxType type) {
        switch (type) {
            case SyntaxType.AsteriskAsteriskToken:
                return 14;
            case SyntaxType.AsteriskToken:
            case SyntaxType.SlashToken:
            case SyntaxType.PercentToken:
                return 13;
            case SyntaxType.PlusToken:
            case SyntaxType.MinusToken:
                return 12;
            case SyntaxType.LessThanLessThanToken:
            case SyntaxType.GreaterThanGreaterThanToken:
            case SyntaxType.GreaterThanGreaterThanGreaterThanToken:
                return 11;
            case SyntaxType.IsKeyword:
            case SyntaxType.IsntKeyword:
            case SyntaxType.LessThanToken:
            case SyntaxType.GreaterThanToken:
            case SyntaxType.LessThanEqualsToken:
            case SyntaxType.GreaterThanEqualsToken:
                return 10;
            case SyntaxType.EqualsEqualsToken:
            case SyntaxType.ExclamationEqualsToken:
                return 9;
            case SyntaxType.AmpersandToken:
                return 8;
            case SyntaxType.CaretToken:
                return 7;
            case SyntaxType.PipeToken:
                return 6;
            case SyntaxType.AmpersandAmpersandToken:
                return 5;
            case SyntaxType.PipePipeToken:
                return 4;
            case SyntaxType.QuestionQuestionToken:
                return 3;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Gets primary operator precedence of a <see cref="SyntaxType" />.
    /// </summary>
    /// <param name="type"><see cref="SyntaxType" />.</param>
    /// <returns>Precedence.</returns>
    internal static int GetPrimaryPrecedence(this SyntaxType type) {
        switch (type) {
            case SyntaxType.TypeOfKeyword:
            case SyntaxType.OpenBracketToken:
            case SyntaxType.OpenParenToken:
                return 18;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Gets unary operator precedence of a <see cref="SyntaxType" />.
    /// </summary>
    /// <param name="type"><see cref="SyntaxType" />.</param>
    /// <returns>Precedence.</returns>
    internal static int GetUnaryPrecedence(this SyntaxType type) {
        switch (type) {
            case SyntaxType.PlusPlusToken:
            case SyntaxType.MinusMinusToken:
            case SyntaxType.PlusToken:
            case SyntaxType.MinusToken:
            case SyntaxType.ExclamationToken:
            case SyntaxType.TildeToken:
                return 17;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Attempts to get a <see cref="SyntaxType" /> from a text representation of a keyword.
    /// </summary>
    /// <param name="text">Text representation.</param>
    /// <returns>Keyword type, defaults to identifer if failed.</returns>
    internal static SyntaxType GetKeywordType(string text) {
        switch (text) {
            case "true":
                return SyntaxType.TrueKeyword;
            case "false":
                return SyntaxType.FalseKeyword;
            case "null":
                return SyntaxType.NullKeyword;
            case "var":
                return SyntaxType.VarKeyword;
            case "const":
                return SyntaxType.ConstKeyword;
            case "ref":
                return SyntaxType.RefKeyword;
            case "if":
                return SyntaxType.IfKeyword;
            case "else":
                return SyntaxType.ElseKeyword;
            case "while":
                return SyntaxType.WhileKeyword;
            case "for":
                return SyntaxType.ForKeyword;
            case "do":
                return SyntaxType.DoKeyword;
            case "break":
                return SyntaxType.BreakKeyword;
            case "continue":
                return SyntaxType.ContinueKeyword;
            case "try":
                return SyntaxType.TryKeyword;
            case "catch":
                return SyntaxType.CatchKeyword;
            case "finally":
                return SyntaxType.FinallyKeyword;
            case "return":
                return SyntaxType.ReturnKeyword;
            case "is":
                return SyntaxType.IsKeyword;
            case "isnt":
                return SyntaxType.IsntKeyword;
            case "typeof":
                return SyntaxType.TypeOfKeyword;
            case "struct":
                return SyntaxType.StructKeyword;
            default:
                return SyntaxType.IdentifierToken;
        }
    }

    /// <summary>
    /// Gets text representation of a <see cref="Token" /> or keyword.
    /// </summary>
    /// <param name="type"><see cref="SyntaxType" />.</param>
    /// <returns>Text representation, default to null if not text representation exists.</returns>
    internal static string GetText(SyntaxType type) {
        switch (type) {
            case SyntaxType.CommaToken:
                return ",";
            case SyntaxType.PlusToken:
                return "+";
            case SyntaxType.MinusToken:
                return "-";
            case SyntaxType.AsteriskToken:
                return "*";
            case SyntaxType.SlashToken:
                return "/";
            case SyntaxType.OpenParenToken:
                return "(";
            case SyntaxType.CloseParenToken:
                return ")";
            case SyntaxType.EqualsToken:
                return "=";
            case SyntaxType.TildeToken:
                return "~";
            case SyntaxType.CaretToken:
                return "^";
            case SyntaxType.AmpersandToken:
                return "&";
            case SyntaxType.PipeToken:
                return "|";
            case SyntaxType.LessThanLessThanToken:
                return "<<";
            case SyntaxType.GreaterThanGreaterThanToken:
                return ">>";
            case SyntaxType.GreaterThanGreaterThanGreaterThanToken:
                return ">>>";
            case SyntaxType.ExclamationToken:
                return "!";
            case SyntaxType.AmpersandAmpersandToken:
                return "&&";
            case SyntaxType.PipePipeToken:
                return "||";
            case SyntaxType.AsteriskAsteriskToken:
                return "**";
            case SyntaxType.PlusPlusToken:
                return "++";
            case SyntaxType.MinusMinusToken:
                return "--";
            case SyntaxType.OpenBraceToken:
                return "{";
            case SyntaxType.CloseBraceToken:
                return "}";
            case SyntaxType.OpenBracketToken:
                return "[";
            case SyntaxType.CloseBracketToken:
                return "]";
            case SyntaxType.SemicolonToken:
                return ";";
            case SyntaxType.EqualsEqualsToken:
                return "==";
            case SyntaxType.ExclamationEqualsToken:
                return "!=";
            case SyntaxType.LessThanToken:
                return "<";
            case SyntaxType.GreaterThanToken:
                return ">";
            case SyntaxType.PercentToken:
                return "%";
            case SyntaxType.QuestionQuestionToken:
                return "??";
            case SyntaxType.LessThanEqualsToken:
                return "<=";
            case SyntaxType.GreaterThanEqualsToken:
                return ">=";
            case SyntaxType.AmpersandEqualsToken:
                return "&=";
            case SyntaxType.PipeEqualsToken:
                return "|=";
            case SyntaxType.CaretEqualsToken:
                return "^=";
            case SyntaxType.PlusEqualsToken:
                return "+=";
            case SyntaxType.MinusEqualsToken:
                return "-=";
            case SyntaxType.SlashEqualsToken:
                return "/=";
            case SyntaxType.AsteriskEqualsToken:
                return "*=";
            case SyntaxType.AsteriskAsteriskEqualsToken:
                return "**=";
            case SyntaxType.GreaterThanGreaterThanEqualsToken:
                return ">>=";
            case SyntaxType.GreaterThanGreaterThanGreaterThanEqualsToken:
                return ">>>=";
            case SyntaxType.LessThanLessThanEqualsToken:
                return "<<=";
            case SyntaxType.PercentEqualsToken:
                return "%=";
            case SyntaxType.QuestionQuestionEqualsToken:
                return "??=";
            case SyntaxType.TrueKeyword:
                return "true";
            case SyntaxType.FalseKeyword:
                return "false";
            case SyntaxType.NullKeyword:
                return "null";
            case SyntaxType.VarKeyword:
                return "var";
            case SyntaxType.ConstKeyword:
                return "const";
            case SyntaxType.RefKeyword:
                return "ref";
            case SyntaxType.IfKeyword:
                return "if";
            case SyntaxType.ElseKeyword:
                return "else";
            case SyntaxType.WhileKeyword:
                return "while";
            case SyntaxType.ForKeyword:
                return "for";
            case SyntaxType.DoKeyword:
                return "do";
            case SyntaxType.BreakKeyword:
                return "break";
            case SyntaxType.ContinueKeyword:
                return "continue";
            case SyntaxType.TryKeyword:
                return "try";
            case SyntaxType.CatchKeyword:
                return "catch";
            case SyntaxType.FinallyKeyword:
                return "finally";
            case SyntaxType.ReturnKeyword:
                return "return";
            case SyntaxType.IsKeyword:
                return "is";
            case SyntaxType.IsntKeyword:
                return "isnt";
            case SyntaxType.TypeOfKeyword:
                return "typeof";
            case SyntaxType.StructKeyword:
                return "struct";
            default:
                return null;
        }
    }

    /// <summary>
    /// Gets base operator type of assignment operator type (e.g. += -> +).
    /// </summary>
    /// <param name="type"><see cref="SyntaxType" />.</param>
    /// <returns>Binary operator type.</returns>
    internal static SyntaxType GetBinaryOperatorOfAssignmentOperator(SyntaxType type) {
        switch (type) {
            case SyntaxType.PlusEqualsToken:
                return SyntaxType.PlusToken;
            case SyntaxType.MinusEqualsToken:
                return SyntaxType.MinusToken;
            case SyntaxType.AsteriskEqualsToken:
                return SyntaxType.AsteriskToken;
            case SyntaxType.SlashEqualsToken:
                return SyntaxType.SlashToken;
            case SyntaxType.AmpersandEqualsToken:
                return SyntaxType.AmpersandToken;
            case SyntaxType.PipeEqualsToken:
                return SyntaxType.PipeToken;
            case SyntaxType.CaretEqualsToken:
                return SyntaxType.CaretToken;
            case SyntaxType.AsteriskAsteriskEqualsToken:
                return SyntaxType.AsteriskAsteriskToken;
            case SyntaxType.LessThanLessThanEqualsToken:
                return SyntaxType.LessThanLessThanToken;
            case SyntaxType.GreaterThanGreaterThanEqualsToken:
                return SyntaxType.GreaterThanGreaterThanToken;
            case SyntaxType.GreaterThanGreaterThanGreaterThanEqualsToken:
                return SyntaxType.GreaterThanGreaterThanGreaterThanToken;
            case SyntaxType.PercentEqualsToken:
                return SyntaxType.PercentToken;
            case SyntaxType.QuestionQuestionEqualsToken:
                return SyntaxType.QuestionQuestionToken;
            default:
                throw new Exception($"GetBinaryOperatorOfAssignmentOperator: unexpected syntax '{type}'");
        }
    }

    /// <summary>
    /// Gets all unary operator types.
    /// </summary>
    /// <returns>Unary operator types (calling code should not depend on order).</returns>
    internal static IEnumerable<SyntaxType> GetUnaryOperatorTypes() {
        var types = (SyntaxType[])Enum.GetValues(typeof(SyntaxType));
        foreach (var type in types) {
            if (GetUnaryPrecedence(type) > 0)
                yield return type;
        }
    }

    /// <summary>
    /// Gets all binary operator types.
    /// </summary>
    /// <returns>Binary operator types (calling code should not depend on order).</returns>
    internal static IEnumerable<SyntaxType> GetBinaryOperatorTypes() {
        var types = (SyntaxType[])Enum.GetValues(typeof(SyntaxType));
        foreach (var type in types) {
            if (GetBinaryPrecedence(type) > 0)
                yield return type;
        }
    }

    /// <summary>
    /// Checks if a <see cref="SyntaxType" /> is a keyword.
    /// </summary>
    /// <param name="type"><see cref="SyntaxType" />.</param>
    /// <returns>If the <see cref="SyntaxType" /> is a keyword.</returns>
    internal static bool IsKeyword(this SyntaxType type) {
        return type.ToString().EndsWith("Keyword");
    }

    /// <summary>
    /// Checks if a <see cref="SyntaxType" /> is a <see cref="Token" />.
    /// </summary>
    /// <param name="type"><see cref="SyntaxType" />.</param>
    /// <returns>If the <see cref="SyntaxType" /> is a token.</returns>
    internal static bool IsToken(this SyntaxType type) {
        return !type.IsTrivia() && (type.IsKeyword() || type.ToString().EndsWith("Token"));
    }

    /// <summary>
    /// Checks if a <see cref="SyntaxType" /> is trivia.
    /// </summary>
    /// <param name="type"><see cref="SyntaxType" />.</param>
    /// <returns>If the <see cref="SyntaxType" /> is trivia.</returns>
    internal static bool IsTrivia(this SyntaxType type) {
        return type.ToString().EndsWith("Trivia");
    }

    /// <summary>
    /// Checks if a <see cref="SyntaxType" /> is a comment.
    /// </summary>
    /// <param name="type"><see cref="SyntaxType" />.</param>
    /// <returns>If the <see cref="SyntaxType" /> is a comment.</returns>
    internal static bool IsComment(this SyntaxType type) {
        return type == SyntaxType.SingleLineCommentTrivia || type == SyntaxType.MultiLineCommentTrivia;
    }
}
