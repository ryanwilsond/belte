using System;
using System.CodeDom.Compiler;
using System.IO;
using Buckle.CodeAnalysis.Authoring;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Display;

/// <summary>
/// Extensions for the TextWriter object, writes text with predefined colors.
/// </summary>
internal static class TextWriterExtensions {
    /// <summary>
    /// Checks if the writer's out is System.Console.Out.
    /// </summary>
    /// <returns>True if the out is System.Console.Out.</returns>
    internal static bool IsConsole(this TextWriter writer) {
        if (writer == Console.Out)
            return !Console.IsOutputRedirected;

        if (writer == Console.Error)
            return !Console.IsErrorRedirected && !Console.IsOutputRedirected;

        if (writer is IndentedTextWriter iw && iw.InnerWriter.IsConsole())
            return true;

        return false;
    }

    /// <summary>
    /// Resets all color of Console (if using System.Console.Out).
    /// </summary>
    internal static void ResetColor(this TextWriter writer) {
        if (writer.IsConsole())
            Console.ResetColor();
    }

    /// <summary>
    /// Writes a keyword in default blue.
    /// </summary>
    /// <param name="text">Keyword.</param>
    internal static void WriteKeyword(this TextWriter writer, string text) {
        SetForeground(writer, ConsoleColor.Blue, Classification.Keyword);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes a keyword in default blue.
    /// </summary>
    /// <param name="type">Keyword, gets converted to text.</param>
    internal static void WriteKeyword(this TextWriter writer, SyntaxKind type) {
        SetForeground(writer, ConsoleColor.Blue, Classification.Keyword);
        writer.Write(SyntaxFacts.GetText(type));
        writer.ResetColor();
    }

    /// <summary>
    /// Writes an identifer in default white.
    /// </summary>
    /// <param name="text">Identifer.</param>
    internal static void WriteIdentifier(this TextWriter writer, string text) {
        SetForeground(writer, ConsoleColor.White, Classification.Identifier);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes a number in default cyan.
    /// </summary>
    /// <param name="text">Number as a string.</param>
    internal static void WriteNumber(this TextWriter writer, string text) {
        SetForeground(writer, ConsoleColor.Cyan, Classification.Number);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes a string in default yellow.
    /// </summary>
    /// <param name="text">String.</param>
    internal static void WriteString(this TextWriter writer, string text) {
        SetForeground(writer, ConsoleColor.Yellow, Classification.String);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes punctuation in default dark gray.
    /// </summary>
    /// <param name="text">Punctuation.</param>
    internal static void WritePunctuation(this TextWriter writer, string text) {
        SetForeground(writer, ConsoleColor.DarkGray, Classification.Text);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes punctuation in default dark gray.
    /// </summary>
    /// <param name="type">Punctuation, gets converted to text.</param>
    internal static void WritePunctuation(this TextWriter writer, SyntaxKind type) {
        SetForeground(writer, ConsoleColor.DarkGray, Classification.Text);
        writer.Write(SyntaxFacts.GetText(type));
        writer.ResetColor();
    }

    /// <summary>
    /// Writes a type name in default blue.
    /// </summary>
    /// <param name="text">Type name (not full clause).</param>
    internal static void WriteType(this TextWriter writer, string text) {
        SetForeground(writer, ConsoleColor.Blue, Classification.TypeName);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes a single space (colorless, so background color would be used by default).
    /// </summary>
    internal static void WriteSpace(this TextWriter writer) {
        writer.Write(" ");
    }

    private static void SetForeground(TextWriter writer, ConsoleColor color, Classification classification) {
        if (writer.IsConsole())
            Console.ForegroundColor = color;
    }
}
