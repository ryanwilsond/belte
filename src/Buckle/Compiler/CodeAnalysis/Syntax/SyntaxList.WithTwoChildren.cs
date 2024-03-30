
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
            return index switch {
                0 => GetRedElement(ref _child0, 0),
                1 => GetRedElementIfNotToken(ref _child1),
                _ => null,
            };
        }

        internal override SyntaxNode GetCachedSlot(int index) {
            return index switch {
                0 => _child0,
                1 => _child1,
                _ => null,
            };
        }
    }
}
