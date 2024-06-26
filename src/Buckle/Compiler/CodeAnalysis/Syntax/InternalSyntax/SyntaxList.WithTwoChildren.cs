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
            AdjustFlagsAndWidth(child0);
            _child0 = child0;
            AdjustFlagsAndWidth(child1);
            _child1 = child1;
        }

        internal WithTwoChildren(GreenNode child0, GreenNode child1, Diagnostic[] diagnostics) : base(diagnostics) {
            AdjustFlagsAndWidth(child0);
            _child0 = child0;
            AdjustFlagsAndWidth(child1);
            _child1 = child1;
        }

        public override int slotCount => 2;

        internal override GreenNode GetSlot(int index) {
            return index switch {
                0 => _child0,
                1 => _child1,
                _ => null,
            };
        }

        internal override SyntaxNode CreateRed(SyntaxNode parent, int position) {
            return new Syntax.SyntaxList.WithTwoChildren(parent, this, position);
        }

        internal override void CopyTo(ArrayElement<GreenNode>[] array, int offset) {
            array[offset].value = _child0;
            array[offset + 1].value = _child1;
        }

        internal override GreenNode SetDiagnostics(Diagnostic[] diagnostics) {
            return new WithTwoChildren(_child0, _child1, diagnostics);
        }
    }
}
