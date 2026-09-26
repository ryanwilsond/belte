
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class PEPropertySymbol {
    private sealed class UncommonFields {
        public ImmutableArray<AttributeData> _lazyCustomAttributes;
        // public ObsoleteAttributeData _lazyObsoleteAttributeData = ObsoleteAttributeData.Uninitialized;
        // public int _lazyOverloadResolutionPriority;
    }
}
