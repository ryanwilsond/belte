using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class PEMethodSymbol {
    private struct PackedFlags {
        private const int MethodKindOffset = 0;
        private const int MethodKindMask = 0x1F;

        private const int MethodKindIsPopulatedBit = 0x1 << 5;
        private const int IsExtensionMethodBit = 0x1 << 6;
        private const int IsExtensionMethodIsPopulatedBit = 0x1 << 7;
        private const int IsExplicitFinalizerOverrideBit = 0x1 << 8;
        private const int IsExplicitClassOverrideBit = 0x1 << 9;
        private const int IsExplicitOverrideIsPopulatedBit = 0x1 << 10;
        private const int IsObsoleteAttributePopulatedBit = 0x1 << 11;
        private const int IsCustomAttributesPopulatedBit = 0x1 << 12;
        private const int IsUseSiteDiagnosticPopulatedBit = 0x1 << 13;
        private const int IsConditionalPopulatedBit = 0x1 << 14;
        private const int IsOverriddenOrHiddenMembersPopulatedBit = 0x1 << 15;
        private const int IsReadOnlyBit = 0x1 << 16;
        private const int IsReadOnlyPopulatedBit = 0x1 << 17;
        private const int NullableContextOffset = 18;
        private const int NullableContextMask = 0x7;
        private const int DoesNotReturnBit = 0x1 << 21;
        private const int IsDoesNotReturnPopulatedBit = 0x1 << 22;
        private const int IsMemberNotNullPopulatedBit = 0x1 << 23;
        private const int IsInitOnlyBit = 0x1 << 24;
        private const int IsInitOnlyPopulatedBit = 0x1 << 25;
        private const int IsUnmanagedCallersOnlyAttributePopulatedBit = 0x1 << 26;
        private const int HasSetsRequiredMembersBit = 0x1 << 27;
        private const int HasSetsRequiredMembersPopulatedBit = 0x1 << 28;
        private const int IsUnscopedRefBit = 0x1 << 29;
        private const int IsUnscopedRefPopulatedBit = 0x1 << 30;
        private const int OverloadResolutionPriorityPopulatedBit = 0x1 << 31;

        private int _bits;

        internal MethodKind methodKind {
            get {
                return (MethodKind)((_bits >> MethodKindOffset) & MethodKindMask);
            }
            set {
                _bits = (_bits & ~(MethodKindMask << MethodKindOffset)) |
                    (((int)value & MethodKindMask) << MethodKindOffset) | MethodKindIsPopulatedBit;
            }
        }

        internal bool methodKindIsPopulated => (_bits & MethodKindIsPopulatedBit) != 0;
        internal bool isExtensionMethod => (_bits & IsExtensionMethodBit) != 0;
        internal bool isExtensionMethodIsPopulated => (_bits & IsExtensionMethodIsPopulatedBit) != 0;
        internal bool isExplicitFinalizerOverride => (_bits & IsExplicitFinalizerOverrideBit) != 0;
        internal bool isExplicitClassOverride => (_bits & IsExplicitClassOverrideBit) != 0;
        internal bool isExplicitOverrideIsPopulated => (_bits & IsExplicitOverrideIsPopulatedBit) != 0;
        internal bool isObsoleteAttributePopulated => (_bits & IsObsoleteAttributePopulatedBit) != 0;
        internal bool isCustomAttributesPopulated => (_bits & IsCustomAttributesPopulatedBit) != 0;
        internal bool isUseSiteDiagnosticPopulated => (_bits & IsUseSiteDiagnosticPopulatedBit) != 0;
        internal bool isConditionalPopulated => (_bits & IsConditionalPopulatedBit) != 0;
        internal bool isOverriddenOrHiddenMembersPopulated => (_bits & IsOverriddenOrHiddenMembersPopulatedBit) != 0;
        internal bool isReadOnly => (_bits & IsReadOnlyBit) != 0;
        internal bool isReadOnlyPopulated => (_bits & IsReadOnlyPopulatedBit) != 0;
        internal bool doesNotReturn => (_bits & DoesNotReturnBit) != 0;
        internal bool isDoesNotReturnPopulated => (_bits & IsDoesNotReturnPopulatedBit) != 0;
        internal bool isMemberNotNullPopulated => (_bits & IsMemberNotNullPopulatedBit) != 0;
        internal bool isInitOnly => (_bits & IsInitOnlyBit) != 0;
        internal bool isInitOnlyPopulated => (_bits & IsInitOnlyPopulatedBit) != 0;
        internal bool isUnmanagedCallersOnlyAttributePopulated => (_bits & IsUnmanagedCallersOnlyAttributePopulatedBit) != 0;
        internal bool hasSetsRequiredMembers => (_bits & HasSetsRequiredMembersBit) != 0;
        internal bool hasSetsRequiredMembersPopulated => (_bits & HasSetsRequiredMembersPopulatedBit) != 0;
        internal bool isUnscopedRef => (_bits & IsUnscopedRefBit) != 0;
        internal bool isUnscopedRefPopulated => (_bits & IsUnscopedRefPopulatedBit) != 0;
        internal bool isOverloadResolutionPriorityPopulated => (_bits & OverloadResolutionPriorityPopulatedBit) != 0;

        private static bool BitsAreUnsetOrSame(int bits, int mask) {
            return (bits & mask) == 0 || (bits & mask) == mask;
        }

        internal void InitializeIsExtensionMethod(bool isExtensionMethod) {
            var bitsToSet = (isExtensionMethod ? IsExtensionMethodBit : 0) | IsExtensionMethodIsPopulatedBit;
            ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal void InitializeIsReadOnly(bool isReadOnly) {
            var bitsToSet = (isReadOnly ? IsReadOnlyBit : 0) | IsReadOnlyPopulatedBit;
            ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal void InitializeMethodKind(MethodKind methodKind) {
            var bitsToSet = (((int)methodKind & MethodKindMask) << MethodKindOffset) | MethodKindIsPopulatedBit;
            ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal void InitializeIsExplicitOverride(bool isExplicitFinalizerOverride, bool isExplicitClassOverride) {
            var bitsToSet =
                (isExplicitFinalizerOverride ? IsExplicitFinalizerOverrideBit : 0) |
                (isExplicitClassOverride ? IsExplicitClassOverrideBit : 0) |
                IsExplicitOverrideIsPopulatedBit;

            ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal void SetIsObsoleteAttributePopulated() {
            ThreadSafeFlagOperations.Set(ref _bits, IsObsoleteAttributePopulatedBit);
        }

        internal void SetIsCustomAttributesPopulated() {
            ThreadSafeFlagOperations.Set(ref _bits, IsCustomAttributesPopulatedBit);
        }

        internal void SetIsUseSiteDiagnosticPopulated() {
            ThreadSafeFlagOperations.Set(ref _bits, IsUseSiteDiagnosticPopulatedBit);
        }

        internal void SetIsConditionalAttributePopulated() {
            ThreadSafeFlagOperations.Set(ref _bits, IsConditionalPopulatedBit);
        }

        internal void SetIsOverriddenOrHiddenMembersPopulated() {
            ThreadSafeFlagOperations.Set(ref _bits, IsOverriddenOrHiddenMembersPopulatedBit);
        }

        internal bool TryGetNullableContext(out byte? value) {
            return ((NullableContextKind)((_bits >> NullableContextOffset) & NullableContextMask))
                .TryGetByte(out value);
        }

        internal bool SetNullableContext(byte? value) {
            return ThreadSafeFlagOperations.Set(
                ref _bits,
                ((int)value.ToNullableContextFlags() & NullableContextMask) << NullableContextOffset
            );
        }

        internal bool InitializeDoesNotReturn(bool value) {
            var bitsToSet = IsDoesNotReturnPopulatedBit;

            if (value)
                bitsToSet |= DoesNotReturnBit;

            return ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal void SetIsMemberNotNullPopulated() {
            ThreadSafeFlagOperations.Set(ref _bits, IsMemberNotNullPopulatedBit);
        }

        internal void InitializeIsInitOnly(bool isInitOnly) {
            var bitsToSet = (isInitOnly ? IsInitOnlyBit : 0) | IsInitOnlyPopulatedBit;
            ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal void SetIsUnmanagedCallersOnlyAttributePopulated() {
            ThreadSafeFlagOperations.Set(ref _bits, IsUnmanagedCallersOnlyAttributePopulatedBit);
        }

        internal bool InitializeSetsRequiredMembersBit(bool value) {
            var bitsToSet = HasSetsRequiredMembersPopulatedBit;

            if (value)
                bitsToSet |= HasSetsRequiredMembersBit;

            return ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal bool InitializeIsUnscopedRef(bool value) {
            var bitsToSet = IsUnscopedRefPopulatedBit;

            if (value)
                bitsToSet |= IsUnscopedRefBit;

            return ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal void SetIsOverloadResolutionPriorityPopulated() {
            ThreadSafeFlagOperations.Set(ref _bits, OverloadResolutionPriorityPopulatedBit);
        }
    }
}
