
namespace Buckle.CodeAnalysis.Syntax;

internal class SyntaxList : SyntaxNode {
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

    internal sealed class SeparatedSyntaxList : SyntaxList {
        private new readonly ArrayElement<SyntaxNode?>[] _children;

        internal SeparatedSyntaxList(SyntaxNode parent, InternalSyntax.SyntaxList green, int position)
            : base(parent, green, position) {
            _children = new ArrayElement<SyntaxNode?>[(green.slotCount + 1) >> 1];
        }

        internal override SyntaxNode GetNodeSlot(int i) {
            if ((i & 1) != 0)
                return null;

            return GetRedElement(ref _children[i >> 1].Value, i);
        }

        internal override SyntaxNode? GetCachedSlot(int i) {
            if ((i & 1) != 0)
                return null;

            return _children[i >> 1].Value;
        }

        internal override int GetChildPosition(int index) {
            int valueIndex = (index & 1) != 0 ? index - 1 : index;

            if (valueIndex > 1
                && GetCachedSlot(valueIndex - 2) is null
                && (valueIndex >= green.slotCount - 2 || GetCachedSlot(valueIndex + 2) is { })) {
                return GetChildPositionFromEnd(index);
            }

            return GetChildPosition(index);
        }
    }
}
