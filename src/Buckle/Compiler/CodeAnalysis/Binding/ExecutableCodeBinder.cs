using System;
using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class ExecutableCodeBinder : Binder {
    private readonly Symbol _memberSymbol;
    private readonly SyntaxNode _root;
    private readonly Action<Binder, SyntaxNode> _binderUpdatedHandler;

    private Dictionary<SyntaxNode, Binder> _lazyBinderMap;

    internal ExecutableCodeBinder(
        SyntaxNode root,
        Symbol memberSymbol,
        Binder next,
        Action<Binder, SyntaxNode> binderUpdatedHandler = null)
        : this(root, memberSymbol, next, next.flags) {
        _binderUpdatedHandler = binderUpdatedHandler;
    }

    internal ExecutableCodeBinder(SyntaxNode root, Symbol memberSymbol, Binder next, BinderFlags additionalFlags)
        : base(next, (next.flags | additionalFlags) & ~BinderFlags.AllClearedAtExecutableCodeBoundary) {
        _memberSymbol = memberSymbol;
        _root = root;
    }

    internal override Symbol containingMember => _memberSymbol ?? next.containingMember;

    internal Symbol memberSymbol => _memberSymbol;

    private protected override bool _inExecutableBinder => true;

    private Dictionary<SyntaxNode, Binder> _binderMap {
        get {
            if (_lazyBinderMap is null)
                ComputeBinderMap();

            return _lazyBinderMap;
        }
    }

    internal override Binder GetBinder(SyntaxNode node) {
        return _binderMap.TryGetValue(node, out var binder) ? binder : next.GetBinder(node);
    }

    private void ComputeBinderMap() {
        Dictionary<SyntaxNode, Binder> map;

        if (_memberSymbol is not null && _root is not null)
            map = LocalBinderFactory.BuildMap(_memberSymbol, _root, this, _binderUpdatedHandler);
        else
            map = [];

        Interlocked.CompareExchange(ref _lazyBinderMap, map, null);
    }
}
