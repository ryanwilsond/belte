using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BinderFactory {
    internal sealed class BinderFactoryVisitor : SyntaxVisitor<Binder> {
        private int _position;
        private BelteSyntaxNode _memberDeclaration;
        private Symbol _member;
        private BinderFactory _factory;

        internal void Initialize(
            BinderFactory factory,
            int position,
            BelteSyntaxNode memberDeclaration,
            Symbol member) {
            _factory = factory;
            _position = position;
            _memberDeclaration = memberDeclaration;
            _member = member;
        }

        internal void Clear() {
            _factory = null;
            _position = 0;
            _memberDeclaration = null;
            _member = null;
        }

        private Compilation compilation => _factory._compilation;

        private SyntaxTree syntaxTree => _factory.syntaxTree;

        private ConcurrentCache<BinderCacheKey, Binder> binderCache => _factory._binderCache;

        private bool inScript => _factory.inScript;

        internal override Binder DefaultVisit(SyntaxNode parent) {
            return ((BelteSyntaxNode)parent).parent.Accept(this);
        }

        internal override Binder Visit(SyntaxNode node) {
            return ((BelteSyntaxNode)node).Accept(this);
        }
    }
}
