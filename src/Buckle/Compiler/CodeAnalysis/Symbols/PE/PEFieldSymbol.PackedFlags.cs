using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PEFieldSymbol {
    private struct PackedFlags {
        private const int HasDisallowNullAttribute = 0x1 << 0;
        private const int HasAllowNullAttribute = 0x1 << 1;
        private const int HasMaybeNullAttribute = 0x1 << 2;
        private const int HasNotNullAttribute = 0x1 << 3;
        private const int FlowAnalysisAnnotationsCompletionBit = 0x1 << 4;
        private const int IsVolatileBit = 0x1 << 5;
        private const int RefKindOffset = 6;
        private const int RefKindMask = 0x3;
        private const int HasRequiredMemberAttribute = 0x1 << 8;
        private const int RequiredMemberCompletionBit = 0x1 << 9;

        private int _bits;

        internal bool isVolatile => (_bits & IsVolatileBit) != 0;

        internal RefKind refKind => (RefKind)((_bits >> RefKindOffset) & RefKindMask);

        internal bool SetFlowAnalysisAnnotations(FlowAnalysisAnnotations value) {
            var bitsToSet = FlowAnalysisAnnotationsCompletionBit;

            if ((value & FlowAnalysisAnnotations.DisallowNull) != 0) bitsToSet |= HasDisallowNullAttribute;
            if ((value & FlowAnalysisAnnotations.AllowNull) != 0) bitsToSet |= HasAllowNullAttribute;
            if ((value & FlowAnalysisAnnotations.MaybeNull) != 0) bitsToSet |= HasMaybeNullAttribute;
            if ((value & FlowAnalysisAnnotations.NotNull) != 0) bitsToSet |= HasNotNullAttribute;

            return ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal bool TryGetFlowAnalysisAnnotations(out FlowAnalysisAnnotations value) {
            var theBits = _bits;
            value = FlowAnalysisAnnotations.None;

            if ((theBits & HasDisallowNullAttribute) != 0) value |= FlowAnalysisAnnotations.DisallowNull;
            if ((theBits & HasAllowNullAttribute) != 0) value |= FlowAnalysisAnnotations.AllowNull;
            if ((theBits & HasMaybeNullAttribute) != 0) value |= FlowAnalysisAnnotations.MaybeNull;
            if ((theBits & HasNotNullAttribute) != 0) value |= FlowAnalysisAnnotations.NotNull;

            var result = (theBits & FlowAnalysisAnnotationsCompletionBit) != 0;
            return result;
        }

        internal void SetIsVolatile(bool isVolatile) {
            if (isVolatile) ThreadSafeFlagOperations.Set(ref _bits, IsVolatileBit);
        }

        internal void SetRefKind(RefKind refKind) {
            var bits = ((int)refKind & RefKindMask) << RefKindOffset;

            if (bits != 0)
                ThreadSafeFlagOperations.Set(ref _bits, bits);
        }

        internal bool SetHasRequiredMemberAttribute(bool isRequired) {
            var bitsToSet = RequiredMemberCompletionBit | (isRequired ? HasRequiredMemberAttribute : 0);
            return ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal bool TryGetHasRequiredMemberAttribute(out bool hasRequiredMemberAttribute) {
            if ((_bits & RequiredMemberCompletionBit) != 0) {
                hasRequiredMemberAttribute = (_bits & HasRequiredMemberAttribute) != 0;
                return true;
            }

            hasRequiredMemberAttribute = false;
            return false;
        }
    }
}
