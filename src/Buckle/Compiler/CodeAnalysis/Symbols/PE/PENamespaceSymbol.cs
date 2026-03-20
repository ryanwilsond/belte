using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class PENamespaceSymbol : NamespaceSymbol {
    private protected Dictionary<ReadOnlyMemory<char>, PENestedNamespaceSymbol> _lazyNamespaces;
    private protected Dictionary<ReadOnlyMemory<char>, ImmutableArray<PENamedTypeSymbol>> _lazyTypes;
    private Dictionary<string, TypeDefinitionHandle> _lazyNoPiaLocalTypes;
    private ImmutableArray<PENamedTypeSymbol> _lazyFlattenedTypes;
    private ImmutableArray<Symbol> _lazyFlattenedNamespacesAndTypes;

    // TODO Using containing assembly instead of module directly for better interop
    internal sealed override NamespaceExtent extent => new NamespaceExtent(containingPEModule.containingAssembly);

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal sealed override ImmutableArray<TextLocation> locations
        => containingPEModule.metadataLocation.Cast<MetadataLocation, TextLocation>();

    internal sealed override TextLocation location => locations[0];

    internal abstract PEModuleSymbol containingPEModule { get; }

    internal sealed override ImmutableArray<Symbol> GetMembers() {
        if (_lazyFlattenedNamespacesAndTypes.IsDefault) {
            EnsureAllMembersLoaded();
            ImmutableInterlocked.InterlockedExchange(ref _lazyFlattenedNamespacesAndTypes, CalculateMembers());
        }

        return _lazyFlattenedNamespacesAndTypes;

        ImmutableArray<Symbol> CalculateMembers() {
            var memberTypes = GetMemberTypesPrivate();

            if (_lazyNamespaces.Count == 0)
                return StaticCast<Symbol>.From(memberTypes);

            var builder = ArrayBuilder<Symbol>.GetInstance(memberTypes.Length + _lazyNamespaces.Count);

            builder.AddRange(memberTypes);

            foreach (var pair in _lazyNamespaces)
                builder.Add(pair.Value);

            return builder.ToImmutableAndFree();
        }
    }

    private ImmutableArray<NamedTypeSymbol> GetMemberTypesPrivate() {
        if (_lazyFlattenedTypes.IsDefault) {
            var flattened = _lazyTypes.Flatten();
            ImmutableInterlocked.InterlockedExchange(ref _lazyFlattenedTypes, flattened);
        }

        return StaticCast<NamedTypeSymbol>.From(_lazyFlattenedTypes);
    }

    internal override NamespaceSymbol GetNestedNamespace(ReadOnlyMemory<char> name) {
        EnsureAllMembersLoaded();

        if (_lazyNamespaces.TryGetValue(name, out var ns))
            return ns;

        return null;
    }

    internal sealed override ImmutableArray<Symbol> GetMembers(ReadOnlyMemory<char> name) {
        EnsureAllMembersLoaded();

        ImmutableArray<PENamedTypeSymbol> t;

        if (_lazyNamespaces.TryGetValue(name, out var ns)) {
            if (_lazyTypes.TryGetValue(name, out t))
                return StaticCast<Symbol>.From(t).Add(ns);
            else
                return [ns];
        } else if (_lazyTypes.TryGetValue(name, out t)) {
            return StaticCast<Symbol>.From(t);
        }

        return [];
    }

    internal sealed override ImmutableArray<NamedTypeSymbol> GetTypeMembers() {
        EnsureAllMembersLoaded();

        return GetMemberTypesPrivate();
    }

    internal sealed override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name) {
        EnsureAllMembersLoaded();

        return _lazyTypes.TryGetValue(name, out var t)
            ? StaticCast<NamedTypeSymbol>.From(t)
            : [];
    }

    internal sealed override ImmutableArray<NamedTypeSymbol> GetTypeMembers(ReadOnlyMemory<char> name, int arity) {
        return GetTypeMembers(name).WhereAsArray((type, arity) => type.arity == arity, arity);
    }

    private protected abstract void EnsureAllMembersLoaded();

    private protected void LoadAllMembers(IEnumerable<IGrouping<string, TypeDefinitionHandle>> typesByNS) {
        MetadataHelpers.GetInfoForImmediateNamespaceMembers(
            isGlobalNamespace,
            isGlobalNamespace ? 0 : GetQualifiedNameLength(),
            typesByNS,
            StringComparer.Ordinal,
            out var nestedTypes, out var nestedNamespaces);

        LazyInitializeNamespaces(nestedNamespaces);
        LazyInitializeTypes(nestedTypes);
    }

    private int GetQualifiedNameLength() {
        var length = name.Length;
        var parent = containingNamespace;

        while (parent?.isGlobalNamespace == false) {
            length += parent.name.Length + 1;
            parent = parent.containingNamespace;
        }

        return length;
    }

    private void LazyInitializeNamespaces(
        IEnumerable<KeyValuePair<string, IEnumerable<IGrouping<string, TypeDefinitionHandle>>>> childNamespaces) {
        if (_lazyNamespaces is null) {
            var namespaces = new Dictionary<ReadOnlyMemory<char>, PENestedNamespaceSymbol>(
                ReadOnlyMemoryOfCharComparer.Instance
            );

            foreach (var child in childNamespaces) {
                var c = new PENestedNamespaceSymbol(child.Key, this, child.Value);
                namespaces.Add(c.name.AsMemory(), c);
            }

            Interlocked.CompareExchange(ref _lazyNamespaces, namespaces, null);
        }
    }

    private void LazyInitializeTypes(IEnumerable<IGrouping<string, TypeDefinitionHandle>> typeGroups) {
        if (_lazyTypes is null) {
            var moduleSymbol = containingPEModule;

            var children = ArrayBuilder<PENamedTypeSymbol>.GetInstance();
            var skipCheckForPiaType = !moduleSymbol.module.ContainsNoPiaLocalTypes();
            Dictionary<string, TypeDefinitionHandle> noPiaLocalTypes = null;

            foreach (var g in typeGroups) {
                foreach (var t in g) {
                    if (skipCheckForPiaType || !moduleSymbol.module.IsNoPiaLocalType(t)) {
                        children.Add(PENamedTypeSymbol.Create(moduleSymbol, this, t, g.Key));
                    } else {
                        try {
                            var typeDefName = moduleSymbol.module.GetTypeDefNameOrThrow(t);

                            noPiaLocalTypes ??= new Dictionary<string, TypeDefinitionHandle>(
                                StringOrdinalComparer.Instance
                            );

                            noPiaLocalTypes[typeDefName] = t;
                        } catch (BadImageFormatException) { }
                    }
                }
            }

            var typesDict = children.ToDictionary(c => c.name.AsMemory(), ReadOnlyMemoryOfCharComparer.Instance);
            children.Free();

            if (noPiaLocalTypes is not null)
                Interlocked.CompareExchange(ref _lazyNoPiaLocalTypes, noPiaLocalTypes, null);

            var original = Interlocked.CompareExchange(ref _lazyTypes, typesDict, null);

            if (original is null)
                moduleSymbol.OnNewTypeDeclarationsLoaded(typesDict);
        }
    }

    internal NamedTypeSymbol UnifyIfNoPiaLocalType(ref MetadataTypeName emittedTypeName) {
        EnsureAllMembersLoaded();

        if (_lazyNoPiaLocalTypes is not null &&
            _lazyNoPiaLocalTypes.TryGetValue(emittedTypeName.typeName, out var typeDef)) {
            return (NamedTypeSymbol)new MetadataDecoder(containingPEModule).GetTypeOfToken(typeDef, out _);
        }

        return null;
    }
}
