using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMemberContainerTypeSymbol {
    private protected sealed class MembersAndInitializers {
        internal readonly ImmutableArray<Symbol> nonTypeMembers;
        internal readonly ImmutableArray<ImmutableArray<FieldInitializer>> instanceInitializers;
        internal readonly ImmutableArray<ImmutableArray<FieldInitializer>> staticInitializers;

        internal MembersAndInitializers(
            ImmutableArray<Symbol> nonTypeMembers,
            ImmutableArray<ImmutableArray<FieldInitializer>> instanceInitializers,
            ImmutableArray<ImmutableArray<FieldInitializer>> staticInitializers) {
            this.nonTypeMembers = nonTypeMembers;
            this.instanceInitializers = instanceInitializers;
            this.staticInitializers = staticInitializers;
        }
    }
}
