using System;
using System.Collections.Generic;
using System.Linq;
using Buckle.CodeAnalysis.Display;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Utilities;
using static Buckle.CodeAnalysis.Display.DisplayTextSegment;

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
public abstract partial class SyntaxNode {
    protected SyntaxNode(SyntaxTree syntaxTree, GreenNode node, int position) {
        this.syntaxTree = syntaxTree;
        green = node;
        this.position = position;
        parent = null;
    }

    /// <summary>
    /// Type of <see cref="SyntaxNode" /> (see <see cref="SyntaxKind" />).
    /// </summary>
    public SyntaxKind kind { get; }

    /// <summary>
    /// The parent of this node. The parent's children are this node's siblings.
    /// </summary>
    public SyntaxNode parent { get; private set; }

    /// <summary>
    /// <see cref="SyntaxTree" /> this <see cref="SyntaxNode" /> resides in.
    /// </summary>
    internal SyntaxTree syntaxTree { get; }

    /// <summary>
    /// The underlying basic node information.
    /// </summary>
    internal GreenNode green { get; }

    /// <summary>
    /// The absolute position of this <see cref="SyntaxNode" /> in the <see cref="SourceText" /> it came from.
    /// </summary>
    internal int position { get; }

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

            if (first == null || last == null)
                return null;

            return TextSpan.FromBounds(first.start, last.end);
        }
    }

    /// <summary>
    /// <see cref="TextSpan" /> of where the <see cref="SyntaxNode" /> is in the <see cref="SourceText" />
    /// (including line break).
    /// </summary>
    internal virtual TextSpan fullSpan => new TextSpan(position, green.fullWidth);

    /// <summary>
    /// Location of where the <see cref="SyntaxNode" /> is in the <see cref="SourceText" />.
    /// </summary>
    internal TextLocation location => syntaxTree == null ? null : new TextLocation(syntaxTree.text, span);

    /// <summary>
    /// If any diagnostics have spans that overlap with this node.
    /// Aka this node produced any diagnostics.
    /// </summary>
    internal bool containsDiagnostics => green.containsDiagnostics;

    public override string ToString() {
        var text = new DisplayText();
        PrettyPrint(text, this);

        return text.ToString();
    }

    /// <summary>
    /// Gets all child SyntaxNodes.
    /// Order should be consistent of how they look in a file, but calling code should not depend on that.
    /// </summary>
    public abstract IEnumerable<SyntaxNode> GetChildren();

    /// <summary>
    /// Write text representation of this <see cref="SyntaxNode" /> to an out.
    /// </summary>
    /// <param name="text">Out.</param>
    public void WriteTo(DisplayText text) {
        PrettyPrint(text, this);
    }

    /// <summary>
    /// Gets last <see cref="SyntaxToken" /> (of all children, recursive) under this <see cref="SyntaxNode" />.
    /// </summary>
    /// <returns>Last <see cref="SyntaxToken" />.</returns>
    public SyntaxToken GetLastToken() {
        return GetChildren().Last().GetLastToken();
    }

    /// <summary>
    /// Finds a token of this node whose span includes the supplied position.
    /// </summary>
    /// <param name="position">The character position of the token relative to the beginning of the file.</param>
    internal SyntaxToken FindToken(int position) {
        if (TryGetEndOfFileAt(position, out var endOfFile))
            return endOfFile;

        if (!fullSpan.Contains(position))
            throw new BelteInternalException("FindToken", new ArgumentOutOfRangeException(nameof(position)));

        var currentNode = this;

        while (true) {
            var node = currentNode is not SyntaxToken ? currentNode : null;

            if (node != null)
                currentNode = node.ChildThatContainsPosition(position);
            else
                return currentNode as SyntaxToken;
        }
    }

    /// <summary>
    /// Sets each child node's parent to this.
    /// </summary>
    internal static T InitializeChildrenParents<T>(T node) where T : SyntaxNode {
        foreach (var child in node.GetChildren())
            child.parent = node;

        return node as T;
    }

    /// <summary>
    /// Finds the index of the first child whose span contains the given position.
    /// </summary>
    internal static int GetFirstChildIndexSpanningPosition(SyntaxNode[] list, int position) {
        var lo = 0;
        var hi = list.Length - 1;

        while (lo <= hi) {
            var r = lo + ((hi - lo) >> 1);

            var m = list[r];
            if (position < m.fullSpan.start) {
                hi = r - 1;
            } else {
                if (position == m.fullSpan.start) {
                    for (; r > 0 && list[r - 1].fullSpan.length == 0; r--)
                        ;

                    return r;
                }

                if (position >= m.fullSpan.end) {
                    lo = r + 1;
                    continue;
                }

                return r;
            }
        }

        throw ExceptionUtilities.Unreachable();
    }

    private SyntaxNode ChildThatContainsPosition(int position) {
        var children = GetChildren();

        if (children.Count() == 0)
            return this;

        foreach (var child in children) {
            if (child.fullSpan.Contains(position))
                return child.ChildThatContainsPosition(position);
        }

        throw ExceptionUtilities.Unreachable();
    }

    private bool TryGetEndOfFileAt(int position, out SyntaxToken endOfFile) {
        if (fullSpan.length == 0) {
            if (this is CompilationUnitSyntax compilationUnit) {
                endOfFile = compilationUnit.endOfFile;
                return true;
            }
        }

        endOfFile = null;
        return false;
    }

    private void PrettyPrint(DisplayText text, SyntaxNode node, string indent = "", bool isLast = true) {
        var token = node as SyntaxToken;

        if (token != null) {
            foreach (var trivia in token.leadingTrivia) {
                text.Write(CreatePunctuation(indent));
                text.Write(CreatePunctuation("├─"));
                text.Write(CreateRedNode($"Lead: {trivia.kind} [{trivia.span.start}..{trivia.span.end})"));
                text.Write(CreateLine());
            }
        }

        var hasTrailingTrivia = token != null && token.trailingTrivia.Any();
        var tokenMarker = !hasTrailingTrivia && isLast ? "└─" : "├─";

        text.Write(CreatePunctuation($"{indent}{tokenMarker}"));

        if (node is SyntaxToken)
            text.Write(CreateGreenNode(node.kind.ToString()));
        else
            text.Write(CreateBlueNode(node.kind.ToString()));

        if (node is SyntaxToken t && t.value != null)
            text.Write(CreatePunctuation($" {t.value}"));

        if (node is SyntaxToken) {
            text.Write(CreateGreenNode($" [{node.span.start}..{node.span.end})"));
            text.Write(CreateLine());
        } else {
            text.Write(CreateBlueNode($" [{node.span.start}..{node.span.end})"));
            text.Write(CreateLine());
        }

        if (token != null) {
            foreach (var trivia in token.trailingTrivia) {
                var isLastTrailingTrivia = trivia == token.trailingTrivia.Last();
                var triviaMarker = isLast && isLastTrailingTrivia ? "└─" : "├─";

                text.Write(CreatePunctuation(indent));
                text.Write(CreatePunctuation(triviaMarker));
                text.Write(CreateRedNode($"Trail: {trivia.kind} [{trivia.span.start}..{trivia.span.end})"));
                text.Write(CreateLine());
            }
        }

        indent += isLast ? "  " : "│ ";
        var lastChild = node.GetChildren().LastOrDefault();

        foreach (var child in node.GetChildren())
            PrettyPrint(text, child, indent, child == lastChild);
    }
}
