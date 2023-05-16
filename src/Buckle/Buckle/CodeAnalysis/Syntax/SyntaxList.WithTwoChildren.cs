
namespace Buckle.CodeAnalysis.Syntax;

public partial class SyntaxList : SyntaxNode {
    /// <summary>
    /// Represents a <see cref="SyntaxList" /> with exactly two children.
    /// </summary>
    public sealed class WithTwoChildren : SyntaxList {
        private SyntaxNode _child0;
        private SyntaxNode _child1;

        internal WithTwoChildren(SyntaxNode parent, InternalSyntax.SyntaxList green, int position)
            : base(parent, green, position) { }

        internal override SyntaxNode GetNodeSlot(int index) {
            switch (index) {
                case 0:
                    return GetRedElement(ref _child0, 0);
                case 1:
                    return GetRedElementIfNotToken(ref _child1);
                default:
                    return null;
            }
        }

        internal override SyntaxNode GetCachedSlot(int index) {
            switch (index) {
                case 0:
                    return _child0;
                case 1:
                    return _child1;
                default:
                    return null;
            }
        }
    }
}
