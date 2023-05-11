
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents a list of GreenNodes.
/// </summary>
internal sealed class SyntaxList : GreenNode {
    internal SyntaxList() : base((SyntaxKind)GreenNode.ListKind) { }

    internal SyntaxList(ArrayElement<GreenNode>[] children) : this() {
        this.children = children;
        InitializeChildren();
    }

    internal ArrayElement<GreenNode>[] children { get; }

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

    internal static SyntaxList List(ArrayElement<GreenNode>[] children) {
        // Can optimize here if needed by adding child classes for different sizes of lists
        return new SyntaxList(children);
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
    // TODO finish this
}
