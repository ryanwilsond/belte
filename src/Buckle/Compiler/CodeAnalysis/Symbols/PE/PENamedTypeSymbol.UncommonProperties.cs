using System.Collections.Immutable;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class PENamedTypeSymbol {
    private sealed class UncommonProperties {
        internal ImmutableArray<PEFieldSymbol> lazyInstanceEnumFields;
        internal NamedTypeSymbol lazyEnumUnderlyingType;
        // internal ImmutableArray<AttributeData> lazyCustomAttributes;
        // internal ImmutableArray<string> lazyConditionalAttributeSymbols;
        // internal ThreeState lazyContainsExtensionMethods;
        internal ThreeState lazyIsByRefLike;
        // internal ThreeState lazyIsReadOnly;
        // internal string lazyDefaultMemberName;
        internal NamedTypeSymbol lazyComImportCoClassType = ErrorTypeSymbol.UnknownResultType;
        internal ThreeState lazyHasEmbeddedAttribute = ThreeState.Unknown;
        internal ThreeState lazyHasInterpolatedStringHandlerAttribute = ThreeState.Unknown;
        internal ThreeState lazyHasRequiredMembers = ThreeState.Unknown;

        internal ImmutableArray<byte> lazyFilePathChecksum = default;
    }
}
