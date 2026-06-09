using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceMemberContainerTypeSymbol {
    private struct LayoutUnit {
        internal ImmutableArray<Symbol> members;
        internal int size;
        internal int alignment;
        internal int originalIndex;
    }
}
