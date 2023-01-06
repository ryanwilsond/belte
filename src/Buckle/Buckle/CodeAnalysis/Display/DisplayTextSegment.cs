using Buckle.CodeAnalysis.Authoring;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Display;

/// <summary>
/// A single piece of text with a single <see cref="Classification" /> associated with it.
/// </summary>
internal sealed class DisplayTextSegment {
    private DisplayTextSegment(string text, Classification classification) {
        this.text = text;
        this.classification = classification;
    }

    /// <summary>
    /// Raw text being represented.
    /// </summary>
    internal string text { get; }

    /// <summary>
    /// The <see cref="Classification" /> associated with the represented text.
    /// </summary>
    internal Classification classification { get; }

    /// <summary>
    /// Creates a new line.
    /// </summary>
    internal static DisplayTextSegment CreateLine() {
        return new DisplayTextSegment(null, Classification.Line);
    }

    /// <summary>
    /// Creates a space.
    /// </summary>
    internal static DisplayTextSegment CreateSpace() {
        return new DisplayTextSegment(" ", Classification.Text);
    }

    /// <summary>
    /// Creates an indentation (usually displayed as a tab).
    /// </summary>
    internal static DisplayTextSegment CreateIndent() {
        return new DisplayTextSegment(null, Classification.Indent);
    }

    /// <summary>
    /// Creates a keyword.
    /// </summary>
    /// <param name="text">Text to be treated as a keyword.</param>
    internal static DisplayTextSegment CreateKeyword(string text) {
        return new DisplayTextSegment(text, Classification.Keyword);
    }

    /// <summary>
    /// Creates a keyword.
    /// </summary>
    /// <param name="kind"><see cref="SyntaxKind" /> to be treated as a keyword, converts to text.</param>
    internal static DisplayTextSegment CreateKeyword(SyntaxKind kind) {
        return CreateKeyword(SyntaxFacts.GetText(kind));
    }

    /// <summary>
    /// Creates a punctuation.
    /// </summary>
    /// <param name="text">Text to be treated as a punctuation.</param>
    internal static DisplayTextSegment CreatePunctuation(string text) {
        return new DisplayTextSegment(text, Classification.Text);
    }

    /// <summary>
    /// Creates a punctuation.
    /// </summary>
    /// <param name="text"><see cref="SyntaxKind" /> to be treated as a punctuation, converts to text.</param>
    internal static DisplayTextSegment CreatePunctuation(SyntaxKind kind) {
        return CreatePunctuation(SyntaxFacts.GetText(kind));
    }

    /// <summary>
    /// Creates a identifier.
    /// </summary>
    /// <param name="text">Text to be treated as a identifier.</param>
    internal static DisplayTextSegment CreateIdentifier(string text) {
        return new DisplayTextSegment(text, Classification.Identifier);
    }

    /// <summary>
    /// Creates a type.
    /// </summary>
    /// <param name="text">Text to be treated as a type.</param>
    internal static DisplayTextSegment CreateType(string text) {
        return new DisplayTextSegment(text, Classification.Type);
    }

    /// <summary>
    /// Creates a number.
    /// </summary>
    /// <param name="text">Text to be treated as a number.</param>
    internal static DisplayTextSegment CreateNumber(string text) {
        return new DisplayTextSegment(text, Classification.Number);
    }

    /// <summary>
    /// Creates a string.
    /// </summary>
    /// <param name="text">Text to be treated as a string (Belte string, not a C# string).</param>
    internal static DisplayTextSegment CreateString(string text) {
        return new DisplayTextSegment(text, Classification.String);
    }

    /// <summary>
    /// Creates a red Node.
    /// </summary>
    /// <param name="text">Text to be treated as a red Node.</param>
    internal static DisplayTextSegment CreateRedNode(string text) {
        return new DisplayTextSegment(text, Classification.RedNode);
    }

    /// <summary>
    /// Creates a green Node.
    /// </summary>
    /// <param name="text">Text to be treated as a green Node.</param>
    internal static DisplayTextSegment CreateGreenNode(string text) {
        return new DisplayTextSegment(text, Classification.GreenNode);
    }

    /// <summary>
    /// Creates a green Node.
    /// </summary>
    /// <param name="text"><see cref="SyntaxKind" /> to be treated as a green Node, converts to text.</param>
    internal static DisplayTextSegment CreateGreenNode(SyntaxKind kind) {
        return CreateGreenNode(SyntaxFacts.GetText(kind));
    }

    /// <summary>
    /// Creates a blue Node.
    /// </summary>
    /// <param name="text">Text to be treated as a blue Node.</param>
    internal static DisplayTextSegment CreateBlueNode(string text) {
        return new DisplayTextSegment(text, Classification.BlueNode);
    }

    /// <summary>
    /// Creates a blue Node.
    /// </summary>
    /// <param name="text"><see cref="SyntaxKind" /> to be treated as a blue Node, converts to text.</param>
    internal static DisplayTextSegment CreateBlueNode(SyntaxKind kind) {
        return CreateBlueNode(SyntaxFacts.GetText(kind));
    }
}
