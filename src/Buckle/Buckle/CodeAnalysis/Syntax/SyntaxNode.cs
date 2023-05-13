using System;
using System.Linq;
using System.Threading;
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

    internal SyntaxNode(SyntaxNode parent, GreenNode node, int position) {
        syntaxTree = null;
        green = node;
        this.position = position;
        this.parent = parent;
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
    /// The position of the very end of this <see cref="SyntaxNode" />.
    /// </summary>
    internal int endPosition => position + green.fullWidth;

    /// <summary>
    /// The number of children.
    /// </summary>
    internal int slotCount => green.slotCount;

    /// <summary>
    /// The width of this <see cref="SyntaxNode" /> including all trivia.
    /// </summary>
    internal int fullWidth => green.fullWidth;

    /// <summary>
    /// The width of this <see cref="SyntaxNode" /> excluding all trivia.
    /// </summary>
    internal int width => green.width;

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

    /// <summary>
    /// If this node is a list.
    /// </summary>
    internal bool isList => green.isList;

    public override string ToString() {
        var text = new DisplayText();
        PrettyPrint(text, this);

        return text.ToString();
    }

    /// <summary>
    /// Write text representation of this <see cref="SyntaxNode" /> to an out.
    /// </summary>
    /// <param name="text">Out.</param>
    public void WriteTo(DisplayText text) {
        PrettyPrint(text, this);
    }

    /// <summary>
    /// Gets the node at the given index, and if no node exists create one.
    /// </summary>
    internal abstract SyntaxNode GetNodeSlot(int index);

    /// <summary>
    /// Get the node at the given index.
    /// </summary>
    internal abstract SyntaxNode GetCachedSlot(int index);

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
        return SyntaxNavigator.Instance.GetLastToken(this);
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

        SyntaxNodeOrToken currentNode = this;

        while (true) {
            var node = currentNode.AsNode();

            if (node != null)
                currentNode = node.ChildThatContainsPosition(position);
            else
                return currentNode.AsToken();
        }
    }

    internal virtual int GetChildPosition(int index) {
        if (GetCachedSlot(index) is { } node)
            return node.position;

        int offset = 0;
        var green = this.green;

        while (index > 0) {
            index--;
            var prevSibling = GetCachedSlot(index);

            if (prevSibling != null)
                return prevSibling.endPosition + offset;

            var greenChild = green.GetSlot(index);

            if (greenChild != null)
                offset += greenChild.fullWidth;
        }

        return position + offset;
    }

    internal int GetChildPositionFromEnd(int index) {
        if (this.GetCachedSlot(index) is { } node)
            return node.position;

        var green = this.green;
        int offset = green.GetSlot(index)?.fullWidth ?? 0;
        int slotCount = green.slotCount;

        while (index < slotCount - 1) {
            index++;
            var nextSibling = this.GetCachedSlot(index);

            if (nextSibling != null)
                return nextSibling.position - offset;

            var greenChild = green.GetSlot(index);

            if (greenChild != null)
                offset += greenChild.fullWidth;
        }

        return endPosition - offset;
    }

    internal int GetChildIndex(int slot) {
        int index = 0;

        for (int i = 0; i < slot; i++) {
            var item = green.GetSlot(i);

            if (item != null) {
                if (item.isList)
                    index += item.slotCount;
                else
                    index++;
            }
        }

        return index;
    }

    internal SyntaxNode GetRedElement(ref SyntaxNode element, int slot) {
        var result = element;

        if (result == null) {
            var green = this.green.GetSlot(slot);
            Interlocked.CompareExchange(ref element, green.CreateRed(parent, GetChildPosition(slot)), null);
            result = element;
        }

        return result;
    }

    internal SyntaxNode GetRedElementIfNotToken(ref SyntaxNode element) {
        var result = element;

        if (result == null) {
            var green = this.green.GetSlot(1);

            if (!green.isToken) {
                Interlocked.CompareExchange(ref element, green.CreateRed(parent, GetChildPosition(1)), null);
                result = element;
            }
        }

        return result;
    }

    internal SyntaxNode GetRed(ref SyntaxNode field, int slot) {
        var result = field;

        if (result == null) {
            var green = this.green.GetSlot(slot);

            if (green != null) {
                Interlocked.CompareExchange(ref field, green.CreateRed(this, GetChildPosition(slot)), null);
                result = field;
            }
        }

        return result;
    }

    internal SyntaxNode GetRedAtZero(ref SyntaxNode field) {
        // Special case where getting the child position is unnecessary (would always return 0)
        var result = field;

        if (result == null) {
            var green = this.green.GetSlot(0);

            if (green != null) {
                Interlocked.CompareExchange(ref field, green.CreateRed(this, position), null);
                result = field;
            }
        }

        return result;
    }

    protected T? GetRed<T>(ref T? field, int slot) where T : SyntaxNode {
        var result = field;

        if (result == null) {
            var green = this.green.GetSlot(slot);

            if (green != null) {
                Interlocked.CompareExchange(ref field, (T)green.CreateRed(this, GetChildPosition(slot)), null);
                result = field;
            }
        }

        return result;
    }

    // special case of above function where slot = 0, does not need GetChildPosition
    protected T? GetRedAtZero<T>(ref T? field) where T : SyntaxNode {
        var result = field;

        if (result == null) {
            var green = this.green.GetSlot(0);

            if (green != null) {
                Interlocked.CompareExchange(ref field, (T)green.CreateRed(this, position), null);
                result = field;
            }
        }

        return result;
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

        if (node.isToken)
            text.Write(CreateGreenNode(node.AsToken().kind.ToString()));
        else
            text.Write(CreateBlueNode(node.AsNode().kind.ToString()));

        if (node.AsToken(out var t) && t.value != null)
            text.Write(CreatePunctuation($" {t.value}"));

        if (node.isToken) {
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

        if (node.isToken)
            return;

        var children = node.AsNode().ChildNodesAndTokens();
        var lastChild = children.Last();

        foreach (var child in children)
            PrettyPrint(text, child, indent, child == lastChild);
    }
}
