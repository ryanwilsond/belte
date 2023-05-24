using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

public partial class SyntaxTree {
    private class ParsedSyntaxTree : SyntaxTree {
        private readonly BelteSyntaxNode _root;

        internal ParsedSyntaxTree(SourceText text, BelteSyntaxNode root, bool cloneRoot) : base(text) {
            _root = cloneRoot ? CloneNodeAsRoot(root) : root;
            endOfFile = _root.GetLastToken(true);
        }

        public override BelteSyntaxNode GetRoot() => _root;

        internal override SyntaxToken endOfFile { get; }

        protected override int length => _root?.fullSpan?.length ?? base.length;
    }
}
