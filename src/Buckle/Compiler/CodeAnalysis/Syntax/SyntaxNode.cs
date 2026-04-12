using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Base building block of all things on the syntax trees.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
public abstract partial class SyntaxNode {
    private protected SyntaxTree _syntaxTree;

    /// <summary>
    /// Creates a new <see cref="SyntaxNode" /> from an underlying <see cref="GreenNode" />.
    /// </summary>
    /// <param name="parent">
    /// The parent of this node, if any parent exists. Otherwise this node is treated as the root of
    /// a <see cref="SyntaxTree" />.
    /// </param>
    /// <param name="node">The underlying <see cref="GreenNode" />.</param>
    /// <param name="position">
    /// The absolute position of this node in relation to the <see cref="SourceText" /> of the
    /// housing <see cref="SyntaxTree" />.
    /// </param>
    internal SyntaxNode(SyntaxNode parent, GreenNode node, int position) {
        green = node;
        this.position = position;
        this.parent = parent;
    }

    internal SyntaxNode(GreenNode node, int position, SyntaxTree syntaxTree) : this(null, node, position) {
        _syntaxTree = syntaxTree;
    }

    /// <summary>
    /// Type of <see cref="SyntaxNode" /> (see <see cref="SyntaxKind" />).
    /// </summary>
    public SyntaxKind kind => green.kind;

    /// <summary>
    /// The parent of this node. The parent's children are this node's siblings.
    /// </summary>
    public SyntaxNode parent { get; }

    /// <summary>
    /// <see cref="SyntaxTree" /> this <see cref="SyntaxNode" /> resides in.
    /// </summary>
    public abstract SyntaxTree syntaxTree { get; }

    /// <summary>
    /// <see cref="TextSpan" /> of where the <see cref="SyntaxNode" /> is in the <see cref="SourceText" />
    /// (not including line break).
    /// </summary>
    public virtual TextSpan span {
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
    public virtual TextSpan fullSpan => new TextSpan(position, green.fullWidth);

    /// <summary>
    /// The absolute position of this <see cref="SyntaxNode" /> in the <see cref="SourceText" /> it came from.
    /// </summary>
    internal int position { get; }

    /// <summary>
    /// The position of the very end of this <see cref="SyntaxNode" />.
    /// </summary>
    public int endPosition => position + green.fullWidth;

    /// <summary>
    /// The number of children.
    /// </summary>
    public int slotCount => green.slotCount;

    /// <summary>
    /// The width of this <see cref="SyntaxNode" /> including all trivia.
    /// </summary>
    public int fullWidth => green.fullWidth;

    /// <summary>
    /// The width of this <see cref="SyntaxNode" /> excluding all trivia.
    /// </summary>
    public int width => green.width;

    /// <summary>
    /// Location of where the <see cref="SyntaxNode" /> is in the <see cref="SourceText" />.
    /// </summary>
    public TextLocation location => syntaxTree is null ? null : new TextLocation(syntaxTree.text, span, syntaxTree);

    /// <summary>
    /// If any diagnostics have spans that overlap with this node.
    /// Aka this node produced any diagnostics.
    /// </summary>
    public bool containsDiagnostics => green.containsDiagnostics;

    /// <summary>
    /// If this node is a list.
    /// </summary>
    public bool isList => green.isList;

    public bool isFabricated => green.isFabricated;

    public bool containsDirectives => green.containsDirectives;

    /// <summary>
    /// The underlying basic node information.
    /// </summary>
    internal GreenNode green { get; }

    public override string ToString() {
        return green.ToString();
    }

    public virtual void WriteTo(TextWriter writer) {
        green.WriteTo(writer, true, true);
    }

    /// <summary>
    /// All child nodes and tokens of this node.
    /// </summary>
    public ChildSyntaxList ChildNodesAndTokens() {
        return new ChildSyntaxList(this);
    }

    /// <summary>
    /// Gets the last <see cref="SyntaxToken" /> (of all children, recursive) under this <see cref="SyntaxNode" />.
    /// </summary>
    /// <returns>Last <see cref="SyntaxToken" />.</returns>
    public SyntaxToken GetLastToken(bool includeZeroWidth = false, bool includeSkipped = false) {
        return SyntaxNavigator.Instance.GetLastToken(this, includeZeroWidth, includeSkipped);
    }

    /// <summary>
    /// Gets the first <see cref="SyntaxToken" /> (of all children, recursive) under this <see cref="SyntaxNode" />.
    /// </summary>
    /// <returns>Last <see cref="SyntaxToken" />.</returns>
    public SyntaxToken GetFirstToken(bool includeZeroWidth = false, bool includeSkipped = false) {
        return SyntaxNavigator.Instance.GetFirstToken(this, includeZeroWidth, includeSkipped);
    }

    public bool Contains(SyntaxNode node) {
        if (node is null || !fullSpan.Contains(node.fullSpan))
            return false;

        while (node is not null) {
            if (node == this)
                return true;

            if (node.parent is not null)
                node = node.parent;
            else
                node = null;
        }

        return false;
    }

    /// <summary>
    /// Returns a copy of this node with an assigned <see cref="SyntaxTree" />.
    /// </summary>
    internal static T CloneNodeAsRoot<T>(T node, SyntaxTree syntaxTree) where T : SyntaxNode {
        var clone = (T)node.green.CreateRed();
        clone._syntaxTree = syntaxTree;
        return clone;
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
    /// Gets the start position of the child at the given index.
    /// </summary>
    internal virtual int GetChildPosition(int index) {
        if (GetCachedSlot(index) is { } node)
            return node.position;

        var offset = 0;
        var green = this.green;

        while (index > 0) {
            index--;
            var prevSibling = GetCachedSlot(index);

            if (prevSibling is not null)
                return prevSibling.endPosition + offset;

            var greenChild = green.GetSlot(index);

            if (greenChild is not null)
                offset += greenChild.fullWidth;
        }

        return position + offset;
    }

    /// <summary>
    /// Gets the leading trivia of this node, if any exists.
    /// </summary>
    internal SyntaxTriviaList GetLeadingTrivia() {
        return GetFirstToken(includeZeroWidth: true).leadingTrivia;
    }

    /// <summary>
    /// Gets the trailing trivia of this node, if any exists.
    /// </summary>
    internal SyntaxTriviaList GetTrailingTrivia() {
        return GetLastToken(includeZeroWidth: true).trailingTrivia;
    }

    /// <summary>
    /// Finds a token of this node whose span includes the supplied position.
    /// </summary>
    /// <param name="position">The character position of the token relative to the beginning of the file.</param>
    internal SyntaxToken FindToken(int position) {
        if (TryGetEndOfFileAt(position, out var endOfFile))
            return endOfFile;

        if (!fullSpan.Contains(position))
            throw new ArgumentOutOfRangeException(nameof(position));

        SyntaxNodeOrToken currentNode = this;

        while (true) {
            var node = currentNode.AsNode();

            if (node is not null)
                currentNode = node.ChildThatContainsPosition(position);
            else
                return currentNode.AsToken();
        }
    }

    /// <summary>
    /// Gets the start position of the child at the given index, calculated from the end of the node.
    /// </summary>
    internal int GetChildPositionFromEnd(int index) {
        if (GetCachedSlot(index) is { } node)
            return node.position;

        var green = this.green;
        var offset = green.GetSlot(index)?.fullWidth ?? 0;
        var slotCount = green.slotCount;

        while (index < slotCount - 1) {
            index++;
            var nextSibling = GetCachedSlot(index);

            if (nextSibling is not null)
                return nextSibling.position - offset;

            var greenChild = green.GetSlot(index);

            if (greenChild is not null)
                offset += greenChild.fullWidth;
        }

        return endPosition - offset;
    }

    /// <summary>
    /// Gets the child index of the given slot, accounting for the slot count of child lists.
    /// </summary>
    internal int GetChildIndex(int slot) {
        var index = 0;

        for (var i = 0; i < slot; i++) {
            var item = green.GetSlot(i);

            if (item is not null) {
                if (item.isList)
                    index += item.slotCount;
                else
                    index++;
            }
        }

        return index;
    }

    /// <summary>
    /// Gets the red element at the given slot if the passed in element is null, and assigns the passed in reference
    /// <param name="element" />. Also returns the retrieved element.
    /// </summary>
    internal SyntaxNode GetRedElement(ref SyntaxNode element, int slot) {
        var result = element;

        if (result is null) {
            var green = this.green.GetSlot(slot);
            Interlocked.CompareExchange(ref element, green.CreateRed(parent, GetChildPosition(slot)), null);
            result = element;
        }

        return result;
    }

    /// <summary>
    /// Gets the red node at slot 1, if it is not a token, and assigns the passed in reference <para name="element" />.
    /// Also returns the retrieved node.
    /// </summary>
    internal SyntaxNode GetRedElementIfNotToken(ref SyntaxNode element) {
        var result = element;

        if (result is null) {
            var green = this.green.GetSlot(1);

            if (!green.isToken) {
                Interlocked.CompareExchange(ref element, green.CreateRed(parent, GetChildPosition(1)), null);
                result = element;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the red element at the given slot and assigns the passed in reference <param name="element" />.
    /// Also returns the retrieved element.
    /// </summary>
    internal SyntaxNode GetRed(ref SyntaxNode field, int slot) {
        var result = field;

        if (result is null) {
            var green = this.green.GetSlot(slot);

            if (green is not null) {
                Interlocked.CompareExchange(ref field, green.CreateRed(this, GetChildPosition(slot)), null);
                result = field;
            }
        }

        return result;
    }

    /// <summary>
    /// Gets the red element at slot 0 and assigns the passed in reference <param name="element" />.
    /// Also returns the retrieved element.
    /// </summary>
    internal SyntaxNode GetRedAtZero(ref SyntaxNode field) {
        // Special case where getting the child position is unnecessary (would always return 0)
        var result = field;

        if (result is null) {
            var green = this.green.GetSlot(0);

            if (green is not null) {
                Interlocked.CompareExchange(ref field, green.CreateRed(this, position), null);
                result = field;
            }
        }

        return result;
    }

    internal TNode FirstAncestorOrSelf<TNode>(Func<TNode, bool> predicate = null, bool ascendOutOfTrivia = true)
        where TNode : SyntaxNode {
        for (var node = this; node is not null; node = GetParent(node, ascendOutOfTrivia)) {
            if (node is TNode tNode && (predicate == null || predicate(tNode)))
                return tNode;
        }

        return null;
    }

    internal IList<DirectiveTriviaSyntax> GetDirectives(Func<DirectiveTriviaSyntax, bool> filter = null) {
        return ((SyntaxNodeOrToken)this).GetDirectives(filter);
    }

    private static SyntaxNode GetParent(SyntaxNode node, bool ascendOutOfTrivia) {
        var parent = node.parent;

        if (parent is null && ascendOutOfTrivia) {
            if (node is StructuredTriviaSyntax structuredTrivia)
                parent = structuredTrivia.parentTrivia.token.parent;
        }

        return parent;
    }

    private protected T GetRed<T>(ref T field, int slot) where T : SyntaxNode {
        var result = field;

        if (result is null) {
            var green = this.green.GetSlot(slot);

            if (green is not null) {
                Interlocked.CompareExchange(ref field, (T)green.CreateRed(this, GetChildPosition(slot)), null);
                result = field;
            }
        }

        return result;
    }

    // special case of above function where slot = 0, does not need GetChildPosition
    private protected T? GetRedAtZero<T>(ref T? field) where T : SyntaxNode {
        var result = field;

        if (result is null) {
            var green = this.green.GetSlot(0);

            if (green is not null) {
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

    private bool TryGetEndOfFileAt(int _, out SyntaxToken endOfFile) {
        if (fullSpan.length == 0) {
            if (this is CompilationUnitSyntax compilationUnit) {
                endOfFile = compilationUnit.endOfFile;
                return true;
            }
        }

        endOfFile = null;
        return false;
    }

    private string GetDebuggerDisplay() {
        return GetType().Name + " " + kind + " " + ToString();
    }
}
