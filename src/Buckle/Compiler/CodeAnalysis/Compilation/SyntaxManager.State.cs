using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis;

internal sealed partial class SyntaxManager {
    internal sealed class State {
        internal readonly ImmutableArray<SyntaxTree> syntaxTrees;
        internal readonly ImmutableDictionary<SyntaxTree, int> ordinalMap;

        internal State(ImmutableArray<SyntaxTree> syntaxTrees, ImmutableDictionary<SyntaxTree, int> ordinalMap) {
            this.syntaxTrees = syntaxTrees;
            this.ordinalMap = ordinalMap;
        }
    }
}
