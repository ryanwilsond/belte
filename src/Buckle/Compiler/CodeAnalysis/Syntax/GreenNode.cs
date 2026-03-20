using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using Diagnostics;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Houses basic information for both SyntaxNodes and SyntaxTokens.
/// </summary>
[DebuggerDisplay("{GetDebuggerDisplay(), nq}")]
internal abstract partial class GreenNode {
    private protected NodeFlags _flags;

    /// <summary>
    /// A <see cref="SyntaxKind" /> that represents any list kind.
    /// </summary>
    internal const SyntaxKind ListKind = (SyntaxKind)1;

    private static readonly ConditionalWeakTable<GreenNode, Diagnostic[]> DiagnosticsTable =
        new ConditionalWeakTable<GreenNode, Diagnostic[]>();
    private static readonly Diagnostic[] NoDiagnostics = Array.Empty<Diagnostic>();

    /// <summary>
    /// Creates a new <see cref="GreenNode" />.
    /// </summary>
    internal GreenNode(SyntaxKind kind) {
        this.kind = kind;
    }

    /// <summary>
    /// Creates a new <see cref="GreenNode" /> that contains diagnostics.
    /// </summary>
    internal GreenNode(SyntaxKind kind, Diagnostic[] diagnostics) {
        this.kind = kind;

        if (diagnostics?.Length > 0) {
            _flags |= NodeFlags.ContainsDiagnostics;
            DiagnosticsTable.Add(this, diagnostics);
        }
    }

    /// <summary>
    /// Creates a new <see cref="GreenNode" /> with a preset width.
    /// </summary>
    internal GreenNode(SyntaxKind kind, int fullWidth) {
        this.kind = kind;
        this.fullWidth = fullWidth;
    }

    /// <summary>
    /// Creates a new <see cref="GreenNode" /> with a preset width and diagnostics.
    /// </summary>
    internal GreenNode(SyntaxKind kind, int fullWidth, Diagnostic[] diagnostics) {
        this.kind = kind;
        this.fullWidth = fullWidth;

        if (diagnostics?.Length > 0) {
            _flags |= NodeFlags.ContainsDiagnostics;
            DiagnosticsTable.Add(this, diagnostics);
        }
    }

    /// <summary>
    /// The full width/length of the <see cref="GreenNode" />.
    /// Includes leading and trialing trivia.
    /// </summary>
    public int fullWidth { get; private protected set; }

    /// <summary>
    /// The number of children / "slots".
    /// </summary>
    public virtual int slotCount { get; private protected set; }

    /// <summary>
    /// The width/length of the <see cref="GreenNode" /> excluding the leading and trailing trivia.
    /// </summary>
    /// <returns></returns>
    internal virtual int width => fullWidth - GetLeadingTriviaWidth() - GetTrailingTriviaWidth();

    /// <summary>
    /// If the node was created by the compiler rather than representing a part of the source text.
    /// </summary>
    internal bool isFabricated => (_flags & NodeFlags.IsMissing) != 0;

    /// <summary>
    /// Type of <see cref="GreenNode" /> (see <see cref="SyntaxKind" />).
    /// </summary>
    internal SyntaxKind kind { get; }

    /// <summary>
    /// If any diagnostics have spans that overlap with this node.
    /// Aka this node produced any diagnostics.
    /// </summary>
    internal bool containsDiagnostics => (_flags & NodeFlags.ContainsDiagnostics) != 0;

    /// <summary>
    /// If this node contains any preprocessor directives.
    /// </summary>
    internal bool containsDirectives => (_flags & NodeFlags.ContainsDirectives) != 0;

    /// <summary>
    /// If this node contains any skipped text from the source text in the form of trivia.
    /// </summary>
    internal bool containsSkippedText => (_flags & NodeFlags.ContainsSkippedText) != 0;

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
    internal bool isList => kind == ListKind;

    public override string ToString() {
        var sb = PooledStringBuilder.GetInstance();
        var writer = new StringWriter(sb.Builder);
        WriteTo(writer, leading: false, trailing: false);
        return sb.ToStringAndFree();
    }

    public void WriteTo(TextWriter writer) {
        WriteTo(writer, leading: true, trailing: true);
    }

    /// <summary>
    /// Returns a child at slot <param name="index" />.
    /// </summary>
    internal abstract GreenNode GetSlot(int index);

    /// <summary>
    /// Creates a "red" tree node.
    /// </summary>
    internal abstract SyntaxNode CreateRed(SyntaxNode parent, int position);

    /// <summary>
    /// Returns a new node with assigned diagnostics.
    /// </summary>
    internal abstract GreenNode SetDiagnostics(Diagnostic[] diagnostics);

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
    /// Gets all child <see cref="GreenNodes" />.
    /// Order should be consistent of how they look in a file, but calling code should not depend on that.
    /// </summary>
    internal InternalSyntax.ChildSyntaxList ChildNodesAndTokens() {
        return new InternalSyntax.ChildSyntaxList(this);
    }

    /// <summary>
    /// Numerates all child nodes and tokens of the tree rooted y this node.
    /// </summary>
    internal IEnumerable<GreenNode> EnumerateNodes() {
        yield return this;

        var stack = new Stack<InternalSyntax.ChildSyntaxList.Enumerator>(24);
        stack.Push(ChildNodesAndTokens().GetEnumerator());

        while (stack.Count > 0) {
            var en = stack.Pop();

            if (!en.MoveNext())
                continue;

            var current = en.current;
            stack.Push(en);

            yield return current;

            if (!current.isToken) {
                stack.Push(current.ChildNodesAndTokens().GetEnumerator());
                continue;
            }
        }
    }

    /// <summary>
    /// Enables given flags.
    /// </summary>
    internal void SetFlags(NodeFlags flags) {
        _flags |= flags;
    }

    /// <summary>
    /// Disables given flags.
    /// </summary>
    internal void ClearFlags(NodeFlags flags) {
        _flags &= ~flags;
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
        var offset = 0;

        for (var i = 0; i < index; i++) {
            var child = GetSlot(i);

            if (child is not null)
                offset += child.fullWidth;
        }

        return offset;
    }

    /// <summary>
    /// Finds which child/slot contains the offset from the start of the node.
    /// </summary>
    internal int FindSlotIndexContainingOffset(int offset) {
        int i;
        var accumulatedWidth = 0;

        for (i = 0; ; i++) {
            var child = GetSlot(i);

            if (child is not null) {
                accumulatedWidth += child.fullWidth;

                if (offset < accumulatedWidth)
                    break;
            }
        }

        return i;
    }

    /// <summary>
    /// Gets the first existing child <see cref="GreenNode" />.
    /// </summary>
    internal GreenNode GetFirstTerminal() {
        var node = this;

        do {
            GreenNode firstChild = null;

            for (int i = 0, n = node.slotCount; i < n; i++) {
                var child = node.GetSlot(i);

                if (child is not null) {
                    firstChild = child;
                    break;
                }
            }

            node = firstChild;
        } while (node?.slotCount > 0);

        return node;
    }

    /// <summary>
    /// Gets the last existing child <see cref="GreenNode" />.
    /// </summary>
    internal GreenNode GetLastTerminal() {
        var node = this;

        do {
            GreenNode lastChild = null;

            for (var i = node.slotCount - 1; i >= 0; i--) {
                var child = node.GetSlot(i);

                if (child is not null) {
                    lastChild = child;
                    break;
                }
            }

            node = lastChild;
        } while (node.slotCount > 0);

        return node;
    }

    /// <summary>
    /// Gets all diagnostics under this node.
    /// </summary>
    internal Diagnostic[] GetDiagnostics() {
        if (containsDiagnostics && DiagnosticsTable.TryGetValue(this, out var diagnostics))
            return diagnostics;

        return NoDiagnostics;
    }

    protected internal void WriteTo(TextWriter writer, bool leading, bool trailing) {
        var stack = new Stack<(GreenNode node, bool leading, bool trailing)>();
        stack.Push((this, leading, trailing));

        ProcessStack(writer, stack);
        return;

        static void ProcessStack(TextWriter writer, Stack<(GreenNode node, bool leading, bool trailing)> stack) {
            while (stack.Count > 0) {
                var current = stack.Pop();
                var currentNode = current.node;
                var currentLeading = current.leading;
                var currentTrailing = current.trailing;

                if (currentNode.isToken) {
                    currentNode.WriteTokenTo(writer, currentLeading, currentTrailing);
                    continue;
                }

                if (currentNode.isTrivia) {
                    currentNode.WriteTriviaTo(writer);
                    continue;
                }

                var firstIndex = GetFirstNonNullChildIndex(currentNode);
                var lastIndex = GetLastNonNullChildIndex(currentNode);

                for (var i = lastIndex; i >= firstIndex; i--) {
                    var child = currentNode.GetSlot(i);

                    if (child is not null) {
                        var first = i == firstIndex;
                        var last = i == lastIndex;
                        stack.Push((child, currentLeading | !first, currentTrailing | !last));
                    }
                }
            }
        }
    }

    private protected virtual void WriteTriviaTo(TextWriter writer) {
        throw new NotImplementedException();
    }

    private protected virtual void WriteTokenTo(TextWriter writer, bool leading, bool trailing) {
        throw new NotImplementedException();
    }

    private protected void AdjustFlagsAndWidth(GreenNode node) {
        _flags |= node._flags;
        fullWidth += node.fullWidth;
    }

    private static int GetFirstNonNullChildIndex(GreenNode node) {
        var n = node.slotCount;
        var firstIndex = 0;

        for (; firstIndex < n; firstIndex++) {
            var child = node.GetSlot(firstIndex);

            if (child is not null)
                break;
        }

        return firstIndex;
    }

    private static int GetLastNonNullChildIndex(GreenNode node) {
        var n = node.slotCount;
        var lastIndex = n - 1;

        for (; lastIndex >= 0; lastIndex--) {
            var child = node.GetSlot(lastIndex);

            if (child is not null)
                break;
        }

        return lastIndex;
    }

    private string GetDebuggerDisplay() {
        return GetType().Name + " " + kind + " " + ToString();
    }
}
