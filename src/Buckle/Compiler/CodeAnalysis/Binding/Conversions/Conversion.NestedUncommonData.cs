using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

internal readonly partial struct Conversion {
    private class NestedUncommonData : UncommonData {
        internal NestedUncommonData(ImmutableArray<Conversion> nestedConversions) {
            this.nestedConversions = nestedConversions;
        }

        internal readonly ImmutableArray<Conversion> nestedConversions;
    }
}
