using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed partial class DeclarationTable {
    internal static readonly DeclarationTable Empty = new DeclarationTable(
        allOlderRootDeclarations: ImmutableSetWithInsertionOrder<Lazy<RootSingleNamespaceDeclaration>>.Empty,
        latestLazyRootDeclaration: null,
        cache: null);

    private readonly ImmutableSetWithInsertionOrder<Lazy<RootSingleNamespaceDeclaration>> _allOlderRootDeclarations;
    private readonly Lazy<RootSingleNamespaceDeclaration> _latestLazyRootDeclaration;

    private readonly Cache _cache;

    private MergedNamespaceDeclaration _mergedRoot;

    private ICollection<string>? _typeNames;
    private ICollection<string>? _namespaceNames;

    private DeclarationTable(
        ImmutableSetWithInsertionOrder<Lazy<RootSingleNamespaceDeclaration>> allOlderRootDeclarations,
        Lazy<RootSingleNamespaceDeclaration>? latestLazyRootDeclaration,
        Cache cache) {
        _allOlderRootDeclarations = allOlderRootDeclarations;
        _latestLazyRootDeclaration = latestLazyRootDeclaration;
        _cache = cache ?? new Cache(this);
    }

    internal DeclarationTable AddRootDeclaration(Lazy<RootSingleNamespaceDeclaration> lazyRootDeclaration) {
        if (_latestLazyRootDeclaration is null) {
            return new DeclarationTable(_allOlderRootDeclarations, lazyRootDeclaration, _cache);
        } else {
            return new DeclarationTable(
                _allOlderRootDeclarations.Add(_latestLazyRootDeclaration),
                lazyRootDeclaration,
                cache: null
            );
        }
    }

    internal DeclarationTable RemoveRootDeclaration(Lazy<RootSingleNamespaceDeclaration> lazyRootDeclaration) {
        if (_latestLazyRootDeclaration == lazyRootDeclaration) {
            return new DeclarationTable(_allOlderRootDeclarations, latestLazyRootDeclaration: null, cache: _cache);
        } else {
            return new DeclarationTable(
                _allOlderRootDeclarations.Remove(lazyRootDeclaration),
                _latestLazyRootDeclaration,
                cache: null
            );
        }
    }

    internal MergedNamespaceDeclaration GetMergedRoot(Compilation compilation) {
        if (_mergedRoot is null)
            Interlocked.CompareExchange(ref _mergedRoot, CalculateMergedRoot(compilation), null);

        return _mergedRoot;
    }

    internal MergedNamespaceDeclaration CalculateMergedRoot(Compilation compilation) {
        var oldRoot = _cache.mergedRoot;

        if (_latestLazyRootDeclaration is null) {
            return oldRoot;
        } else if (oldRoot is null) {
            return MergedNamespaceDeclaration.Create(_latestLazyRootDeclaration.Value);
        } else {
            var oldRootDeclarations = oldRoot.declarations;
            var builder = ArrayBuilder<SingleNamespaceDeclaration>.GetInstance(oldRootDeclarations.Length + 1);
            builder.AddRange(oldRootDeclarations);
            builder.Add(_latestLazyRootDeclaration.Value);

            if (compilation is not null)
                builder.Sort(new RootNamespaceLocationComparer(compilation));

            return MergedNamespaceDeclaration.Create(builder.ToImmutableAndFree());
        }
    }

    private ICollection<string> GetMergedTypeNames() {
        var cachedTypeNames = _cache.typeNames;

        if (_latestLazyRootDeclaration == null) {
            return cachedTypeNames;
        } else {
            return UnionCollection<string>.Create(cachedTypeNames, GetTypeNames(_latestLazyRootDeclaration.Value));
        }
    }

    private ICollection<string> GetMergedNamespaceNames() {
        var cachedNamespaceNames = _cache.namespaceNames;

        if (_latestLazyRootDeclaration == null) {
            return cachedNamespaceNames;
        } else {
            return UnionCollection<string>.Create(cachedNamespaceNames, GetNamespaceNames(_latestLazyRootDeclaration.Value));
        }
    }

    private static readonly Predicate<Declaration> IsNamespacePredicate = d => d.kind == DeclarationKind.Namespace;
    private static readonly Predicate<Declaration> IsTypePredicate = d => d.kind != DeclarationKind.Namespace;

    private static ISet<string> GetTypeNames(Declaration declaration) {
        return GetNames(declaration, IsTypePredicate);
    }

    private static ISet<string> GetNamespaceNames(Declaration declaration) {
        return GetNames(declaration, IsNamespacePredicate);
    }

    private static ISet<string> GetNames(Declaration declaration, Predicate<Declaration> predicate) {
        var set = new HashSet<string>();
        var stack = new Stack<Declaration>();
        stack.Push(declaration);

        while (stack.Count > 0) {
            var current = stack.Pop();

            if (current is null)
                continue;

            if (predicate(current))
                set.Add(current.name);

            foreach (var child in current.children)
                stack.Push(child);
        }

        return SpecializedCollections.ReadOnlySet(set);
    }

    internal ICollection<string> typeNames {
        get {
            if (_typeNames is null)
                Interlocked.CompareExchange(ref _typeNames, GetMergedTypeNames(), comparand: null);

            return _typeNames;
        }
    }

    internal ICollection<string> namespaceNames {
        get {
            if (_namespaceNames is null)
                Interlocked.CompareExchange(ref _namespaceNames, GetMergedNamespaceNames(), comparand: null);

            return _namespaceNames;
        }
    }

    internal static bool ContainsName(
        MergedNamespaceDeclaration mergedRoot,
        string name,
        SymbolFilter filter,
        CancellationToken cancellationToken) {
        return ContainsNameHelper(
            mergedRoot,
            n => n == name,
            filter,
            t => t.memberNames.Value.Contains(name),
            cancellationToken
        );
    }

    internal static bool ContainsName(
        MergedNamespaceDeclaration mergedRoot,
        Func<string, bool> predicate,
        SymbolFilter filter,
        CancellationToken cancellationToken) {
        return ContainsNameHelper(
            mergedRoot, predicate, filter,
            t => {
                foreach (var name in t.memberNames.Value) {
                    if (predicate(name))
                        return true;
                }

                return false;
            }, cancellationToken
        );
    }

    private static bool ContainsNameHelper(
        MergedNamespaceDeclaration mergedRoot,
        Func<string, bool> predicate,
        SymbolFilter filter,
        Func<SingleTypeDeclaration, bool> typePredicate,
        CancellationToken cancellationToken) {
        var includeNamespace = (filter & SymbolFilter.Namespace) == SymbolFilter.Namespace;
        var includeType = (filter & SymbolFilter.Type) == SymbolFilter.Type;
        var includeMember = (filter & SymbolFilter.Member) == SymbolFilter.Member;

        var stack = new Stack<MergedNamespaceOrTypeDeclaration>();
        stack.Push(mergedRoot);

        while (stack.Count > 0) {
            cancellationToken.ThrowIfCancellationRequested();

            var current = stack.Pop();
            if (current == null) {
                continue;
            }

            if (current.kind == DeclarationKind.Namespace) {
                if (includeNamespace && predicate(current.name))
                    return true;
            } else {
                if (includeType && predicate(current.name))
                    return true;

                if (includeMember) {
                    var mergedType = (MergedTypeDeclaration)current;

                    foreach (var typeDecl in mergedType.declarations) {
                        if (typePredicate(typeDecl))
                            return true;
                    }
                }
            }

            foreach (var child in current.children) {
                if (child is MergedNamespaceOrTypeDeclaration childNamespaceOrType) {
                    if (includeMember || includeType || childNamespaceOrType.kind == DeclarationKind.Namespace)
                        stack.Push(childNamespaceOrType);
                }
            }
        }

        return false;
    }
}
