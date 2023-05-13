
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal partial class SyntaxList {
    internal class WithTwoChildren : SyntaxList {
        private readonly GreenNode _child0;
        private readonly GreenNode _child1;

        internal WithTwoChildren(GreenNode child0, GreenNode child1) {
            slotCount = 2;
            this.AdjustFlagsAndWidth(child0);
            _child0 = child0;
            this.AdjustFlagsAndWidth(child1);
            _child1 = child1;
        }

        internal override GreenNode GetSlot(int index) {
            switch (index) {
                case 0:
                    return _child0;
                case 1:
                    return _child1;
                default:
                    return null;
            }
        }

        internal override SyntaxNode CreateRed(SyntaxNode parent, int position) {
            return new Syntax.SyntaxList.WithTwoChildren(parent, this, position);
        }
    }
}
