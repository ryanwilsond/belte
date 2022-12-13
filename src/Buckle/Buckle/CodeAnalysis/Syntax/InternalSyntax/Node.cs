using System.IO;
using System.Linq;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Text;
using System;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Base building block of all things.
/// Because of generators, the order of fields in a <see cref="Node" /> child class need to correctly reflect the
/// source file.
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
    /// Type of <see cref="Node" /> (see <see cref="SyntaxType" />).
    /// </summary>
    internal abstract SyntaxType type { get; }

    /// <summary>
    /// <see cref="SyntaxTree" /> this <see cref="Node" /> resides in.
    /// </summary>
    internal SyntaxTree syntaxTree { get; }

    /// <summary>
    /// <see cref="TextSpan" /> of where the <see cref="Node" /> is in the <see cref="SourceText" />
    /// (not including line break).
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
    /// <see cref="TextSpan" /> of where the <see cref="Node" /> is in the <see cref="SourceText" />
    /// (including line break).
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
    /// Location of where the <see cref="Node" /> is in the <see cref="SourceText" />.
    /// </summary>
    internal TextLocation location => syntaxTree == null ? null : new TextLocation(syntaxTree.text, span);

    /// <summary>
    /// Gets all child Nodes.
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
    /// Write text representation of this <see cref="Node" /> to an out.
    /// </summary>
    /// <param name="writer">Out.</param>
    internal void WriteTo(TextWriter writer) {
        PrettyPrint(writer, this);
    }

    /// <summary>
    /// Gets last <see cref="Token" /> (of all children, recursive) under this <see cref="Node" />.
    /// </summary>
    /// <returns>Last <see cref="Token" />.</returns>
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
