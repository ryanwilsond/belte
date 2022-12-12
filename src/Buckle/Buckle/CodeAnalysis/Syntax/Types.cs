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
    // Punctuation
    TildeToken,
    ExclamationToken,
    PercentToken,
    CaretToken,
    AmpersandToken,
    AsteriskToken,
    OpenParenToken,
    CloseParenToken,
    MinusToken,
    PlusToken,
    EqualsToken,
    OpenBraceToken,
    CloseBraceToken,
    OpenBracketToken,
    CloseBracketToken,
    PipeToken,
    SemicolonToken,
    LessThanToken,
    CommaToken,
    GreaterThanToken,
    SlashToken,

    // Compound punctuation
    PipePipeToken,
    AmpersandAmpersandToken,
    MinusMinusToken,
    PlusPlusToken,
    AsteriskAsteriskToken,
    QuestionQuestionToken,
    ExclamationEqualsToken,
    EqualsEqualsToken,
    LessThanEqualsToken,
    LessThanLessThanToken,
    LessThanLessThanEqualsToken,
    GreaterThanEqualsToken,
    GreaterThanGreaterThanToken,
    GreaterThanGreaterThanEqualsToken,
    SlashEqualsToken,
    AsteriskEqualsToken,
    PipeEqualsToken,
    AmpersandEqualsToken,
    PlusEqualsToken,
    MinusEqualsToken,
    AsteriskAsteriskEqualsToken,
    CaretEqualsToken,
    PercentEqualsToken,
    QuestionQuestionEqualsToken,
    GreaterThanGreaterThanGreaterThanToken,
    GreaterThanGreaterThanGreaterThanEqualsToken,

    // Keywords
    TypeOfKeyword,
    NullKeyword,
    TrueKeyword,
    FalseKeyword,
    IfKeyword,
    ElseKeyword,
    WhileKeyword,
    ForKeyword,
    DoKeyword,
    TryKeyword,
    CatchKeyword,
    FinallyKeyword,
    BreakKeyword,
    ContinueKeyword,
    ReturnKeyword,
    ConstKeyword,
    RefKeyword,
    IsKeyword,
    IsntKeyword,
    StructKeyword,
    VarKeyword,

    // Tokens with text
    BadToken,
    IdentifierToken,
    NumericLiteralToken,
    StringLiteralToken,

    // Trivia
    EndOfLineTrivia,
    WhitespaceTrivia,
    SingleLineCommentTrivia,
    MultiLineCommentTrivia,
    SkippedTokenTrivia,

    // Expressions
    ParenthesizedExpression,
    CastExpression,
    EmptyExpression,

    // Operator expressions
    BinaryExpression,
    UnaryExpression,
    IndexExpression,
    PrefixExpression,
    PostfixExpression,
    AssignExpression,
    CompoundAssignmentExpression,

    // Primary expressions
    LiteralExpression,
    TypeOfExpression,
    InlineFunction,
    NameExpression,
    CallExpression,
    RefExpression,

    // Statements
    Block,
    VariableDeclarationStatement,
    ExpressionStatement,
    LocalFunctionStatement,
    EmptyStatement,

    // Jump statements
    BreakStatement,
    ContinueStatement,
    ReturnStatement,
    WhileStatement,
    DoWhileStatement,
    ForStatement,

    // Checked statements
    IfStatement,
    ElseClause,
    TryStatement,
    CatchClause,
    FinallyClause,

    // Declarations
    CompilationUnit,
    GlobalStatement,

    // Type declarations
    StructDeclaration,
    TypeClause,
    Parameter,
    MethodDeclaration,

    // Other
    EndOfFileToken,
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

    internal override SyntaxType type => SyntaxType.CompilationUnit;
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

    internal override SyntaxType type => SyntaxType.TypeClause;

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
