
using System;
using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents a list of GreenNodes.
/// </summary>
internal partial class SyntaxList : GreenNode {
    internal SyntaxList() : base(GreenNode.ListKind) { }

    internal SyntaxList(Diagnostic[] diagnostics) : base(GreenNode.ListKind, diagnostics) { }

    internal SyntaxList(ArrayElement<GreenNode>[] children) : this() {
        this.children = children;
        InitializeChildren();
    }

    internal SyntaxList(ArrayElement<GreenNode>[] children, Diagnostic[] diagnostics) : this(diagnostics) {
        this.children = children;
        InitializeChildren();
    }

    internal ArrayElement<GreenNode>[] children { get; }

    public override int slotCount => children.Length;

    internal static GreenNode List(GreenNode[] nodes) {
        return List(nodes, nodes.Length);
    }

    internal static GreenNode List(GreenNode[] nodes, int count) {
        var array = new ArrayElement<GreenNode>[count];

        for (int i = 0; i < count; i++) {
            var node = nodes[i];
            array[i].Value = node;
        }

        return List(array);
    }

    internal static WithTwoChildren List(GreenNode child0, GreenNode child1) {
        return new WithTwoChildren(child0, child1);
    }

    internal static SyntaxList List(ArrayElement<GreenNode>[] children) {
        // Can optimize here if needed by adding child classes for different sizes of lists
        return new SyntaxList(children);
    }

    internal static GreenNode Concat(GreenNode left, GreenNode right) {
        if (left == null)
            return right;

        if (right == null)
            return left;

        var leftList = left as SyntaxList;
        var rightList = right as SyntaxList;

        if (leftList != null) {
            if (rightList != null) {
                var tmp = new ArrayElement<GreenNode>[left.slotCount + right.slotCount];
                leftList.CopyTo(tmp, 0);
                rightList.CopyTo(tmp, left.slotCount);

                return List(tmp);
            } else {
                var tmp = new ArrayElement<GreenNode>[left.slotCount + 1];
                leftList.CopyTo(tmp, 0);
                tmp[left.slotCount].Value = right;

                return List(tmp);
            }
        } else if (rightList != null) {
            var tmp = new ArrayElement<GreenNode>[rightList.slotCount + 1];
            tmp[0].Value = left;
            rightList.CopyTo(tmp, 1);

            return List(tmp);
        } else {
            return List(left, right);
        }
    }

    internal virtual void CopyTo(ArrayElement<GreenNode>[] array, int offset) {
        Array.Copy(children, 0, array, offset, children.Length);
    }

    private void InitializeChildren() {
        int n = children.Length;

        if (n < byte.MaxValue)
            slotCount = (byte)n;
        else
            slotCount = byte.MaxValue;

        for (int i = 0; i < children.Length; i++)
            AdjustFlagsAndWidth(children[i]);
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

    private bool HasNodeTokenPattern() {
        for (int i = 0; i < slotCount; i++) {
            if (GetSlot(i).isToken == ((i & 1) == 0))
                return false;
        }

        return true;
    }
}
