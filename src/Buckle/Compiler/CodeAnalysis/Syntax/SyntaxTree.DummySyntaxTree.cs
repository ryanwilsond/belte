
namespace Buckle.CodeAnalysis.Syntax;

public partial class SyntaxTree {
    internal sealed class DummySyntaxTree : SyntaxTree {
        private readonly CompilationUnitSyntax _node;

        internal DummySyntaxTree() : base(null, SourceCodeKind.Regular) {
            _node = CloneNodeAsRoot(SyntaxFactory.ParseCompilationUnit(""));
        }

        public override BelteSyntaxNode GetRoot() {
            return _node;
        }
    }
}
