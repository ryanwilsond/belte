using System;
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
    internal SyntaxNode(SyntaxTree syntaxTree, GreenNode node, int position) {
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
    /// The number of children.
    /// </summary>
    internal int slotCount => green.slotCount;

    /// <summary>
    /// <see cref="TextSpan" /> of where the <see cref="SyntaxNode" /> is in the <see cref="SourceText" />
    /// (not including line break).
    /// </summary>
    internal virtual TextSpan span {
        get {
            var start = position;
            var width = green.fullWidth;

            var precedingWidth = green.GetLeadingTriviaWidth();
            start += precedingWidth;
            width -= precedingWidth;

            width -= green.GetTrailingTriviaWidth();

            return new TextSpan(start, width);
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
        PrettyPrint(text, new SyntaxNodeOrToken(this));

        return text.ToString();
    }

    /// <summary>
    /// Write text representation of this <see cref="SyntaxNode" /> to an out.
    /// </summary>
    /// <param name="text">Out.</param>
    public void WriteTo(DisplayText text) {
        PrettyPrint(text, new SyntaxNodeOrToken(this));
    }

    /// <summary>
    /// Gets the node at the given index.
    /// </summary>
    internal abstract SyntaxNode GetNodeSlot(int slot);

    /// <summary>
    /// All child nodes and tokens of this node.
    /// </summary>
    public ChildSyntaxList ChildNodesAndTokens() {
        return new ChildSyntaxList(this);
    }

    /// <summary>
    /// Gets last <see cref="SyntaxToken" /> (of all children, recursive) under this <see cref="SyntaxNode" />.
    /// </summary>
    /// <returns>Last <see cref="SyntaxToken" />.</returns>
    public SyntaxToken GetLastToken() {
        // TODO implement SyntaxNavigator
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

        SyntaxNodeOrToken currentNode = new SyntaxNodeOrToken(this);

        while (true) {
            var node = currentNode.AsNode();

            if (node != null)
                currentNode = node.ChildThatContainsPosition(position);
            else
                return currentNode.AsToken();
        }
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

    private SyntaxNodeOrToken ChildThatContainsPosition(int position) {
        if (!fullSpan.Contains(position))
            throw new ArgumentOutOfRangeException(nameof(position));

        var childNodeOrToken = ChildSyntaxList.ChildThatContainsPosition(this, position);
        return childNodeOrToken;
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

    private void PrettyPrint(DisplayText text, SyntaxNodeOrToken node, string indent = "", bool isLast = true) {
        var token = node.AsToken();

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

        if (node.IsToken)
            text.Write(CreateGreenNode(node.AsToken().kind.ToString()));
        else
            text.Write(CreateBlueNode(node.AsNode().kind.ToString()));

        if (node.AsToken(out var t) && t.value != null)
            text.Write(CreatePunctuation($" {t.value}"));

        if (node.IsToken) {
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

        if (node.IsToken)
            return;

        var children = node.AsNode().ChildNodesAndTokens();
        var lastChild = children.Last();

        foreach (var child in children)
            PrettyPrint(text, child, indent, child == lastChild);
    }
}
