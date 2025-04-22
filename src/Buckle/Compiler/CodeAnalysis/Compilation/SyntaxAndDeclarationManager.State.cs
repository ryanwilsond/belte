using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis;

internal sealed partial class SyntaxAndDeclarationManager {
    internal sealed class State {
        internal readonly ImmutableArray<SyntaxTree> syntaxTrees;
        internal readonly ImmutableDictionary<SyntaxTree, int> ordinalMap;
        internal readonly ImmutableDictionary<string, SyntaxTree> loadedSyntaxTreeMap;
        internal readonly ImmutableDictionary<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>> rootNamespaces;
        internal readonly ImmutableDictionary<
            SyntaxTree,
            OneOrMany<WeakReference<StrongBox<ImmutableSegmentedHashSet<string>>>>> lastComputedMemberNames;
        internal readonly DeclarationTable declarationTable;

        internal State(
            ImmutableArray<SyntaxTree> syntaxTrees,
            ImmutableDictionary<SyntaxTree, int> syntaxTreeOrdinalMap,
            ImmutableDictionary<string, SyntaxTree> loadedSyntaxTreeMap,
            ImmutableDictionary<SyntaxTree, Lazy<RootSingleNamespaceDeclaration>> rootNamespaces,
            ImmutableDictionary<SyntaxTree, OneOrMany<WeakReference<StrongBox<ImmutableSegmentedHashSet<string>>>>>
                lastComputedMemberNames,
            DeclarationTable declarationTable) {
            this.syntaxTrees = syntaxTrees;
            ordinalMap = syntaxTreeOrdinalMap;
            this.loadedSyntaxTreeMap = loadedSyntaxTreeMap;
            this.rootNamespaces = rootNamespaces;
            this.lastComputedMemberNames = lastComputedMemberNames;
            this.declarationTable = declarationTable;
        }
    }
}
