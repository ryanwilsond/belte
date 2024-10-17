using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Buckle.CodeAnalysis.Syntax;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis;

internal sealed partial class SyntaxManager {
    private State _lazyState;

    internal SyntaxManager(ImmutableArray<SyntaxTree> syntaxTrees, State state) {
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

        foreach (var tree in syntaxTrees)
            AppendAllSyntaxTrees(treesBuilder, tree, ordinalMapBuilder);

        return new State(treesBuilder.ToImmutable(), ordinalMapBuilder.ToImmutableDictionary());
    }

    internal SyntaxManager AddSyntaxTrees(IEnumerable<SyntaxTree> trees) {
        var state = _lazyState;
        var newSyntaxTrees = syntaxTrees.AddRange(trees);

        if (state is null)
            return WithSyntaxTrees(newSyntaxTrees);

        var ordinalMapBuilder = state.ordinalMap.ToBuilder();
        var treesBuilder = ArrayBuilder<SyntaxTree>.GetInstance();
        treesBuilder.AddRange(state.syntaxTrees);

        foreach (var tree in trees)
            AppendAllSyntaxTrees(treesBuilder, tree, ordinalMapBuilder);

        state = new State(treesBuilder.ToImmutable(), ordinalMapBuilder.ToImmutableDictionary());
        return new SyntaxManager(newSyntaxTrees, state);
    }

    private SyntaxManager WithSyntaxTrees(ImmutableArray<SyntaxTree> trees) {
        return new SyntaxManager(trees, null);
    }

    private static void AppendAllSyntaxTrees(
        ArrayBuilder<SyntaxTree> treesBuilder,
        SyntaxTree tree,
        IDictionary<SyntaxTree, int> ordinalMapBuilder) {
        treesBuilder.Add(tree);
        ordinalMapBuilder.Add(tree, ordinalMapBuilder.Count);
    }
}
