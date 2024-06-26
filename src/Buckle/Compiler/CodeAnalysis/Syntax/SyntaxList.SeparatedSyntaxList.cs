
namespace Buckle.CodeAnalysis.Syntax;

public partial class SyntaxList {
    /// <summary>
    /// Represents a <see cref="SyntaxList" /> with separator tokens every other slot, starting on the second slot.
    /// </summary>
    public sealed class SeparatedSyntaxList : SyntaxList {
        private new readonly ArrayElement<SyntaxNode?>[] _children;

        internal SeparatedSyntaxList(SyntaxNode parent, InternalSyntax.SyntaxList green, int position)
            : base(parent, green, position) {
            _children = new ArrayElement<SyntaxNode?>[(green.slotCount + 1) >> 1];
        }

        internal override SyntaxNode GetNodeSlot(int i) {
            if ((i & 1) != 0)
                return null;

            return GetRedElement(ref _children[i >> 1].value, i);
        }

        internal override SyntaxNode? GetCachedSlot(int i) {
            if ((i & 1) != 0)
                return null;

            return _children[i >> 1].value;
        }

        internal override int GetChildPosition(int index) {
            var valueIndex = (index & 1) != 0 ? index - 1 : index;

            if (valueIndex > 1
                && GetCachedSlot(valueIndex - 2) is null
                && (valueIndex >= green.slotCount - 2 || GetCachedSlot(valueIndex + 2) is { })) {
                return GetChildPositionFromEnd(index);
            }

            return base.GetChildPosition(index);
        }
    }
}
