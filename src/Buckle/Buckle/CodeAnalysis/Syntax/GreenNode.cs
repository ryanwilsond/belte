
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Houses basic information for both SyntaxNodes and SyntaxTokens.
/// </summary>
internal abstract partial class GreenNode {
    protected NodeFlags flags;

    internal const int ListKind = 1;

    internal GreenNode() { }

    internal GreenNode(SyntaxKind kind) {
        this.kind = kind;
    }

    /// <summary>
    /// Creates a <see cref="GreenNode" />.
    /// </summary>
    internal GreenNode(SyntaxKind kind, int fullWidth) {
        this.kind = kind;
        this.fullWidth = fullWidth;
    }

    /// <summary>
    /// The full width/length of the <see cref="GreenNode" />.
    /// Includes leading and trialing trivia.
    /// </summary>
    public int fullWidth { get; protected set; }

    /// <summary>
    /// The number of children / "slots".
    /// </summary>
    public virtual int slotCount { get; protected set; }

    /// <summary>
    /// The width/length of the <see cref="GreenNode" /> excluding the leading and trailing trivia.
    /// </summary>
    /// <returns></returns>
    internal virtual int width => fullWidth - GetLeadingTriviaWidth() - GetTrailingTriviaWidth();

    /// <summary>
    /// If the node was created by the compiler rather than representing a part of the source text.
    /// </summary>
    internal bool isFabricated => (flags & NodeFlags.IsMissing) != 0;

    /// <summary>
    /// Type of <see cref="GreenNode" /> (see <see cref="SyntaxKind" />).
    /// </summary>
    internal SyntaxKind kind { get; }

    /// <summary>
    /// If any diagnostics have spans that overlap with this node.
    /// Aka this node produced any diagnostics.
    /// </summary>
    internal bool containsDiagnostics => (flags & NodeFlags.ContainsDiagnostics) != 0;

    /// <summary>
    /// If this node contains any skipped text from the source text in the form of trivia.
    /// </summary>
    internal bool containsSkippedText => (flags & NodeFlags.ContainsSkippedText) != 0;

    /// <summary>
    /// If this <see cref="GreenNode" /> is any token type.
    /// </summary>
    internal virtual bool isToken => false;

    /// <summary>
    /// If this <see cref="GreenNode" /> is any trivia type.
    /// </summary>
    internal virtual bool isTrivia => false;

    /// <summary>
    /// If this <see cref="GreeNode" /> is a syntax list.
    /// </summary>
    internal bool isList => (int)kind == ListKind;

    /// <summary>
    /// Returns a child at slot <param name="index" />.
    /// </summary>
    internal abstract GreenNode GetSlot(int index);

    /// <summary>
    /// Creates a "red" tree node.
    /// </summary>
    internal abstract SyntaxNode CreateRed(SyntaxNode parent, int position);

    /// <summary>
    /// Gets all child <see cref="GreenNodes" />.
    /// Order should be consistent of how they look in a file, but calling code should not depend on that.
    /// </summary>
    internal InternalSyntax.ChildSyntaxList ChildNodesAndTokens() {
        return new InternalSyntax.ChildSyntaxList(this);
    }

    /// <summary>
    /// Enables given flags.
    /// </summary>
    internal void SetFlags(NodeFlags flags) {
        this.flags |= flags;
    }

    /// <summary>
    /// Disables given flags.
    /// </summary>
    internal void ClearFlags(NodeFlags flags) {
        this.flags &= ~flags;
    }

    /// <summary>
    /// Creates a "red" tree node.
    /// </summary>
    internal SyntaxNode CreateRed() {
        return CreateRed(null, 0);
    }

    /// <summary>
    /// Gets the offset from the start of this <see cref="GreenNode" /> to the start of the child at slot
    /// <param name="index" />.
    /// </summary>
    internal int GetSlotOffset(int index) {
        int offset = 0;

        for (int i = 0; i < index; i++) {
            var child = GetSlot(i);

            if (child != null)
                offset = child.fullWidth;
        }

        return offset;
    }

    /// <summary>
    /// Finds which child/slot contains the offset from the start of the node.
    /// </summary>
    internal int FindSlotIndexContainingOffset(int offset) {
        int i;
        int accumulatedWidth = 0;

        for (i = 0; ; i++) {
            var child = GetSlot(i);

            if (child != null) {
                accumulatedWidth += child.fullWidth;

                if (offset < accumulatedWidth)
                    break;
            }
        }

        return i;
    }

    /// <summary>
    /// Gets the width of the leading trivia.
    /// </summary>
    internal virtual int GetLeadingTriviaWidth() {
        return fullWidth != 0 ? GetFirstTerminal().GetLeadingTriviaWidth() : 0;
    }

    /// <summary>
    /// Gets the width of the trailing trivia.
    /// </summary>
    /// <returns></returns>
    internal virtual int GetTrailingTriviaWidth() {
        return fullWidth != 0 ? GetLastTerminal().GetTrailingTriviaWidth() : 0;
    }

    /// <summary>
    /// Gets all leading trivia.
    /// </summary>
    internal virtual GreenNode GetLeadingTrivia() {
        return null;
    }

    /// <summary>
    /// Gets all trailing trivia.
    /// </summary>
    internal virtual GreenNode GetTrailingTrivia() {
        return null;
    }

    /// <summary>
    /// Gets the stored value, if any.
    /// </summary>
    internal virtual object GetValue() {
        return null;
    }

    /// <summary>
    /// Returns a copy of this node with leading trivia.
    /// </summary>
    internal virtual GreenNode WithLeadingTrivia(GreenNode trivia) {
        return this;
    }

    /// <summary>
    /// Returns a copy of this node with trailing trivia.
    /// </summary>
    internal virtual GreenNode WithTrailingTrivia(GreenNode trivia) {
        return this;
    }

    /// <summary>
    /// Gets the first existing child <see cref="GreenNode" />.
    /// </summary>
    internal GreenNode GetFirstTerminal() {
        GreenNode node = this;

        do {
            GreenNode firstChild = null;

            for (int i = 0, n = node.slotCount; i < n; i++) {
                var child = node.GetSlot(i);

                if (child != null) {
                    firstChild = child;
                    break;
                }
            }

            node = firstChild;
        } while (node.slotCount > 0);

        return node;
    }

    /// <summary>
    /// Gets the last existing child <see cref="GreenNode" />.
    /// </summary>
    internal GreenNode GetLastTerminal() {
        GreenNode node = this;

        do {
            GreenNode lastChild = null;

            for (int i = node.slotCount - 1; i >= 0; i--) {
                var child = node.GetSlot(i);

                if (child != null) {
                    lastChild = child;
                    break;
                }
            }

            node = lastChild;
        } while (node.slotCount > 0);

        return node;
    }

    protected void AdjustFlagsAndWidth(GreenNode node) {
        flags |= node.flags;
        fullWidth += node.fullWidth;
    }
}
