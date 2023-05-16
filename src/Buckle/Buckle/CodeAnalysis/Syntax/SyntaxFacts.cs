using System;
using System.Collections.Generic;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Basic syntax facts references by the <see cref="InternalSyntax.Parser" /> and the
/// <see cref="InternalSyntax.Lexer" />.
/// </summary>
internal static class SyntaxFacts {
    /// <summary>
    /// Gets binary operator precedence of a <see cref="SyntaxKind" />.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>Precedence, or 0 if <paramref name="type" /> is not a binary operator.</returns>
    internal static int GetBinaryPrecedence(this SyntaxKind type) {
        switch (type) {
            case SyntaxKind.AsteriskAsteriskToken:
                return 14;
            case SyntaxKind.AsteriskToken:
            case SyntaxKind.SlashToken:
            case SyntaxKind.PercentToken:
                return 13;
            case SyntaxKind.PlusToken:
            case SyntaxKind.MinusToken:
                return 12;
            case SyntaxKind.LessThanLessThanToken:
            case SyntaxKind.GreaterThanGreaterThanToken:
            case SyntaxKind.GreaterThanGreaterThanGreaterThanToken:
                return 11;
            case SyntaxKind.IsKeyword:
            case SyntaxKind.IsntKeyword:
            case SyntaxKind.LessThanToken:
            case SyntaxKind.GreaterThanToken:
            case SyntaxKind.LessThanEqualsToken:
            case SyntaxKind.GreaterThanEqualsToken:
                return 10;
            case SyntaxKind.EqualsEqualsToken:
            case SyntaxKind.ExclamationEqualsToken:
                return 9;
            case SyntaxKind.AmpersandToken:
                return 8;
            case SyntaxKind.CaretToken:
                return 7;
            case SyntaxKind.PipeToken:
                return 6;
            case SyntaxKind.AmpersandAmpersandToken:
                return 5;
            case SyntaxKind.PipePipeToken:
                return 4;
            case SyntaxKind.QuestionQuestionToken:
                return 3;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Gets primary operator precedence of a <see cref="SyntaxKind" />.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>Precedence, or 0 if <paramref name="type" /> is not a primary operator.</returns>
    internal static int GetPrimaryPrecedence(this SyntaxKind type) {
        switch (type) {
            case SyntaxKind.TypeOfKeyword:
            case SyntaxKind.OpenBracketToken:
            case SyntaxKind.OpenParenToken:
            case SyntaxKind.PeriodToken:
            case SyntaxKind.QuestionPeriodToken:
            case SyntaxKind.QuestionOpenBracketToken:
            case SyntaxKind.PlusPlusToken:
            case SyntaxKind.MinusMinusToken:
            case SyntaxKind.ExclamationToken:
                return 18;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Gets unary operator precedence of a <see cref="SyntaxKind" />.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>Precedence, or 0 if <paramref name="type" /> is not a unary operator.</returns>
    internal static int GetUnaryPrecedence(this SyntaxKind type) {
        switch (type) {
            case SyntaxKind.PlusPlusToken:
            case SyntaxKind.MinusMinusToken:
            case SyntaxKind.PlusToken:
            case SyntaxKind.MinusToken:
            case SyntaxKind.ExclamationToken:
            case SyntaxKind.TildeToken:
                return 17;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Gets ternary operator precedence of a <see cref="SyntaxKind" />.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>Precedence, or 0 if <paramref name="type" /> is not a ternary operator.</returns>
    internal static int GetTernaryPrecedence(this SyntaxKind type) {
        switch (type) {
            case SyntaxKind.QuestionToken:
                return 2;
            default:
                return 0;
        }
    }

    /// <summary>
    /// Gets the right operator associated with the given left operator in a ternary operator.
    /// E.g.
    /// c ? t : f
    ///   ^----------- If given the ? token (left operator),
    ///       ^------- will return the : token (right operator).
    /// </summary>
    /// <param name="type">Left operator of the ternary operator.</param>
    /// <returns>Associated right operator, throws if given an unknown right operator.</returns>
    internal static SyntaxKind GetTernaryOperatorPair(this SyntaxKind type) {
        switch (type) {
            case SyntaxKind.QuestionToken:
                return SyntaxKind.ColonToken;
            default:
                throw new BelteInternalException($"GetTernaryOperatorPair: unknown right operator '{type}'");
        }
    }

    /// <summary>
    /// Attempts to get a <see cref="SyntaxKind" /> from a text representation of a keyword.
    /// </summary>
    /// <param name="text">Text representation.</param>
    /// <returns>Keyword kind, defaults to identifer if failed.</returns>
    internal static SyntaxKind GetKeywordType(string text) {
        switch (text) {
            case "true":
                return SyntaxKind.TrueKeyword;
            case "false":
                return SyntaxKind.FalseKeyword;
            case "null":
                return SyntaxKind.NullKeyword;
            case "var":
                return SyntaxKind.VarKeyword;
            case "const":
                return SyntaxKind.ConstKeyword;
            case "ref":
                return SyntaxKind.RefKeyword;
            case "if":
                return SyntaxKind.IfKeyword;
            case "else":
                return SyntaxKind.ElseKeyword;
            case "while":
                return SyntaxKind.WhileKeyword;
            case "for":
                return SyntaxKind.ForKeyword;
            case "do":
                return SyntaxKind.DoKeyword;
            case "break":
                return SyntaxKind.BreakKeyword;
            case "continue":
                return SyntaxKind.ContinueKeyword;
            case "try":
                return SyntaxKind.TryKeyword;
            case "catch":
                return SyntaxKind.CatchKeyword;
            case "finally":
                return SyntaxKind.FinallyKeyword;
            case "return":
                return SyntaxKind.ReturnKeyword;
            case "is":
                return SyntaxKind.IsKeyword;
            case "isnt":
                return SyntaxKind.IsntKeyword;
            case "typeof":
                return SyntaxKind.TypeOfKeyword;
            case "struct":
                return SyntaxKind.StructKeyword;
            case "class":
                return SyntaxKind.ClassKeyword;
            default:
                return SyntaxKind.IdentifierToken;
        }
    }

    /// <summary>
    /// Gets text representation of a <see cref="SyntaxToken" /> or keyword.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>Text representation, default to null if not text representation exists.</returns>
    internal static string GetText(SyntaxKind type) {
        switch (type) {
            case SyntaxKind.PeriodToken:
                return ".";
            case SyntaxKind.CommaToken:
                return ",";
            case SyntaxKind.PlusToken:
                return "+";
            case SyntaxKind.MinusToken:
                return "-";
            case SyntaxKind.AsteriskToken:
                return "*";
            case SyntaxKind.SlashToken:
                return "/";
            case SyntaxKind.OpenParenToken:
                return "(";
            case SyntaxKind.CloseParenToken:
                return ")";
            case SyntaxKind.EqualsToken:
                return "=";
            case SyntaxKind.TildeToken:
                return "~";
            case SyntaxKind.CaretToken:
                return "^";
            case SyntaxKind.AmpersandToken:
                return "&";
            case SyntaxKind.PipeToken:
                return "|";
            case SyntaxKind.LessThanLessThanToken:
                return "<<";
            case SyntaxKind.GreaterThanGreaterThanToken:
                return ">>";
            case SyntaxKind.GreaterThanGreaterThanGreaterThanToken:
                return ">>>";
            case SyntaxKind.ExclamationToken:
                return "!";
            case SyntaxKind.AmpersandAmpersandToken:
                return "&&";
            case SyntaxKind.PipePipeToken:
                return "||";
            case SyntaxKind.AsteriskAsteriskToken:
                return "**";
            case SyntaxKind.PlusPlusToken:
                return "++";
            case SyntaxKind.MinusMinusToken:
                return "--";
            case SyntaxKind.OpenBraceToken:
                return "{";
            case SyntaxKind.CloseBraceToken:
                return "}";
            case SyntaxKind.OpenBracketToken:
                return "[";
            case SyntaxKind.CloseBracketToken:
                return "]";
            case SyntaxKind.SemicolonToken:
                return ";";
            case SyntaxKind.ColonToken:
                return ":";
            case SyntaxKind.QuestionToken:
                return "?";
            case SyntaxKind.EqualsEqualsToken:
                return "==";
            case SyntaxKind.ExclamationEqualsToken:
                return "!=";
            case SyntaxKind.LessThanToken:
                return "<";
            case SyntaxKind.GreaterThanToken:
                return ">";
            case SyntaxKind.PercentToken:
                return "%";
            case SyntaxKind.QuestionQuestionToken:
                return "??";
            case SyntaxKind.LessThanEqualsToken:
                return "<=";
            case SyntaxKind.GreaterThanEqualsToken:
                return ">=";
            case SyntaxKind.AmpersandEqualsToken:
                return "&=";
            case SyntaxKind.PipeEqualsToken:
                return "|=";
            case SyntaxKind.CaretEqualsToken:
                return "^=";
            case SyntaxKind.PlusEqualsToken:
                return "+=";
            case SyntaxKind.MinusEqualsToken:
                return "-=";
            case SyntaxKind.SlashEqualsToken:
                return "/=";
            case SyntaxKind.AsteriskEqualsToken:
                return "*=";
            case SyntaxKind.AsteriskAsteriskEqualsToken:
                return "**=";
            case SyntaxKind.GreaterThanGreaterThanEqualsToken:
                return ">>=";
            case SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken:
                return ">>>=";
            case SyntaxKind.LessThanLessThanEqualsToken:
                return "<<=";
            case SyntaxKind.PercentEqualsToken:
                return "%=";
            case SyntaxKind.QuestionQuestionEqualsToken:
                return "??=";
            case SyntaxKind.QuestionPeriodToken:
                return "?.";
            case SyntaxKind.QuestionOpenBracketToken:
                return "?[";
            case SyntaxKind.TrueKeyword:
                return "true";
            case SyntaxKind.FalseKeyword:
                return "false";
            case SyntaxKind.NullKeyword:
                return "null";
            case SyntaxKind.VarKeyword:
                return "var";
            case SyntaxKind.ConstKeyword:
                return "const";
            case SyntaxKind.RefKeyword:
                return "ref";
            case SyntaxKind.IfKeyword:
                return "if";
            case SyntaxKind.ElseKeyword:
                return "else";
            case SyntaxKind.WhileKeyword:
                return "while";
            case SyntaxKind.ForKeyword:
                return "for";
            case SyntaxKind.DoKeyword:
                return "do";
            case SyntaxKind.BreakKeyword:
                return "break";
            case SyntaxKind.ContinueKeyword:
                return "continue";
            case SyntaxKind.TryKeyword:
                return "try";
            case SyntaxKind.CatchKeyword:
                return "catch";
            case SyntaxKind.FinallyKeyword:
                return "finally";
            case SyntaxKind.ReturnKeyword:
                return "return";
            case SyntaxKind.IsKeyword:
                return "is";
            case SyntaxKind.IsntKeyword:
                return "isnt";
            case SyntaxKind.TypeOfKeyword:
                return "typeof";
            case SyntaxKind.StructKeyword:
                return "struct";
            case SyntaxKind.ClassKeyword:
                return "class";
            default:
                return null;
        }
    }

    /// <summary>
    /// Gets base operator type of assignment operator type (e.g. += -> +).
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>Binary operator type.</returns>
    internal static SyntaxKind GetBinaryOperatorOfAssignmentOperator(SyntaxKind type) {
        switch (type) {
            case SyntaxKind.PlusEqualsToken:
                return SyntaxKind.PlusToken;
            case SyntaxKind.MinusEqualsToken:
                return SyntaxKind.MinusToken;
            case SyntaxKind.AsteriskEqualsToken:
                return SyntaxKind.AsteriskToken;
            case SyntaxKind.SlashEqualsToken:
                return SyntaxKind.SlashToken;
            case SyntaxKind.AmpersandEqualsToken:
                return SyntaxKind.AmpersandToken;
            case SyntaxKind.PipeEqualsToken:
                return SyntaxKind.PipeToken;
            case SyntaxKind.CaretEqualsToken:
                return SyntaxKind.CaretToken;
            case SyntaxKind.AsteriskAsteriskEqualsToken:
                return SyntaxKind.AsteriskAsteriskToken;
            case SyntaxKind.LessThanLessThanEqualsToken:
                return SyntaxKind.LessThanLessThanToken;
            case SyntaxKind.GreaterThanGreaterThanEqualsToken:
                return SyntaxKind.GreaterThanGreaterThanToken;
            case SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken:
                return SyntaxKind.GreaterThanGreaterThanGreaterThanToken;
            case SyntaxKind.PercentEqualsToken:
                return SyntaxKind.PercentToken;
            case SyntaxKind.QuestionQuestionEqualsToken:
                return SyntaxKind.QuestionQuestionToken;
            default:
                throw new BelteInternalException($"GetBinaryOperatorOfAssignmentOperator: unexpected syntax '{type}'");
        }
    }

    /// <summary>
    /// Gets all unary operator types.
    /// </summary>
    /// <returns>Unary operator types (calling code should not depend on order).</returns>
    internal static IEnumerable<SyntaxKind> GetUnaryOperatorTypes() {
        var types = (SyntaxKind[])Enum.GetValues(typeof(SyntaxKind));
        foreach (var type in types) {
            if (GetUnaryPrecedence(type) > 0)
                yield return type;
        }
    }

    /// <summary>
    /// Gets all binary operator types.
    /// </summary>
    /// <returns>Binary operator types (calling code should not depend on order).</returns>
    internal static IEnumerable<SyntaxKind> GetBinaryOperatorTypes() {
        var types = (SyntaxKind[])Enum.GetValues(typeof(SyntaxKind));
        foreach (var type in types) {
            if (GetBinaryPrecedence(type) > 0)
                yield return type;
        }
    }

    /// <summary>
    /// Checks if a <see cref="SyntaxKind" /> is a keyword.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>If the <see cref="SyntaxKind" /> is a keyword.</returns>
    internal static bool IsKeyword(this SyntaxKind type) {
        return type.ToString().EndsWith("Keyword");
    }

    /// <summary>
    /// Checks if a <see cref="SyntaxKind" /> is a <see cref="SyntaxToken" />.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>If the <see cref="SyntaxKind" /> is a token.</returns>
    internal static bool IsToken(this SyntaxKind type) {
        return !type.IsTrivia() && (type.IsKeyword() || type.ToString().EndsWith("Token"));
    }

    /// <summary>
    /// Checks if a <see cref="SyntaxKind" /> is trivia.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>If the <see cref="SyntaxKind" /> is trivia.</returns>
    internal static bool IsTrivia(this SyntaxKind type) {
        return type.ToString().EndsWith("Trivia");
    }

    /// <summary>
    /// Checks if a <see cref="SyntaxKind" /> is a comment.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>If the <see cref="SyntaxKind" /> is a comment.</returns>
    internal static bool IsComment(this SyntaxKind type) {
        return type == SyntaxKind.SingleLineCommentTrivia || type == SyntaxKind.MultiLineCommentTrivia;
    }
}
