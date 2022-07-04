using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

internal enum SyntaxType {
    END_OF_FILE_TOKEN,

    // punctuation
    PLUS_TOKEN,
    MINUS_TOKEN,
    ASTERISK_TOKEN,
    SLASH_TOKEN,
    OPEN_PAREN_TOKEN,
    CLOSE_PAREN_TOKEN,
    OPEN_BRACE_TOKEN,
    CLOSE_BRACE_TOKEN,
    OPEN_BRACKET_TOKEN,
    CLOSE_BRACKET_TOKEN,
    EQUALS_TOKEN,
    EXCLAMATION_TOKEN,
    AMPERSAND_TOKEN,
    PIPE_TOKEN,
    TILDE_TOKEN,
    CARET_TOKEN,
    COMMA_TOKEN,
    SEMICOLON_TOKEN,
    LESS_THAN_TOKEN,
    GREATER_THAN_TOKEN,

    // compound punctuation
    AMPERSAND_AMPERSAND_TOKEN,
    PIPE_PIPE_TOKEN,
    ASTERISK_ASTERISK_TOKEN,
    EQUALS_EQUALS_TOKEN,
    EXCLAMATION_EQUALS_TOKEN,
    LESS_THAN_LESS_THAN_TOKEN,
    GREATER_THAN_GREATER_THAN_TOKEN,
    LESS_THAN_EQUALS_TOKEN,
    GREATER_THAN_EQUALS_TOKEN,
    AMPERSAND_EQUALS_TOKEN,
    PIPE_EQUALS_TOKEN,
    CARET_EQUALS_TOKEN,
    PLUS_EQUALS_TOKEN,
    MINUS_EQUALS_TOKEN,
    ASTERISK_EQUALS_TOKEN,
    SLASH_EQUALS_TOKEN,
    ASTERISK_ASTERISK_EQUALS_TOKEN,
    LESS_THAN_LESS_THAN_EQUALS_TOKEN,
    GREATER_THAN_GREATER_THAN_EQUALS_TOKEN,
    PLUS_PLUS_TOKEN,
    MINUS_MINUS_TOKEN,

    // keywords
    TRUE_KEYWORD,
    FALSE_KEYWORD,
    NULL_KEYWORD,
    VAR_KEYWORD,
    CONST_KEYWORD,
    REF_KEYWORD,
    IF_KEYWORD,
    ELSE_KEYWORD,
    WHILE_KEYWORD,
    FOR_KEYWORD,
    DO_KEYWORD,
    BREAK_KEYWORD,
    CONTINUE_KEYWORD,
    TRY_KEYWORD,
    CATCH_KEYWORD,
    FINALLY_KEYWORD,
    RETURN_KEYWORD,

    // tokens with text
    BAD_TOKEN,
    IDENTIFIER_TOKEN,
    NUMERIC_LITERAL_TOKEN,
    STRING_LITERAL_TOKEN,

    // trivia
    END_OF_LINE_TRIVIA,
    WHITESPACE_TRIVIA,
    SINGLELINE_COMMENT_TRIVIA,
    MULTILINE_COMMENT_TRIVIA,
    SKIPPED_TOKEN_TRIVIA,

    // expressions
    INLINE_FUNCTION,
    EMPTY_EXPRESSION,
    LITERAL_EXPRESSION,
    NAME_EXPRESSION,
    ASSIGN_EXPRESSION,
    CALL_EXPRESSION,
    INDEX_EXPRESSION,
    PARENTHESIZED_EXPRESSION,
    BINARY_EXPRESSION,
    UNARY_EXPRESSION,
    PREFIX_EXPRESSION,
    POSTFIX_EXPRESSION,
    COMPOUND_ASSIGNMENT_EXPRESSION,
    REFERENCE_EXPRESSION,
    CAST_EXPRESSION,

    // statements
    BLOCK,
    VARIABLE_DECLARATION_STATEMENT,
    EXPRESSION_STATEMENT,
    EMPTY_STATEMENT,

    // jump statements
    BREAK_STATEMENT,
    CONTINUE_STATEMENT,
    RETURN_STATEMENT,

    IF_STATEMENT,
    WHILE_STATEMENT,
    FOR_STATEMENT,
    DO_WHILE_STATEMENT,
    TRY_STATEMENT,

    // checked statements
    ELSE_CLAUSE,
    CATCH_CLAUSE,
    FINALLY_CLAUSE,

    // type declarations
    TYPE_CLAUSE,

    // declarations
    PARAMETER,
    FUNCTION_DECLARATION,
    LOCAL_FUNCTION_DECLARATION,
    GLOBAL_STATEMENT,
    COMPILATION_UNIT,
}

internal abstract class Node {
    internal abstract SyntaxType type { get; }
    internal SyntaxTree syntaxTree { get; }

    internal virtual TextSpan span {
        get {
            if (GetChildren().ToArray().Length == 0)
                return null;

            var first = GetChildren().First().span;
            var last = GetChildren().Last().span;
            return TextSpan.FromBounds(first.start, last.end);
        }
    }

    internal virtual TextSpan fullSpan {
        get {
            if (GetChildren().ToArray().Length == 0)
                return null;

            var first = GetChildren().First().fullSpan;
            var last = GetChildren().Last().fullSpan;
            return TextSpan.FromBounds(first.start, last.end);
        }
    }

    protected Node(SyntaxTree syntaxTree_) {
        syntaxTree = syntaxTree_;
    }

    internal TextLocation location => syntaxTree == null ? null : new TextLocation(syntaxTree.text, span);

    internal abstract IEnumerable<Node> GetChildren();

    internal void WriteTo(TextWriter writer) {
        PrettyPrint(writer, this);
    }

    private void PrettyPrint(TextWriter writer, Node node, string indent = "", bool isLast = true) {
        var isConsoleOut = writer == Console.Out;
        var token = node as Token;

        if (isConsoleOut)
            Console.ForegroundColor = ConsoleColor.DarkGray;

        if (token != null) {
            foreach (var trivia in token.leadingTrivia) {
                writer.Write(indent);
                writer.Write("├─");
                writer.WriteLine($"L: {trivia.type}");
            }
        }

        var hasTrailingTrivia = token != null && token.trailingTrivia.Any();
        var tokenMarker = !hasTrailingTrivia && isLast ? "└─" : "├─";
        writer.Write($"{indent}{tokenMarker}");

        if (isConsoleOut)
            Console.ForegroundColor = node is Token ? ConsoleColor.DarkBlue : ConsoleColor.Cyan;

        writer.Write(node.type);

        if (node is Token t && t.value != null)
            writer.Write($" {t.value}");

        writer.WriteLine();

        if (isConsoleOut)
            Console.ForegroundColor = ConsoleColor.DarkGray;

        if (token != null) {
            foreach (var trivia in token.trailingTrivia) {
                var isLastTrailingTrivia = trivia == token.trailingTrivia.Last();
                var triviaMarker = isLast && isLastTrailingTrivia ? "└─" : "├─";

                writer.Write(indent);
                writer.Write(triviaMarker);
                writer.WriteLine($"T: {trivia.type}");
            }
        }

        if (isConsoleOut)
            Console.ResetColor();

        indent += isLast ? "  " : "│ ";
        var lastChild = node.GetChildren().LastOrDefault();

        foreach (var child in node.GetChildren())
            PrettyPrint(writer, child, indent, child == lastChild);
    }

    public override string ToString() {
        using (var writer = new StringWriter()) {
            WriteTo(writer);
            return writer.ToString();
        }
    }

    internal Token GetLastToken() {
        if (this is Token t)
            return t;

        return GetChildren().Last().GetLastToken();
    }
}

internal sealed class Token : Node {
    internal override SyntaxType type { get; }
    internal int position { get; }
    internal string text { get; }
    internal object value { get; }
    internal bool isMissing => text == null;
    internal override TextSpan span => new TextSpan(position, text?.Length ?? 0);
    internal override TextSpan fullSpan {
        get {
            var start = leadingTrivia.Length == 0 ? span.start : leadingTrivia.First().span.start;
            var end = trailingTrivia.Length == 0 ? span.end : trailingTrivia.Last().span.end;
            return TextSpan.FromBounds(start, end);
        }
    }
    internal ImmutableArray<SyntaxTrivia> leadingTrivia { get; }
    internal ImmutableArray<SyntaxTrivia> trailingTrivia { get; }

    internal Token(SyntaxTree syntaxTree, SyntaxType type_, int position_, string text_, object value_,
        ImmutableArray<SyntaxTrivia> leadingTrivia_, ImmutableArray<SyntaxTrivia> trailingTrivia_)
        : base(syntaxTree) {
        type = type_;
        position = position_;
        text = text_;
        value = value_;
        leadingTrivia = leadingTrivia_;
        trailingTrivia = trailingTrivia_;
    }

    internal override IEnumerable<Node> GetChildren() {
        return Array.Empty<Node>();
    }
}

internal sealed partial class CompilationUnit : Node {
    internal ImmutableArray<Member> members { get; }
    internal Token endOfFile { get; }
    internal override SyntaxType type => SyntaxType.COMPILATION_UNIT;

    internal CompilationUnit(SyntaxTree syntaxTree, ImmutableArray<Member> members_, Token endOfFile_)
        : base(syntaxTree) {
        members = members_;
        endOfFile = endOfFile_;
    }
}

internal sealed class SyntaxTrivia {
    internal SyntaxTree syntaxTree { get; }
    internal SyntaxType type { get; }
    internal int position { get; }
    internal TextSpan span => new TextSpan(position, text?.Length ?? 0);
    internal string text { get; }

    internal SyntaxTrivia(SyntaxTree syntaxTree_, SyntaxType type_, int position_, string text_) {
        syntaxTree = syntaxTree_;
        position = position_;
        type = type_;
        text = text_;
    }
}

internal sealed class TypeClause : Node {
    internal ImmutableArray<(Token openBracket, Token identifier, Token closeBracket)> attributes { get; }
    internal Token? constRefKeyword { get; }
    internal Token? refKeyword { get; }
    internal Token? constKeyword { get; }
    internal Token typeName { get; }
    internal ImmutableArray<(Token openBracket, Token closeBracket)> brackets { get; }
    internal override SyntaxType type => SyntaxType.TYPE_CLAUSE;

    internal TypeClause(SyntaxTree syntaxTree, ImmutableArray<(Token, Token, Token)> attributes_,
        Token constRefKeyword_, Token refKeyword_, Token constKeyword_, Token typeName_,
        ImmutableArray<(Token, Token)> brackets_) : base(syntaxTree) {
        attributes = attributes_;
        constRefKeyword = constRefKeyword_;
        refKeyword = refKeyword_;
        constKeyword = constKeyword_;
        typeName = typeName_;
        brackets = brackets_;
    }

    internal override IEnumerable<Node> GetChildren() {
        foreach (var attribute in attributes) {
            yield return attribute.openBracket;
            yield return attribute.identifier;
            yield return attribute.closeBracket;
        }

        if (constRefKeyword != null && constRefKeyword.fullSpan != null)
            yield return constRefKeyword;

        if (refKeyword != null && refKeyword.fullSpan != null)
            yield return refKeyword;

        if (constKeyword != null && constKeyword.fullSpan != null)
            yield return constKeyword;

        yield return typeName;

        foreach (var pair in brackets) {
            yield return pair.openBracket;
            yield return pair.closeBracket;
        }
    }
}
