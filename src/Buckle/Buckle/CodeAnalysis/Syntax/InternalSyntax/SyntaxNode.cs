using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Base building block of all things.
/// Because of generators, the order of fields in a <see cref="SyntaxNode" /> child class need to correctly reflect the
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
internal abstract class SyntaxNode {
    protected SyntaxNode(SyntaxTree syntaxTree) {
        this.syntaxTree = syntaxTree;
    }

    /// <summary>
    /// Type of <see cref="SyntaxNode" /> (see <see cref="SyntaxKind" />).
    /// </summary>
    internal abstract SyntaxKind kind { get; }

    /// <summary>
    /// <see cref="SyntaxTree" /> this <see cref="SyntaxNode" /> resides in.
    /// </summary>
    internal SyntaxTree syntaxTree { get; }

    /// <summary>
    /// <see cref="TextSpan" /> of where the <see cref="SyntaxNode" /> is in the <see cref="SourceText" />
    /// (not including line break).
    /// </summary>
    internal virtual TextSpan span {
        get {
            var children = GetChildren();

            if (children.ToArray().Length == 0)
                return null;

            var first = children.First().span;
            var last = children.Last().span;

            return TextSpan.FromBounds(first.start, last.end);
        }
    }

    /// <summary>
    /// <see cref="TextSpan" /> of where the <see cref="SyntaxNode" /> is in the <see cref="SourceText" />
    /// (including line break).
    /// </summary>
    internal virtual TextSpan fullSpan {
        get {
            var children = GetChildren();

            if (children.ToArray().Length == 0)
                return null;

            var first = children.First().fullSpan;
            var last = children.Last().fullSpan;

            return TextSpan.FromBounds(first.start, last.end);
        }
    }

    /// <summary>
    /// Location of where the <see cref="SyntaxNode" /> is in the <see cref="SourceText" />.
    /// </summary>
    internal TextLocation location => syntaxTree == null ? null : new TextLocation(syntaxTree.text, span);

    /// <summary>
    /// Gets all child SyntaxNodes.
    /// Order should be consistent of how they look in a file, but calling code should not depend on that.
    /// </summary>
    internal abstract IEnumerable<SyntaxNode> GetChildren();

    public override string ToString() {
        using (var writer = new StringWriter()) {
            WriteTo(writer);

            return writer.ToString();
        }
    }

    /// <summary>
    /// Write text representation of this <see cref="SyntaxNode" /> to an out.
    /// </summary>
    /// <param name="writer">Out.</param>
    internal void WriteTo(TextWriter writer) {
        PrettyPrint(writer, this);
    }

    /// <summary>
    /// Gets last <see cref="SyntaxToken" /> (of all children, recursive) under this <see cref="SyntaxNode" />.
    /// </summary>
    /// <returns>Last <see cref="SyntaxToken" />.</returns>
    internal SyntaxToken GetLastToken() {
        if (this is SyntaxToken t)
            return t;

        return GetChildren().Last().GetLastToken();
    }

    private void PrettyPrint(TextWriter writer, SyntaxNode node, string indent = "", bool isLast = true) {
        var isConsoleOut = writer == Console.Out;
        var token = node as SyntaxToken;

        if (isConsoleOut)
            Console.ForegroundColor = ConsoleColor.Red;

        if (token != null) {
            foreach (var trivia in token.leadingTrivia) {
                writer.Write(indent);
                writer.Write("├─");
                writer.WriteLine($"Lead: {trivia.kind} [{trivia.span.start}..{trivia.span.end})");
            }
        }

        var hasTrailingTrivia = token != null && token.trailingTrivia.Any();
        var tokenMarker = !hasTrailingTrivia && isLast ? "└─" : "├─";

        if (isConsoleOut)
            Console.ForegroundColor = ConsoleColor.DarkGray;

        writer.Write($"{indent}{tokenMarker}");

        if (isConsoleOut)
            Console.ForegroundColor = node is SyntaxToken ? ConsoleColor.Green : ConsoleColor.Blue;

        writer.Write(node.kind);

        if (node is SyntaxToken t && t.value != null)
            writer.Write($" {t.value}");

        writer.WriteLine($" [{node.span.start}..{node.span.end})");

        if (token != null) {
            foreach (var trivia in token.trailingTrivia) {
                var isLastTrailingTrivia = trivia == token.trailingTrivia.Last();
                var triviaMarker = isLast && isLastTrailingTrivia ? "└─" : "├─";

                if (isConsoleOut)
                    Console.ForegroundColor = ConsoleColor.DarkGray;

                writer.Write(indent);
                writer.Write(triviaMarker);

                if (isConsoleOut)
                    Console.ForegroundColor = ConsoleColor.Red;

                writer.WriteLine($"Trail: {trivia.kind} [{trivia.span.start}..{trivia.span.end})");
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
