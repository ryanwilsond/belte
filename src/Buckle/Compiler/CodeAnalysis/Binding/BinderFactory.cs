using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BinderFactory {
    private readonly ConcurrentCache<BinderCacheKey, Binder> _binderCache;
    private readonly Compilation _compilation;
    private readonly ObjectPool<BinderFactoryVisitor> _binderFactoryVisitorPool;
    private readonly EndBinder _endBinder;
    private readonly bool _ignoreAccessibility;

    private static readonly ObjectPool<BinderFactoryVisitor> BinderFactoryVisitorPool
        = new ObjectPool<BinderFactoryVisitor>(static () => new BinderFactoryVisitor(), 64);

    internal BinderFactory(
        Compilation compilation,
        SyntaxTree syntaxTree,
        bool ignoreAccessibility,
        ObjectPool<BinderFactoryVisitor> binderFactoryVisitorPool = null) {
        _compilation = compilation;
        this.syntaxTree = syntaxTree;
        _binderFactoryVisitorPool = binderFactoryVisitorPool ?? BinderFactoryVisitorPool;
        // 50 is most likely more than ever needed before collected
        _binderCache = new ConcurrentCache<BinderCacheKey, Binder>(50);
        _endBinder = new EndBinder(compilation, syntaxTree.text);
        _ignoreAccessibility = ignoreAccessibility;
    }

    internal SyntaxTree syntaxTree { get; }

    internal bool inScript => _compilation.options.isScript;

    internal Binder GetBinder(SyntaxNode node, BelteSyntaxNode memberDeclaration = null, Symbol member = null) {
        var position = node.span.start;

        if ((!_compilation.options.isScript || node.kind != SyntaxKind.CompilationUnit) && node.parent is not null)
            node = node.parent;

        return GetBinder(node, position, memberDeclaration, member);
    }

    internal Binder GetBinder(SyntaxNode node, int position, BelteSyntaxNode memberDeclaration, Symbol member) {
        var visitor = GetBinderFactoryVisitor(position, memberDeclaration, member);
        var result = visitor.Visit(node);
        ClearBinderFactoryVisitor(visitor);
        return result;
    }

    internal Binder GetInTypeBodyBinder(TypeDeclarationSyntax syntax) {
        var visitor = GetBinderFactoryVisitor(syntax.span.start, null, null);
        var resultBinder = visitor.VisitTypeDeclarationCore(syntax, NodeUsage.NamedTypeBodyOrTemplateParameters);
        ClearBinderFactoryVisitor(visitor);
        return resultBinder;
    }

    internal Binder GetInNamespaceBinder(BelteSyntaxNode unit) {
        switch (unit.kind) {
            case SyntaxKind.CompilationUnit:
                var visitor = GetBinderFactoryVisitor(0, null, null);
                var result = visitor.VisitCompilationUnit((CompilationUnitSyntax)unit);
                ClearBinderFactoryVisitor(visitor);
                return result;
            default:
                return null;
        }
    }

    private BinderFactoryVisitor GetBinderFactoryVisitor(
        int position,
        BelteSyntaxNode memberDeclaration,
        Symbol member) {
        var visitor = _binderFactoryVisitorPool.Allocate();
        visitor.Initialize(this, position, memberDeclaration, member);
        return visitor;
    }

    private void ClearBinderFactoryVisitor(BinderFactoryVisitor visitor) {
        visitor.Clear();
        _binderFactoryVisitorPool.Free(visitor);
    }
}
