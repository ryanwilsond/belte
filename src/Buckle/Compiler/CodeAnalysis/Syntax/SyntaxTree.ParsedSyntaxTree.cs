using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Syntax;

public partial class SyntaxTree {
    private class ParsedSyntaxTree : SyntaxTree {
        private readonly BelteSyntaxNode _root;

        internal ParsedSyntaxTree(SourceText text, BelteSyntaxNode root, bool cloneRoot, SourceCodeKind kind)
            : base(text, kind) {
            _root = cloneRoot ? CloneNodeAsRoot(root) : root;
            endOfFile = _root.GetLastToken(true);
        }

        public override BelteSyntaxNode GetRoot() => _root;

        internal override SyntaxToken endOfFile { get; }

        private protected override int _length => _root?.fullSpan?.length ?? base._length;
    }
}
