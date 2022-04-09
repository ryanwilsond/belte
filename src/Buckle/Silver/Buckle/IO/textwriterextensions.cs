using System;
using System.CodeDom.Compiler;
using System.IO;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.IO {
    internal static class TextWriterExtensions {
        public static bool IsConsole(this TextWriter writer) {
            if (writer == Console.Out)
                return !Console.IsOutputRedirected;

            if (writer == Console.Error)
                return !Console.IsErrorRedirected && !Console.IsOutputRedirected;

            if (writer is IndentedTextWriter iw && iw.InnerWriter.IsConsole())
                return true;

            return false;
        }
        public static void SetForeground(this TextWriter writer, ConsoleColor color) {
            if (writer.IsConsole())
                Console.ForegroundColor = color;
        }

        public static void ResetColor(this TextWriter writer) {
            if (writer.IsConsole())
                Console.ResetColor();
        }

        public static void WriteKeyword(this TextWriter writer, string text) {
            writer.SetForeground(ConsoleColor.Blue);
            writer.Write(text);
            writer.ResetColor();
        }

        public static void WriteKeyword(this TextWriter writer, SyntaxType type) {
            writer.SetForeground(ConsoleColor.Blue);
            writer.Write(SyntaxFacts.GetText(type));
            writer.ResetColor();
        }

        public static void WriteIdentifier(this TextWriter writer, string text) {
            writer.SetForeground(ConsoleColor.White);
            writer.Write(text);
            writer.ResetColor();
        }

        public static void WriteNumber(this TextWriter writer, string text) {
            writer.SetForeground(ConsoleColor.Cyan);
            writer.Write(text);
            writer.ResetColor();
        }

        public static void WriteString(this TextWriter writer, string text) {
            writer.SetForeground(ConsoleColor.Yellow);
            writer.Write(text);
            writer.ResetColor();
        }

        public static void WritePunctuation(this TextWriter writer, string text) {
            writer.SetForeground(ConsoleColor.DarkGray);
            writer.Write(text);
            writer.ResetColor();
        }

        public static void WritePunctuation(this TextWriter writer, SyntaxType type) {
            writer.SetForeground(ConsoleColor.DarkGray);
            writer.Write(SyntaxFacts.GetText(type));
            writer.ResetColor();
        }

        public static void WriteType(this TextWriter writer, string text) {
            writer.SetForeground(ConsoleColor.Blue);
            writer.Write(text);
            writer.ResetColor();
        }

        public static void WriteSpace(this TextWriter writer) {
            writer.Write(" ");
        }
    }
}
