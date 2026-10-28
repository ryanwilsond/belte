using System;
using System.Collections.Generic;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

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
        return TryGetBinder(node, out var binder) ? binder : next.GetBinder(node);
    }

    private bool TryGetBinder(SyntaxNode node, out Binder binder) {
        if (_binderMap.TryGetValue(node, out binder)) {
            return true;
        } else if (node.location is CompoundTextLocation compound) {
            var current = compound.sourceNode;

            while (current is not null) {
                if (_binderMap.TryGetValue(current, out var nextBinder)) {
                    var insertedMap = LocalBinderFactory.BuildMap(
                        _memberSymbol,
                        node,
                        nextBinder,
                        _binderUpdatedHandler
                    );

                    return insertedMap.TryGetValue(node, out binder);
                }

                current = current.parent;
            }
        }

        binder = null;
        return false;
    }

    private void ComputeBinderMap() {
        Dictionary<SyntaxNode, Binder> map;

        if (_memberSymbol is SynthesizedEntryPoint entryPoint && _root == entryPoint.syntaxNode) {
            var scopeOwner = new SimpleProgramBinder(this, entryPoint);
            map = LocalBinderFactory.BuildMap(_memberSymbol, _root, scopeOwner, _binderUpdatedHandler);
            map.Add(_root, scopeOwner);
        } else {
            if (_memberSymbol is not null && _root is not null)
                map = LocalBinderFactory.BuildMap(_memberSymbol, _root, this, _binderUpdatedHandler);
            else
                map = [];
        }

        Interlocked.CompareExchange(ref _lazyBinderMap, map, null);
    }
}
