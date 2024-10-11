using System;
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

        private EndBinder endBinder => _factory._endBinder;

        private bool inScript => _factory.inScript;

        internal override Binder DefaultVisit(SyntaxNode parent) {
            return ((BelteSyntaxNode)parent).parent.Accept(this);
        }

        internal override Binder Visit(SyntaxNode node) {
            return ((BelteSyntaxNode)node).Accept(this);
        }

        internal override Binder VisitCompilationUnit(CompilationUnitSyntax node) {
            if (node != syntaxTree.GetRoot())
                throw new ArgumentOutOfRangeException(nameof(node), "node is not apart of the tree");

            var key = new BinderCacheKey(node, inScript ? NodeUsage.CompilationUnitScript : NodeUsage.Normal);

            if (!binderCache.TryGetValue(key, out var result)) {
                result = endBinder;

                if (inScript) {
                    // TODO
                } else {
                    var globalNamespace = compilation.globalNamespace;
                    result = new InContainerBinder(globalNamespace, result);
                }

                binderCache.TryAdd(key, result);
            }

            return result;
        }
    }
}