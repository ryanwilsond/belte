using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

internal readonly partial struct Conversion {
    private class NestedUncommonData : UncommonData {
        internal readonly ImmutableArray<Conversion> nestedConversions;
        internal readonly ConversionKind? trueKind;

        internal NestedUncommonData(ImmutableArray<Conversion> nestedConversions, ConversionKind? trueKind) {
            this.nestedConversions = nestedConversions;
            this.trueKind = trueKind;
        }
    }
}
