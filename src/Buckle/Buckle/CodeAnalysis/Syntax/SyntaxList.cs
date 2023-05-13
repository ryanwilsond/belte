
namespace Buckle.CodeAnalysis.Syntax;

public partial class SyntaxList : SyntaxNode {
    private readonly ArrayElement<SyntaxNode>[] _children;

    internal SyntaxList(SyntaxNode parent, InternalSyntax.SyntaxList green, int position)
        : base(parent, green, position) {
        _children = new ArrayElement<SyntaxNode>[green.slotCount];
    }

    internal override SyntaxNode GetNodeSlot(int index) {
        return GetRedElement(ref _children[index].Value, index);
    }

    internal override SyntaxNode GetCachedSlot(int index) {
        return _children[index];
    }
}
