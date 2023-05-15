using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

public partial class SyntaxTree {
    private class ParsedSyntaxTree : SyntaxTree {
        private readonly BelteSyntaxNode _root;

        internal ParsedSyntaxTree(SourceText text, BelteSyntaxNode root, bool cloneRoot) : base(text) {
            _root = cloneRoot ? CloneNodeAsRoot(root) : root;
        }

        protected override int length => _root?.fullSpan?.length ?? base.length;

        public override BelteSyntaxNode GetRoot() => _root;
    }
}
