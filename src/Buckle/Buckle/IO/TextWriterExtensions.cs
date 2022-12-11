using System;
using System.IO;
using System.CodeDom.Compiler;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.IO;

// TODO Use REPL color themes instead of hard coding colors
/// <summary>
/// Extensions for the TextWriter object, writes text with predefined colors.
/// </summary>
internal static class TextWriterExtensions {
    /// <summary>
    /// Checks if the writer's out is System.Console.Out.
    /// </summary>
    /// <returns>True if the out is System.Console.Out</returns>
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
    /// Sets foreground color of Console (if using System.Console.Out).
    /// </summary>
    /// <param name="color">New foreground color</param>
    internal static void SetForeground(this TextWriter writer, ConsoleColor color) {
        if (writer.IsConsole())
            Console.ForegroundColor = color;
    }

    /// <summary>
    /// Resets all color of Console (if using System.Console.Out).
    /// </summary>
    internal static void ResetColor(this TextWriter writer) {
        if (writer.IsConsole())
            Console.ResetColor();
    }

    /// <summary>
    /// Writes a keyword in blue.
    /// </summary>
    /// <param name="text">Keyword</param>
    internal static void WriteKeyword(this TextWriter writer, string text) {
        writer.SetForeground(ConsoleColor.Blue);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes a keyword in blue.
    /// </summary>
    /// <param name="type">Keyword, gets converted to text</param>
    internal static void WriteKeyword(this TextWriter writer, SyntaxType type) {
        writer.SetForeground(ConsoleColor.Blue);
        writer.Write(SyntaxFacts.GetText(type));
        writer.ResetColor();
    }

    /// <summary>
    /// Writes an identifer in white.
    /// </summary>
    /// <param name="text">Identifer</param>
    internal static void WriteIdentifier(this TextWriter writer, string text) {
        writer.SetForeground(ConsoleColor.White);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes a number in cyan.
    /// </summary>
    /// <param name="text">Number as a string</param>
    internal static void WriteNumber(this TextWriter writer, string text) {
        writer.SetForeground(ConsoleColor.Cyan);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes a string in yellow.
    /// </summary>
    /// <param name="text">String</param>
    internal static void WriteString(this TextWriter writer, string text) {
        writer.SetForeground(ConsoleColor.Yellow);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes punctuation in dark gray.
    /// </summary>
    /// <param name="text">Punctuation</param>
    internal static void WritePunctuation(this TextWriter writer, string text) {
        writer.SetForeground(ConsoleColor.DarkGray);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes punctuation in dark gray.
    /// </summary>
    /// <param name="type">Punctuation, gets converted to text</param>
    internal static void WritePunctuation(this TextWriter writer, SyntaxType type) {
        writer.SetForeground(ConsoleColor.DarkGray);
        writer.Write(SyntaxFacts.GetText(type));
        writer.ResetColor();
    }

    /// <summary>
    /// Writes a type name in blue.
    /// </summary>
    /// <param name="text">Type name (not full clause)</param>
    internal static void WriteType(this TextWriter writer, string text) {
        writer.SetForeground(ConsoleColor.Blue);
        writer.Write(text);
        writer.ResetColor();
    }

    /// <summary>
    /// Writes a single space (colorless, so background color would be used by default).
    /// </summary>
    internal static void WriteSpace(this TextWriter writer) {
        writer.Write(" ");
    }
}
