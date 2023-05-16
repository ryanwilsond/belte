
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Represents a list of SyntaxNodes.
/// </summary>
public partial class SyntaxList : SyntaxNode {
    private readonly ArrayElement<SyntaxNode>[] _children;

    /// <summary>
    /// Creates a new <see cref="SyntaxList" /> from an <see cref="InternalSyntax.SyntaxList" />.
    /// </summary>
    internal SyntaxList(SyntaxNode parent, InternalSyntax.SyntaxList green, int position)
        : base(parent, green, position) {
        _children = new ArrayElement<SyntaxNode>[green.slotCount];
    }

    internal override SyntaxTree syntaxTree => parent.syntaxTree;

    internal override SyntaxNode GetNodeSlot(int index) {
        return GetRedElement(ref _children[index].Value, index);
    }

    internal override SyntaxNode GetCachedSlot(int index) {
        return _children[index];
    }
}
