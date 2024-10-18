using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMemberContainerTypeSymbol {
    private protected sealed class MembersAndInitializers {
        internal readonly ImmutableArray<Symbol> nonTypeMembers;
        internal readonly ImmutableArray<ImmutableArray<FieldInitializer>> fieldInitializers;

        internal MembersAndInitializers(
            ImmutableArray<Symbol> nonTypeMembers,
            ImmutableArray<ImmutableArray<FieldInitializer>> fieldInitializers) {
            this.nonTypeMembers = nonTypeMembers;
            this.fieldInitializers = fieldInitializers;
        }
    }
}
