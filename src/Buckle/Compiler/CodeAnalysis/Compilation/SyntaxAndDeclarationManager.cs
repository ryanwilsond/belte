using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed partial class SyntaxAndDeclarationManager {
    private static readonly ObjectPool<Stack<SingleNamespaceOrTypeDeclaration>> DeclarationStack =
        new ObjectPool<Stack<SingleNamespaceOrTypeDeclaration>>(() => new Stack<SingleNamespaceOrTypeDeclaration>());

    private State _lazyState;

    internal SyntaxAndDeclarationManager(ImmutableArray<SyntaxTree> syntaxTrees, State state) {
        this.syntaxTrees = syntaxTrees;
        _lazyState = state;
    }

    internal ImmutableArray<SyntaxTree> syntaxTrees { get; }

    internal State state {
        get {
            if (_lazyState is null) {
                var newState = CreateState(syntaxTrees);
                Interlocked.CompareExchange(ref _lazyState, newState, null);
            }

            return _lazyState;
        }
    }

    private static State CreateState(ImmutableArray<SyntaxTree> syntaxTrees) {
        var treesBuilder = ArrayBuilder<SyntaxTree>.GetInstance();
        var ordinalMapBuilder = PooledDictionary<SyntaxTree, int>.GetInstance();
        var loadedSyntaxTreeMapBuilder = PooledDictionary<string, SyntaxTree>.GetInstance();
        var declMapBuilder = PooledDictionary<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>>.GetInstance();
        var lastComputedMemberNamesMap = PooledDictionary<
            SyntaxTree,
            OneOrMany<WeakReference<StrongBox<ImmutableSegmentedHashSet<string>>>>>.GetInstance();
        var declTable = DeclarationTable.Empty;

        foreach (var tree in syntaxTrees) {
            AppendAllSyntaxTrees(
                treesBuilder,
                tree,
                ordinalMapBuilder,
                loadedSyntaxTreeMapBuilder,
                declMapBuilder,
                lastComputedMemberNamesMap,
                ref declTable
            );
        }

        return new State(
            treesBuilder.ToImmutableAndFree(),
            ordinalMapBuilder.ToImmutableDictionaryAndFree(),
            loadedSyntaxTreeMapBuilder.ToImmutableDictionaryAndFree(),
            declMapBuilder.ToImmutableDictionaryAndFree(),
            lastComputedMemberNamesMap.ToImmutableDictionaryAndFree(),
            declTable
        );
    }

    internal SyntaxAndDeclarationManager AddSyntaxTrees(IEnumerable<SyntaxTree> trees) {
        var state = _lazyState;
        var newSyntaxTrees = syntaxTrees.AddRange(trees);

        if (state is null)
            return WithSyntaxTrees(newSyntaxTrees);

        var ordinalMapBuilder = state.ordinalMap.ToBuilder();
        var loadedSyntaxTreeMapBuilder = state.loadedSyntaxTreeMap.ToBuilder();
        var declMapBuilder = state.rootNamespaces.ToBuilder();
        var lastComputedMemberNamesMap = state.lastComputedMemberNames.ToBuilder();
        var declTable = state.declarationTable;
        var treesBuilder = ArrayBuilder<SyntaxTree>.GetInstance();
        treesBuilder.AddRange(state.syntaxTrees);

        foreach (var tree in trees) {
            AppendAllSyntaxTrees(
                treesBuilder,
                tree,
                ordinalMapBuilder,
                loadedSyntaxTreeMapBuilder,
                declMapBuilder,
                lastComputedMemberNamesMap,
                ref declTable
            );
        }

        state = new State(
            treesBuilder.ToImmutableAndFree(),
            ordinalMapBuilder.ToImmutableDictionary(),
            loadedSyntaxTreeMapBuilder.ToImmutableDictionary(),
            declMapBuilder.ToImmutableDictionary(),
            lastComputedMemberNamesMap.ToImmutableDictionary(),
            declTable
        );

        return new SyntaxAndDeclarationManager(newSyntaxTrees, state);
    }

    private SyntaxAndDeclarationManager WithSyntaxTrees(ImmutableArray<SyntaxTree> trees) {
        return new SyntaxAndDeclarationManager(trees, null);
    }

    private static void AppendAllSyntaxTrees(
        ArrayBuilder<SyntaxTree> treesBuilder,
        SyntaxTree tree,
        IDictionary<SyntaxTree, int> ordinalMapBuilder,
        IDictionary<string, SyntaxTree> loadedSyntaxTreeMapBuilder,
        IDictionary<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>> declMapBuilder,
        IDictionary<SyntaxTree, OneOrMany<WeakReference<StrongBox<ImmutableSegmentedHashSet<string>>>>>
            lastComputedMemberNamesMap,
        ref DeclarationTable declTable) {
        var sourceCodeKind = tree.kind;

        // TODO Consider for scripts?
        // if (sourceCodeKind == SourceCodeKind.Script) {
        //     AppendAllLoadedSyntaxTrees(
        //         treesBuilder,
        //         tree,
        //         ordinalMapBuilder,
        //         loadedSyntaxTreeMapBuilder,
        //         declMapBuilder,
        //         lastComputedMemberNamesMap,
        //         ref declTable
        //     );
        // }

        AddSyntaxTreeToDeclarationMapAndTable(
            tree,
            declMapBuilder,
            lastComputedMemberNames: OneOrMany<WeakReference<StrongBox<ImmutableSegmentedHashSet<string>>>>.Empty,
            ref declTable
        );

        treesBuilder.Add(tree);
        ordinalMapBuilder.Add(tree, ordinalMapBuilder.Count);

        lastComputedMemberNamesMap.Add(
            tree,
            OneOrMany<WeakReference<StrongBox<ImmutableSegmentedHashSet<string>>>>.Empty
        );
    }

    private static void AddSyntaxTreeToDeclarationMapAndTable(
        SyntaxTree tree,
        IDictionary<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>> declMapBuilder,
        OneOrMany<WeakReference<StrongBox<ImmutableSegmentedHashSet<string>>>> lastComputedMemberNames,
        ref DeclarationTable declTable) {
        var lazyRoot = new Lazy<RootSingleNamespaceDeclaration>(
            () => DeclarationTreeBuilder.ForTree(tree, lastComputedMemberNames)
        );

        declMapBuilder.Add(tree, lazyRoot);
        declTable = declTable.AddRootDeclaration(lazyRoot);
    }
}
