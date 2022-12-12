using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// All types of things to be found in a source file.
/// </summary>
internal enum SyntaxType {
    END_OF_FILE_TOKEN,

    // Punctuation
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
    PERCENT_TOKEN,
    QUESTION_QUESTION_TOKEN,

    // Compound punctuation
    AMPERSAND_AMPERSAND_TOKEN,
    PIPE_PIPE_TOKEN,
    ASTERISK_ASTERISK_TOKEN,
    EQUALS_EQUALS_TOKEN,
    EXCLAMATION_EQUALS_TOKEN,
    LESS_THAN_LESS_THAN_TOKEN,
    GREATER_THAN_GREATER_THAN_TOKEN,
    GREATER_THAN_GREATER_THAN_GREATER_THAN_TOKEN,
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
    GREATER_THAN_GREATER_THAN_GREATER_THAN_EQUALS_TOKEN,
    PLUS_PLUS_TOKEN,
    MINUS_MINUS_TOKEN,
    PERCENT_EQUALS_TOKEN,
    QUESTION_QUESTION_EQUALS_TOKEN,

    // Keywords
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
    IS_KEYWORD,
    ISNT_KEYWORD,
    TYPEOF_KEYWORD,

    // Tokens with text
    BAD_TOKEN,
    IDENTIFIER_TOKEN,
    NUMERIC_LITERAL_TOKEN,
    STRING_LITERAL_TOKEN,

    // Trivia
    END_OF_LINE_TRIVIA,
    WHITESPACE_TRIVIA,
    SINGLELINE_COMMENT_TRIVIA,
    MULTILINE_COMMENT_TRIVIA,
    SKIPPED_TOKEN_TRIVIA,

    // Expressions
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
    TYPEOF_EXPRESSION,

    // Statements
    BLOCK,
    VARIABLE_DECLARATION_STATEMENT,
    EXPRESSION_STATEMENT,
    EMPTY_STATEMENT,

    // Jump statements
    BREAK_STATEMENT,
    CONTINUE_STATEMENT,
    RETURN_STATEMENT,

    IF_STATEMENT,
    WHILE_STATEMENT,
    FOR_STATEMENT,
    DO_WHILE_STATEMENT,
    TRY_STATEMENT,

    // Checked statements
    ELSE_CLAUSE,
    CATCH_CLAUSE,
    FINALLY_CLAUSE,

    // Type declarations
    TYPE_CLAUSE,

    // Declarations
    PARAMETER,
    FUNCTION_DECLARATION,
    LOCAL_FUNCTION_DECLARATION,
    GLOBAL_STATEMENT,
    COMPILATION_UNIT,
}

/// <summary>
/// Base building block of all things.
/// Because of generators, the order of fields in a node child class need to correctly reflect the source file.
/// <code>
/// sealed partial class PrefixExpression { // Wrong (would display `--a` as `a--`)
///     Token identifier { get; }
///     Token op { get; }
/// }
///
/// sealed partial class PrefixExpression { // Right
///     Token op { get; }
///     Token identifier { get; }
/// }
/// <code>
/// </summary>
internal abstract class Node {
    protected Node(SyntaxTree syntaxTree) {
        this.syntaxTree = syntaxTree;
    }

    /// <summary>
    /// Type of node (see SyntaxType).
    /// </summary>
    internal abstract SyntaxType type { get; }

    /// <summary>
    /// Syntax tree this node resides in.
    /// </summary>
    internal SyntaxTree syntaxTree { get; }

    /// <summary>
    /// Span of where the node is in the source text (not including line break).
    /// </summary>
    internal virtual TextSpan span {
        get {
            if (GetChildren().ToArray().Length == 0)
                return null;

            var first = GetChildren().First().span;
            var last = GetChildren().Last().span;
            return TextSpan.FromBounds(first.start, last.end);
        }
    }

    /// <summary>
    /// Span of where the node is in the source text (including line break).
    /// </summary>
    internal virtual TextSpan fullSpan {
        get {
            if (GetChildren().ToArray().Length == 0)
                return null;

            var first = GetChildren().First().fullSpan;
            var last = GetChildren().Last().fullSpan;
            return TextSpan.FromBounds(first.start, last.end);
        }
    }

    /// <summary>
    /// Location of where the node is in the source text.
    /// </summary>
    internal TextLocation location => syntaxTree == null ? null : new TextLocation(syntaxTree.text, span);

    /// <summary>
    /// Gets all child nodes.
    /// Order should be consistent of how they look in a file, but calling code should not depend on that.
    /// </summary>
    internal abstract IEnumerable<Node> GetChildren();

    public override string ToString() {
        using (var writer = new StringWriter()) {
            WriteTo(writer);
            return writer.ToString();
        }
    }

    /// <summary>
    /// Write text representation of this node to an out.
    /// </summary>
    /// <param name="writer">Out</param>
    internal void WriteTo(TextWriter writer) {
        PrettyPrint(writer, this);
    }

    /// <summary>
    /// Gets last token (of all children, recursive) under this node.
    /// </summary>
    /// <returns>Last token</returns>
    internal Token GetLastToken() {
        if (this is Token t)
            return t;

        return GetChildren().Last().GetLastToken();
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
}

/// <summary>
/// Token type.
/// </summary>
internal sealed class Token : Node {
    /// <param name="position">Position of token (indexed by the node, not character in source text)</param>
    /// <param name="text">Text related to token (if applicable)</param>
    /// <param name="value">Value related to token (if applicable)</param>
    /// <param name="leadingTrivia">Trivia before token (anything)</param>
    /// <param name="trailingTrivia">Trivia after token (same line)</param>
    internal Token(SyntaxTree syntaxTree, SyntaxType type, int position, string text, object value,
        ImmutableArray<SyntaxTrivia> leadingTrivia, ImmutableArray<SyntaxTrivia> trailingTrivia)
        : base(syntaxTree) {
        this.type = type;
        this.position = position;
        this.text = text;
        this.value = value;
        this.leadingTrivia = leadingTrivia;
        this.trailingTrivia = trailingTrivia;
    }

    internal override SyntaxType type { get; }

    /// <summary>
    /// Position of token (indexed by the node, not character in source text).
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// Text related to token (if applicable).
    /// </summary>
    internal string text { get; }

    /// <summary>
    /// Value related to token (if applicable).
    /// </summary>
    internal object value { get; }

    /// <summary>
    /// If token was created artificially, or if it came from the source text.
    /// </summary>
    internal bool isMissing => text == null;

    internal override TextSpan span => new TextSpan(position, text?.Length ?? 0);

    internal override TextSpan fullSpan {
        get {
            var start = leadingTrivia.Length == 0 ? span.start : leadingTrivia.First().span.start;
            var end = trailingTrivia.Length == 0 ? span.end : trailingTrivia.Last().span.end;
            return TextSpan.FromBounds(start, end);
        }
    }

    /// <summary>
    /// Trivia before token (anything).
    /// </summary>
    internal ImmutableArray<SyntaxTrivia> leadingTrivia { get; }

    /// <summary>
    /// Trivia after token (same line).
    /// </summary>
    internal ImmutableArray<SyntaxTrivia> trailingTrivia { get; }

    /// <summary>
    /// Gets all child nodes, which is none.
    /// </summary>
    internal override IEnumerable<Node> GetChildren() {
        return Array.Empty<Node>();
    }
}

/// <summary>
/// A node representing a source file, the root node of a syntax tree.
/// </summary>
internal sealed partial class CompilationUnit : Node {
    /// <param name="members">The top level nodes (global)</param>
    /// <param name="endOfFile">EOF token</param>
    internal CompilationUnit(SyntaxTree syntaxTree, ImmutableArray<Member> members, Token endOfFile)
        : base(syntaxTree) {
        this.members = members;
        this.endOfFile = endOfFile;
    }

    /// <summary>
    /// The top level nodes (global) in the source file.
    /// </summary>
    internal ImmutableArray<Member> members { get; }

    /// <summary>
    /// EOF token.
    /// </summary>
    internal Token endOfFile { get; }

    internal override SyntaxType type => SyntaxType.COMPILATION_UNIT;
}

/// <summary>
/// All trivia: comments and whitespace. Text that does not affect compilation.
/// </summary>
internal sealed class SyntaxTrivia {
    /// <param name="position">Position of the trivia (indexed by nodes, not by character)</param>
    /// <param name="text">Text associated with the trivia</param>
    internal SyntaxTrivia(SyntaxTree syntaxTree, SyntaxType type, int position, string text) {
        this.syntaxTree = syntaxTree;
        this.position = position;
        this.type = type;
        this.text = text;
    }

    internal SyntaxTree syntaxTree { get; }

    internal SyntaxType type { get; }

    /// <summary>
    /// The position of the trivia.
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// The span of where the trivia is in the source text.
    /// </summary>
    internal TextSpan span => new TextSpan(position, text?.Length ?? 0);

    /// <summary>
    /// Text associated with the trivia.
    /// </summary>
    internal string text { get; }
}

/// <summary>
/// A type clause, includes array dimensions, type name, and attributes.
/// </summary>
internal sealed class TypeClause : Node {
    /// <param name="attributes">Simple flag modifiers on a type (e.g. [NotNull])</param>
    /// <param name="constRefKeyword">Const keyword referring to a constant reference type</param>
    /// <param name="refKeyword">Ref keyword referring to a reference type</param>
    /// <param name="constKeyword">Const keyword referring to a constant type</param>
    /// <param name="brackets">Brackets, determine array dimensions</param>
    internal TypeClause(SyntaxTree syntaxTree, ImmutableArray<(Token, Token, Token)> attributes,
        Token constRefKeyword, Token refKeyword, Token constKeyword, Token typeName,
        ImmutableArray<(Token, Token)> brackets) : base(syntaxTree) {
        this.attributes = attributes;
        this.constRefKeyword = constRefKeyword;
        this.refKeyword = refKeyword;
        this.constKeyword = constKeyword;
        this.typeName = typeName;
        this.brackets = brackets;
    }

    /// <summary>
    /// Simple flag modifiers on a type.
    /// </summary>
    /// <param name="openBracket">Open square bracket token</param>
    /// <param name="identifier">Name of the attribute</param>
    /// <param name="closeBracket">Close square bracket token</param>
    internal ImmutableArray<(Token openBracket, Token identifier, Token closeBracket)> attributes { get; }

    /// <summary>
    /// Const keyword referring to a constant reference type, only valid if the refKeyword field is also set.
    /// </summary>
    internal Token? constRefKeyword { get; }

    /// <summary>
    /// Ref keyword referring to a reference type.
    /// </summary>
    internal Token? refKeyword { get; }

    /// <summary>
    /// Const keyword referring to a constant type.
    /// </summary>
    internal Token? constKeyword { get; }

    internal Token typeName { get; }

    /// <summary>
    /// Brackets defining array dimensions ([] -> 1 dimension, [][] -> 2 dimensions).
    /// </summary>
    /// <param name="openBracket">Open square bracket token</param>
    /// <param name="closeBracket">Close square bracket token</param>
    internal ImmutableArray<(Token openBracket, Token closeBracket)> brackets { get; }

    internal override SyntaxType type => SyntaxType.TYPE_CLAUSE;

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
