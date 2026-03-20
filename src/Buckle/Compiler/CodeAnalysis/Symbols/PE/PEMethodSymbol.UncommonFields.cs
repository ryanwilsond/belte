using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PEMethodSymbol {
    private sealed class UncommonFields {
        // internal ParameterSymbol _lazyThisParameter;
        internal OverriddenOrHiddenMembersResult _lazyOverriddenOrHiddenMembersResult;
        internal ImmutableArray<AttributeData> _lazyCustomAttributes;
        internal ImmutableArray<string> _lazyConditionalAttributeSymbols;
        internal ImmutableArray<string> _lazyNotNullMembers;
        internal ImmutableArray<string> _lazyNotNullMembersWhenTrue;
        internal ImmutableArray<string> _lazyNotNullMembersWhenFalse;
        internal MethodSymbol _lazyExplicitClassOverride;
        // internal int _lazyOverloadResolutionPriority;
    }
}
