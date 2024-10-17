using System;
using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents a list of GreenNodes.
/// </summary>
internal partial class SyntaxList : GreenNode {
    /// <summary>
    /// Creates an empty <see cref="SyntaxList" />.
    /// </summary>
    internal SyntaxList() : base(ListKind) { }

    /// <summary>
    /// Creates an empty <see cref="SyntaxList" /> with diagnostics.
    /// </summary>
    internal SyntaxList(Diagnostic[] diagnostics) : base(ListKind, diagnostics) { }

    /// <summary>
    /// Creates a new <see cref="SyntaxList" /> with children.
    /// </summary>
    internal SyntaxList(ArrayElement<GreenNode>[] children) : this() {
        this.children = children;
        InitializeChildren();
    }

    /// <summary>
    /// Creates a new <see cref="SyntaxList" /> with children and diagnostics.
    /// </summary>
    internal SyntaxList(ArrayElement<GreenNode>[] children, Diagnostic[] diagnostics) : this(diagnostics) {
        this.children = children;
        InitializeChildren();
    }

    /// <summary>
    /// All items in this list.
    /// </summary>
    internal ArrayElement<GreenNode>[] children { get; }

    public override int slotCount => children.Length;

    /// <summary>
    /// Converts an array of GreenNodes into a <see cref="SyntaxList" />.
    /// </summary>
    internal static GreenNode List(GreenNode[] nodes) {
        return List(nodes, nodes.Length);
    }

    /// <summary>
    /// Converts an array of GreenNodes into a <see cref="SyntaxList" />.
    /// </summary>
    internal static GreenNode List(GreenNode[] nodes, int count) {
        var array = new ArrayElement<GreenNode>[count];

        for (var i = 0; i < count; i++) {
            var node = nodes[i];
            array[i].value = node;
        }

        return List(array);
    }

    /// <summary>
    /// Creates a <see cref="SyntaxList" /> with exactly two children.
    /// </summary>
    internal static WithTwoChildren List(GreenNode child0, GreenNode child1) {
        return new WithTwoChildren(child0, child1);
    }

    /// <summary>
    /// Creates a <see cref="SyntaxList" /> with children.
    /// </summary>
    internal static SyntaxList List(ArrayElement<GreenNode>[] children) {
        return new SyntaxList(children);
    }

    /// <summary>
    /// Joins together two SyntaxLists.
    /// </summary>
    internal static GreenNode Concat(GreenNode left, GreenNode right) {
        if (left is null)
            return right;

        if (right is null)
            return left;

        var rightList = right as SyntaxList;

        if (left is SyntaxList leftList) {
            if (rightList is not null) {
                var tmp = new ArrayElement<GreenNode>[left.slotCount + right.slotCount];
                leftList.CopyTo(tmp, 0);
                rightList.CopyTo(tmp, left.slotCount);

                return List(tmp);
            } else {
                var tmp = new ArrayElement<GreenNode>[left.slotCount + 1];
                leftList.CopyTo(tmp, 0);
                tmp[left.slotCount].value = right;

                return List(tmp);
            }
        } else if (rightList is not null) {
            var tmp = new ArrayElement<GreenNode>[rightList.slotCount + 1];
            tmp[0].value = left;
            rightList.CopyTo(tmp, 1);

            return List(tmp);
        } else {
            return List(left, right);
        }
    }

    internal override GreenNode GetSlot(int index) {
        return children[index];
    }

    internal override SyntaxNode CreateRed(SyntaxNode parent, int position) {
        var separated = slotCount > 1 && HasNodeTokenPattern();

        return separated
            ? new Syntax.SyntaxList.SeparatedSyntaxList(parent, this, position)
            : (SyntaxNode)new Syntax.SyntaxList(parent, this, position);
    }

    internal override GreenNode SetDiagnostics(Diagnostic[] diagnostics) {
        return new SyntaxList(children, diagnostics);
    }

    internal virtual void CopyTo(ArrayElement<GreenNode>[] array, int offset) {
        Array.Copy(children, 0, array, offset, children.Length);
    }

    private void InitializeChildren() {
        var n = children.Length;

        if (n < byte.MaxValue)
            slotCount = (byte)n;
        else
            slotCount = byte.MaxValue;

        for (var i = 0; i < children.Length; i++)
            AdjustFlagsAndWidth(children[i]);
    }

    private bool HasNodeTokenPattern() {
        for (var i = 0; i < slotCount; i++) {
            if (GetSlot(i).isToken == ((i & 1) == 0))
                return false;
        }

        return true;
    }
}
