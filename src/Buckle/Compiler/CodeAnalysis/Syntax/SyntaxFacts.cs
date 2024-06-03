using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Basic syntax facts references by the <see cref="InternalSyntax.LanguageParser" /> and the
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
            case SyntaxKind.AsKeyword:
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
            case SyntaxKind.NameOfKeyword:
            case SyntaxKind.OpenBracketToken:
            case SyntaxKind.OpenParenToken:
            case SyntaxKind.PeriodToken:
            case SyntaxKind.QuestionPeriodToken:
            case SyntaxKind.QuestionOpenBracketToken:
            case SyntaxKind.PlusPlusToken:
            case SyntaxKind.MinusMinusToken:
            case SyntaxKind.ExclamationToken:
            case SyntaxKind.NewKeyword:
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
        return type switch {
            SyntaxKind.QuestionToken => SyntaxKind.ColonToken,
            _ => throw new BelteInternalException($"GetTernaryOperatorPair: unknown right operator '{type}'"),
        };
    }

    /// <summary>
    /// Attempts to get a <see cref="SyntaxKind" /> from a text representation of a keyword.
    /// </summary>
    /// <param name="text">Text representation.</param>
    /// <returns>Keyword kind, defaults to identifier if failed.</returns>
    internal static SyntaxKind GetKeywordType(string text) {
        return text switch {
            "true" => SyntaxKind.TrueKeyword,
            "false" => SyntaxKind.FalseKeyword,
            "null" => SyntaxKind.NullKeyword,
            "const" => SyntaxKind.ConstKeyword,
            "ref" => SyntaxKind.RefKeyword,
            "if" => SyntaxKind.IfKeyword,
            "else" => SyntaxKind.ElseKeyword,
            "while" => SyntaxKind.WhileKeyword,
            "for" => SyntaxKind.ForKeyword,
            "do" => SyntaxKind.DoKeyword,
            "break" => SyntaxKind.BreakKeyword,
            "continue" => SyntaxKind.ContinueKeyword,
            "try" => SyntaxKind.TryKeyword,
            "catch" => SyntaxKind.CatchKeyword,
            "finally" => SyntaxKind.FinallyKeyword,
            "return" => SyntaxKind.ReturnKeyword,
            "is" => SyntaxKind.IsKeyword,
            "isnt" => SyntaxKind.IsntKeyword,
            "typeof" => SyntaxKind.TypeOfKeyword,
            "nameof" => SyntaxKind.NameOfKeyword,
            "struct" => SyntaxKind.StructKeyword,
            "class" => SyntaxKind.ClassKeyword,
            "new" => SyntaxKind.NewKeyword,
            "this" => SyntaxKind.ThisKeyword,
            "base" => SyntaxKind.BaseKeyword,
            "static" => SyntaxKind.StaticKeyword,
            "constexpr" => SyntaxKind.ConstexprKeyword,
            "operator" => SyntaxKind.OperatorKeyword,
            "lowlevel" => SyntaxKind.LowlevelKeyword,
            "extends" => SyntaxKind.ExtendsKeyword,
            "public" => SyntaxKind.PublicKeyword,
            "private" => SyntaxKind.PrivateKeyword,
            "protected" => SyntaxKind.ProtectedKeyword,
            "sealed" => SyntaxKind.SealedKeyword,
            "abstract" => SyntaxKind.AbstractKeyword,
            "virtual" => SyntaxKind.VirtualKeyword,
            "override" => SyntaxKind.OverrideKeyword,
            "constructor" => SyntaxKind.ConstructorKeyword,
            "as" => SyntaxKind.AsKeyword,
            "where" => SyntaxKind.WhereKeyword,
            "throw" => SyntaxKind.ThrowKeyword,
            _ => SyntaxKind.IdentifierToken,
        };
    }

    /// <summary>
    /// Gets text representation of a <see cref="SyntaxToken" /> or keyword.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>Text representation, default to null if not text representation exists.</returns>
    internal static string GetText(SyntaxKind type) {
        return type switch {
            SyntaxKind.PeriodToken => ".",
            SyntaxKind.CommaToken => ",",
            SyntaxKind.PlusToken => "+",
            SyntaxKind.MinusToken => "-",
            SyntaxKind.AsteriskToken => "*",
            SyntaxKind.SlashToken => "/",
            SyntaxKind.OpenParenToken => "(",
            SyntaxKind.CloseParenToken => ")",
            SyntaxKind.EqualsToken => "=",
            SyntaxKind.TildeToken => "~",
            SyntaxKind.CaretToken => "^",
            SyntaxKind.AmpersandToken => "&",
            SyntaxKind.PipeToken => "|",
            SyntaxKind.LessThanLessThanToken => "<<",
            SyntaxKind.GreaterThanGreaterThanToken => ">>",
            SyntaxKind.GreaterThanGreaterThanGreaterThanToken => ">>>",
            SyntaxKind.ExclamationToken => "!",
            SyntaxKind.AmpersandAmpersandToken => "&&",
            SyntaxKind.PipePipeToken => "||",
            SyntaxKind.AsteriskAsteriskToken => "**",
            SyntaxKind.PlusPlusToken => "++",
            SyntaxKind.MinusMinusToken => "--",
            SyntaxKind.OpenBraceToken => "{",
            SyntaxKind.CloseBraceToken => "}",
            SyntaxKind.OpenBracketToken => "[",
            SyntaxKind.CloseBracketToken => "]",
            SyntaxKind.SemicolonToken => ";",
            SyntaxKind.ColonToken => ":",
            SyntaxKind.QuestionToken => "?",
            SyntaxKind.EqualsEqualsToken => "==",
            SyntaxKind.ExclamationEqualsToken => "!=",
            SyntaxKind.LessThanToken => "<",
            SyntaxKind.GreaterThanToken => ">",
            SyntaxKind.PercentToken => "%",
            SyntaxKind.QuestionQuestionToken => "??",
            SyntaxKind.LessThanEqualsToken => "<=",
            SyntaxKind.GreaterThanEqualsToken => ">=",
            SyntaxKind.AmpersandEqualsToken => "&=",
            SyntaxKind.PipeEqualsToken => "|=",
            SyntaxKind.CaretEqualsToken => "^=",
            SyntaxKind.PlusEqualsToken => "+=",
            SyntaxKind.MinusEqualsToken => "-=",
            SyntaxKind.SlashEqualsToken => "/=",
            SyntaxKind.AsteriskEqualsToken => "*=",
            SyntaxKind.AsteriskAsteriskEqualsToken => "**=",
            SyntaxKind.GreaterThanGreaterThanEqualsToken => ">>=",
            SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken => ">>>=",
            SyntaxKind.LessThanLessThanEqualsToken => "<<=",
            SyntaxKind.PercentEqualsToken => "%=",
            SyntaxKind.QuestionQuestionEqualsToken => "??=",
            SyntaxKind.QuestionPeriodToken => "?.",
            SyntaxKind.QuestionOpenBracketToken => "?[",
            SyntaxKind.HashToken => "#",
            SyntaxKind.TrueKeyword => "true",
            SyntaxKind.FalseKeyword => "false",
            SyntaxKind.NullKeyword => "null",
            SyntaxKind.ConstKeyword => "const",
            SyntaxKind.RefKeyword => "ref",
            SyntaxKind.IfKeyword => "if",
            SyntaxKind.ElseKeyword => "else",
            SyntaxKind.WhileKeyword => "while",
            SyntaxKind.ForKeyword => "for",
            SyntaxKind.DoKeyword => "do",
            SyntaxKind.BreakKeyword => "break",
            SyntaxKind.ContinueKeyword => "continue",
            SyntaxKind.TryKeyword => "try",
            SyntaxKind.CatchKeyword => "catch",
            SyntaxKind.FinallyKeyword => "finally",
            SyntaxKind.ReturnKeyword => "return",
            SyntaxKind.IsKeyword => "is",
            SyntaxKind.IsntKeyword => "isnt",
            SyntaxKind.TypeOfKeyword => "typeof",
            SyntaxKind.NameOfKeyword => "nameof",
            SyntaxKind.StructKeyword => "struct",
            SyntaxKind.ClassKeyword => "class",
            SyntaxKind.NewKeyword => "new",
            SyntaxKind.ThisKeyword => "this",
            SyntaxKind.BaseKeyword => "base",
            SyntaxKind.StaticKeyword => "static",
            SyntaxKind.ConstexprKeyword => "constexpr",
            SyntaxKind.OperatorKeyword => "operator",
            SyntaxKind.LowlevelKeyword => "lowlevel",
            SyntaxKind.ExtendsKeyword => "extends",
            SyntaxKind.PublicKeyword => "public",
            SyntaxKind.PrivateKeyword => "private",
            SyntaxKind.ProtectedKeyword => "protected",
            SyntaxKind.SealedKeyword => "sealed",
            SyntaxKind.AbstractKeyword => "abstract",
            SyntaxKind.VirtualKeyword => "virtual",
            SyntaxKind.OverrideKeyword => "override",
            SyntaxKind.ConstructorKeyword => "constructor",
            SyntaxKind.AsKeyword => "as",
            SyntaxKind.WhereKeyword => "where",
            SyntaxKind.ThrowKeyword => "throw",
            _ => null,
        };
    }

    /// <summary>
    /// Gets base operator type of assignment operator type (e.g. += -> +).
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>Binary operator type.</returns>
    internal static SyntaxKind GetBinaryOperatorOfAssignmentOperator(SyntaxKind type) {
        return type switch {
            SyntaxKind.PlusEqualsToken => SyntaxKind.PlusToken,
            SyntaxKind.MinusEqualsToken => SyntaxKind.MinusToken,
            SyntaxKind.AsteriskEqualsToken => SyntaxKind.AsteriskToken,
            SyntaxKind.SlashEqualsToken => SyntaxKind.SlashToken,
            SyntaxKind.AmpersandEqualsToken => SyntaxKind.AmpersandToken,
            SyntaxKind.PipeEqualsToken => SyntaxKind.PipeToken,
            SyntaxKind.CaretEqualsToken => SyntaxKind.CaretToken,
            SyntaxKind.AsteriskAsteriskEqualsToken => SyntaxKind.AsteriskAsteriskToken,
            SyntaxKind.LessThanLessThanEqualsToken => SyntaxKind.LessThanLessThanToken,
            SyntaxKind.GreaterThanGreaterThanEqualsToken => SyntaxKind.GreaterThanGreaterThanToken,
            SyntaxKind.GreaterThanGreaterThanGreaterThanEqualsToken
                => SyntaxKind.GreaterThanGreaterThanGreaterThanToken,
            SyntaxKind.PercentEqualsToken => SyntaxKind.PercentToken,
            SyntaxKind.QuestionQuestionEqualsToken => SyntaxKind.QuestionQuestionToken,
            _ => throw new BelteInternalException($"GetBinaryOperatorOfAssignmentOperator: unexpected syntax '{type}'"),
        };
    }

    /// <summary>
    /// Checks if a <see cref="SyntaxKind" /> is an overloadable unary or binary operator.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>If the <see cref="SyntaxKind" /> is an overloadable operator.</returns>
    internal static bool IsOverloadableOperator(this SyntaxKind type) {
        return type switch {
            SyntaxKind.AsteriskAsteriskToken => true,
            SyntaxKind.AsteriskToken => true,
            SyntaxKind.SlashToken => true,
            SyntaxKind.PercentToken => true,
            SyntaxKind.PlusToken => true,
            SyntaxKind.MinusToken => true,
            SyntaxKind.LessThanLessThanToken => true,
            SyntaxKind.GreaterThanGreaterThanToken => true,
            SyntaxKind.GreaterThanGreaterThanGreaterThanToken => true,
            SyntaxKind.AmpersandToken => true,
            SyntaxKind.CaretToken => true,
            SyntaxKind.PipeToken => true,
            SyntaxKind.PlusPlusToken => true,
            SyntaxKind.MinusMinusToken => true,
            SyntaxKind.ExclamationToken => true,
            SyntaxKind.TildeToken => true,
            SyntaxKind.OpenBracketToken => true,
            _ => false,
        };
    }

    /// <summary>
    /// Gets the associations operator name of an operator token.
    /// </summary>
    /// <param name="type"><see cref="SyntaxKind" />.</param>
    /// <returns>The association operator of the token, or null if the token is not an overloadable operator.</returns>
    internal static string GetOperatorMemberName(SyntaxKind type, int arity) {
        return type switch {
            SyntaxKind.AsteriskAsteriskToken => WellKnownMemberNames.PowerOperatorName,
            SyntaxKind.AsteriskToken => WellKnownMemberNames.MultiplyOperatorName,
            SyntaxKind.SlashToken => WellKnownMemberNames.DivideOperatorName,
            SyntaxKind.PercentToken => WellKnownMemberNames.ModulusOperatorName,
            SyntaxKind.PlusToken when arity == 1 => WellKnownMemberNames.UnaryPlusOperatorName,
            SyntaxKind.PlusToken when arity != 1 => WellKnownMemberNames.AdditionOperatorName,
            SyntaxKind.MinusToken when arity == 1 => WellKnownMemberNames.UnaryNegationOperatorName,
            SyntaxKind.MinusToken when arity != 1 => WellKnownMemberNames.SubtractionOperatorName,
            SyntaxKind.LessThanLessThanToken => WellKnownMemberNames.LeftShiftOperatorName,
            SyntaxKind.GreaterThanGreaterThanToken => WellKnownMemberNames.RightShiftOperatorName,
            SyntaxKind.GreaterThanGreaterThanGreaterThanToken => WellKnownMemberNames.UnsignedRightShiftOperatorName,
            SyntaxKind.AmpersandToken => WellKnownMemberNames.BitwiseAndOperatorName,
            SyntaxKind.CaretToken => WellKnownMemberNames.BitwiseExclusiveOrOperatorName,
            SyntaxKind.PipeToken => WellKnownMemberNames.BitwiseOrOperatorName,
            SyntaxKind.PlusPlusToken => WellKnownMemberNames.IncrementOperatorName,
            SyntaxKind.MinusMinusToken => WellKnownMemberNames.DecrementOperatorName,
            SyntaxKind.ExclamationToken => WellKnownMemberNames.LogicalNotOperatorName,
            SyntaxKind.TildeToken => WellKnownMemberNames.BitwiseNotOperatorName,
            SyntaxKind.OpenBracketToken when arity != 3 => WellKnownMemberNames.IndexOperatorName,
            SyntaxKind.OpenBracketToken when arity == 3 => WellKnownMemberNames.IndexAssignName,
            SyntaxKind.QuestionOpenBracketToken when arity != 3 => WellKnownMemberNames.IndexOperatorName,
            SyntaxKind.QuestionOpenBracketToken when arity == 3 => WellKnownMemberNames.IndexAssignName,
            _ => null,
        };
    }

    /// <summary>
    /// Gets the arity of an operator.
    /// </summary>
    /// <param name="name">The well known operator name.</param>
    /// <returns>The arity of the operator.</returns>
    internal static int GetOperatorArity(string name) {
        return name switch {
            WellKnownMemberNames.PowerOperatorName => 2,
            WellKnownMemberNames.MultiplyOperatorName => 2,
            WellKnownMemberNames.DivideOperatorName => 2,
            WellKnownMemberNames.ModulusOperatorName => 2,
            WellKnownMemberNames.UnaryPlusOperatorName => 1,
            WellKnownMemberNames.AdditionOperatorName => 2,
            WellKnownMemberNames.UnaryNegationOperatorName => 1,
            WellKnownMemberNames.SubtractionOperatorName => 2,
            WellKnownMemberNames.LeftShiftOperatorName => 2,
            WellKnownMemberNames.RightShiftOperatorName => 2,
            WellKnownMemberNames.UnsignedRightShiftOperatorName => 2,
            WellKnownMemberNames.BitwiseAndOperatorName => 2,
            WellKnownMemberNames.BitwiseExclusiveOrOperatorName => 2,
            WellKnownMemberNames.BitwiseOrOperatorName => 2,
            WellKnownMemberNames.IncrementOperatorName => 1,
            WellKnownMemberNames.DecrementOperatorName => 1,
            WellKnownMemberNames.LogicalNotOperatorName => 1,
            WellKnownMemberNames.BitwiseNotOperatorName => 1,
            WellKnownMemberNames.IndexOperatorName => 2,
            WellKnownMemberNames.IndexAssignName => 3,
            _ => 0,
        };
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
