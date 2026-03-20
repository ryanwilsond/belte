using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class PEParameterSymbol {
    private struct PackedFlags {
        private const int WellKnownAttributeDataOffset = 0;
        private const int WellKnownAttributeCompletionFlagOffset = 8;
        private const int RefKindOffset = 16;
        private const int FlowAnalysisAnnotationsOffset = 21;
        private const int ScopeOffset = 29;

        private const int RefKindMask = 0x7;
        private const int WellKnownAttributeDataMask = 0xFF;
        private const int WellKnownAttributeCompletionFlagMask = WellKnownAttributeDataMask;
        private const int FlowAnalysisAnnotationsMask = 0xFF;
        private const int ScopeMask = 0x3;

        private const int HasNameInMetadataBit = 0x1 << 19;
        private const int FlowAnalysisAnnotationsCompletionBit = 0x1 << 20;
        private const int HasUnscopedRefAttributeBit = 0x1 << 31;

        private const int AllWellKnownAttributesCompleteNoData = WellKnownAttributeCompletionFlagMask << WellKnownAttributeCompletionFlagOffset;

        private int _bits;

        internal RefKind refKind => (RefKind)((_bits >> RefKindOffset) & RefKindMask);

        internal bool hasNameInMetadata => (_bits & HasNameInMetadataBit) != 0;

        internal ScopedKind scope => (ScopedKind)((_bits >> ScopeOffset) & ScopeMask);

        internal bool hasUnscopedRefAttribute => (_bits & HasUnscopedRefAttributeBit) != 0;

        internal PackedFlags(RefKind refKind, bool attributesAreComplete, bool hasNameInMetadata, ScopedKind scope, bool hasUnscopedRefAttribute) {
            var refKindBits = ((int)refKind & RefKindMask) << RefKindOffset;
            var attributeBits = attributesAreComplete ? AllWellKnownAttributesCompleteNoData : 0;
            var hasNameInMetadataBits = hasNameInMetadata ? HasNameInMetadataBit : 0;
            var scopeBits = ((int)scope & ScopeMask) << ScopeOffset;
            var hasUnscopedRefAttributeBits = hasUnscopedRefAttribute ? HasUnscopedRefAttributeBit : 0;

            _bits = refKindBits | attributeBits | hasNameInMetadataBits | scopeBits | hasUnscopedRefAttributeBits;
        }

        internal bool SetWellKnownAttribute(WellKnownAttributeFlags flag, bool value) {
            var bitsToSet = (int)flag << WellKnownAttributeCompletionFlagOffset;

            if (value)
                bitsToSet |= (int)flag << WellKnownAttributeDataOffset;

            ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
            return value;
        }

        internal bool TryGetWellKnownAttribute(WellKnownAttributeFlags flag, out bool value) {
            var theBits = _bits;
            value = (theBits & ((int)flag << WellKnownAttributeDataOffset)) != 0;
            return (theBits & ((int)flag << WellKnownAttributeCompletionFlagOffset)) != 0;
        }

        internal bool SetFlowAnalysisAnnotations(FlowAnalysisAnnotations value) {
            var bitsToSet = FlowAnalysisAnnotationsCompletionBit |
                (((int)value & FlowAnalysisAnnotationsMask) << FlowAnalysisAnnotationsOffset);

            return ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal bool TryGetFlowAnalysisAnnotations(out FlowAnalysisAnnotations value) {
            var theBits = _bits;
            value = (FlowAnalysisAnnotations)((theBits >> FlowAnalysisAnnotationsOffset) & FlowAnalysisAnnotationsMask);
            var result = (theBits & FlowAnalysisAnnotationsCompletionBit) != 0;
            return result;
        }
    }
}
