using Diagnostics;

namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

internal partial class SyntaxList {
    /// <summary>
    /// A <see cref="SyntaxList" /> with exactly two children.
    /// </summary>
    internal class WithTwoChildren : SyntaxList {
        private readonly GreenNode _child0;
        private readonly GreenNode _child1;

        internal WithTwoChildren(GreenNode child0, GreenNode child1) {
            this.AdjustFlagsAndWidth(child0);
            _child0 = child0;
            this.AdjustFlagsAndWidth(child1);
            _child1 = child1;
        }

        internal WithTwoChildren(GreenNode child0, GreenNode child1, Diagnostic[] diagnostics) : base(diagnostics) {
            this.AdjustFlagsAndWidth(child0);
            _child0 = child0;
            this.AdjustFlagsAndWidth(child1);
            _child1 = child1;
        }

        public override int slotCount => 2;

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

        internal override void CopyTo(ArrayElement<GreenNode>[] array, int offset) {
            array[offset].Value = _child0;
            array[offset + 1].Value = _child1;
        }

        internal override GreenNode SetDiagnostics(Diagnostic[] diagnostics) {
            return new WithTwoChildren(_child0, _child1, diagnostics);
        }
    }
}
