using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed class MergedNamespaceDeclaration : MergedNamespaceOrTypeDeclaration {
    private ImmutableArray<MergedNamespaceOrTypeDeclaration> _lazyChildren;

    private MergedNamespaceDeclaration(ImmutableArray<SingleNamespaceDeclaration> declarations)
        : base(declarations.IsEmpty ? string.Empty : declarations[0].name) {
        this.declarations = declarations;
    }

    internal static MergedNamespaceDeclaration Create(ImmutableArray<SingleNamespaceDeclaration> declarations) {
        return new MergedNamespaceDeclaration(declarations);
    }

    internal static MergedNamespaceDeclaration Create(SingleNamespaceDeclaration declaration) {
        return new MergedNamespaceDeclaration(ImmutableArray.Create(declaration));
    }

    internal override DeclarationKind kind => DeclarationKind.Namespace;

    internal LexicalSortKey GetLexicalSortKey(Compilation compilation) {
        var sortKey = new LexicalSortKey(
            declarations[0].syntaxReference.syntaxTree,
            declarations[0].nameLocation,
            compilation
        );

        for (var i = 1; i < declarations.Length; i++) {
            sortKey = LexicalSortKey.First(sortKey, new LexicalSortKey(
                declarations[i].syntaxReference.syntaxTree,
                declarations[i].nameLocation,
                compilation
            ));
        }

        return sortKey;
    }

    internal ImmutableArray<TextLocation> nameLocations {
        get {
            if (declarations.Length == 1) {
                return [declarations[0].nameLocation];
            } else {
                var builder = ArrayBuilder<TextLocation>.GetInstance();

                foreach (var decl in declarations) {
                    var loc = decl.nameLocation;

                    if (loc is not null)
                        builder.Add(loc);
                }

                return builder.ToImmutableAndFree();
            }
        }
    }

    internal ImmutableArray<SingleNamespaceDeclaration> declarations { get; }

    internal new ImmutableArray<MergedNamespaceOrTypeDeclaration> children {
        get {
            if (_lazyChildren.IsDefault)
                ImmutableInterlocked.InterlockedInitialize(ref _lazyChildren, MakeChildren());

            return _lazyChildren;
        }
    }

    private protected override ImmutableArray<Declaration> GetDeclarationChildren() {
        return StaticCast<Declaration>.From(children);
    }

    private ImmutableArray<MergedNamespaceOrTypeDeclaration> MakeChildren() {
        ArrayBuilder<SingleNamespaceDeclaration> namespaces = null;
        ArrayBuilder<SingleTypeDeclaration> types = null;
        var allNamespacesHaveSameName = true;
        var allTypesHaveSameIdentity = true;

        foreach (var decl in declarations) {
            foreach (var child in decl.children) {
                if (child is SingleTypeDeclaration asType) {
                    if (types is null)
                        types = ArrayBuilder<SingleTypeDeclaration>.GetInstance();
                    else if (allTypesHaveSameIdentity && !asType.identity.Equals(types[0].identity))
                        allTypesHaveSameIdentity = false;

                    types.Add(asType);
                    continue;
                }

                if (child is SingleNamespaceDeclaration asNamespace) {
                    if (namespaces is null)
                        namespaces = ArrayBuilder<SingleNamespaceDeclaration>.GetInstance();
                    else if (allNamespacesHaveSameName && !asNamespace.name.Equals(namespaces[0].name))
                        allNamespacesHaveSameName = false;

                    namespaces.Add(asNamespace);
                    continue;
                }
            }
        }

        var children = ArrayBuilder<MergedNamespaceOrTypeDeclaration>.GetInstance();

        AddNamespacesToChildren(namespaces, allNamespacesHaveSameName, children);
        AddTypesToChildren(types, allTypesHaveSameIdentity, children);

        return children.ToImmutableAndFree();

        static void AddNamespacesToChildren(
            ArrayBuilder<SingleNamespaceDeclaration> namespaces,
            bool allNamespacesHaveSameName,
            ArrayBuilder<MergedNamespaceOrTypeDeclaration> children) {
            if (namespaces is not null) {
                if (allNamespacesHaveSameName) {
                    children.Add(Create(namespaces.ToImmutableAndFree()));
                } else {
                    var namespaceGroups = new Dictionary<string, ArrayBuilder<SingleNamespaceDeclaration>>(
                        StringOrdinalComparer.Instance
                    );

                    foreach (var n in namespaces) {
                        var builder = namespaceGroups.GetOrAdd(
                            n.name,
                            static () => ArrayBuilder<SingleNamespaceDeclaration>.GetInstance()
                        );

                        builder.Add(n);
                    }

                    namespaces.Free();

                    foreach (var (_, namespaceGroup) in namespaceGroups)
                        children.Add(Create(namespaceGroup.ToImmutableAndFree()));
                }
            }
        }

        static void AddTypesToChildren(
            ArrayBuilder<SingleTypeDeclaration> types,
            bool allTypesHaveSameIdentity,
            ArrayBuilder<MergedNamespaceOrTypeDeclaration> children) {
            if (types is not null) {
                if (allTypesHaveSameIdentity) {
                    children.Add(new MergedTypeDeclaration(types.ToImmutableAndFree()));
                } else {
                    var typeGroups = PooledDictionary<SingleTypeDeclaration.TypeDeclarationIdentity, object>
                        .GetInstance();

                    foreach (var t in types) {
                        var id = t.identity;

                        if (typeGroups.TryGetValue(id, out var existingValue)) {
                            if (existingValue is not ArrayBuilder<SingleTypeDeclaration> builder) {
                                builder = ArrayBuilder<SingleTypeDeclaration>.GetInstance();
                                builder.Add((SingleTypeDeclaration)existingValue);
                                typeGroups[id] = builder;
                            }

                            builder.Add(t);
                        } else {
                            typeGroups.Add(id, t);
                        }
                    }

                    foreach (var (_, typeGroup) in typeGroups) {
                        if (typeGroup is SingleTypeDeclaration t) {
                            children.Add(new MergedTypeDeclaration([t]));
                        } else {
                            var builder = (ArrayBuilder<SingleTypeDeclaration>)typeGroup;
                            children.Add(new MergedTypeDeclaration(builder.ToImmutableAndFree()));
                        }
                    }

                    types.Free();
                    typeGroups.Free();
                }
            }
        }
    }
}
