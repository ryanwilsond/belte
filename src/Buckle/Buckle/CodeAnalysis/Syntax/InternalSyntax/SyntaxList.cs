
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Represents a list of GreenNodes.
/// </summary>
internal partial class SyntaxList : GreenNode {
    internal SyntaxList() : base((SyntaxKind)GreenNode.ListKind) { }

    internal SyntaxList(ArrayElement<GreenNode>[] children) : this() {
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

    internal override GreenNode GetSlot(int index) {
        return children[index];
    }

    internal override SyntaxNode CreateRed(SyntaxNode parent, int position) {
        var separated = slotCount > 1 && HasNodeTokenPattern();

        return separated
            ? new Syntax.SyntaxList.SeparatedSyntaxList(parent, this, position)
            : (SyntaxNode)new Syntax.SyntaxList(parent, this, position);
    }

    private bool HasNodeTokenPattern() {
        for (int i = 0; i < slotCount; i++) {
            if (GetSlot(i).isToken == ((i & 1) == 0))
                return false;
        }

        return true;
    }
}
