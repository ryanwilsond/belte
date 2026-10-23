using System.Threading;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class PEPropertySymbol {
    private struct PackedFlags {
        private const int IsSpecialNameFlag = 1 << 0;
        private const int IsRuntimeSpecialNameFlag = 1 << 1;
        private const int CallMethodsDirectlyFlag = 1 << 2;
        private const int HasRequiredMemberAttribute = 1 << 4;
        private const int RequiredMemberCompletionBit = 1 << 5;
        private const int HasUnscopedRefAttribute = 1 << 6;
        private const int UnscopedRefCompletionBit = 1 << 7;
        private const int IsUseSiteDiagnosticPopulatedBit = 1 << 8;
        private const int IsObsoleteAttributePopulatedBit = 1 << 9;
        private const int IsCustomAttributesPopulatedBit = 1 << 10;
        private const int IsOverloadResolutionPriorityPopulatedBit = 1 << 11;
        private const int RequiresUnsafeBit = 1 << 12;
        private const int RequiresUnsafePopulatedBit = 1 << 13;

        private int _bits;

        internal PackedFlags(bool isSpecialName, bool isRuntimeSpecialName, bool callMethodsDirectly) {
            _bits = (isSpecialName ? IsSpecialNameFlag : 0)
                    | (isRuntimeSpecialName ? IsRuntimeSpecialNameFlag : 0)
                    | (callMethodsDirectly ? CallMethodsDirectlyFlag : 0);
        }

        internal readonly bool isSpecialName => (_bits & IsSpecialNameFlag) != 0;
        internal readonly bool isRuntimeSpecialName => (_bits & IsRuntimeSpecialNameFlag) != 0;
        internal readonly bool callMethodsDirectly => (_bits & CallMethodsDirectlyFlag) != 0;

        internal bool isCustomAttributesPopulated => (Volatile.Read(ref _bits) & IsCustomAttributesPopulatedBit) != 0;

        internal void SetHasRequiredMemberAttribute(bool isRequired) {
            var bitsToSet = (isRequired ? HasRequiredMemberAttribute : 0) | RequiredMemberCompletionBit;
            ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal readonly bool TryGetHasRequiredMemberAttribute(out bool hasRequiredMemberAttribute) {
            if ((_bits & RequiredMemberCompletionBit) != 0) {
                hasRequiredMemberAttribute = (_bits & HasRequiredMemberAttribute) != 0;
                return true;
            }

            hasRequiredMemberAttribute = false;
            return false;
        }

        internal void SetHasUnscopedRefAttribute(bool unscopedRef) {
            var bitsToSet = (unscopedRef ? HasUnscopedRefAttribute : 0) | UnscopedRefCompletionBit;
            ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal readonly bool TryGetHasUnscopedRefAttribute(out bool hasUnscopedRefAttribute) {
            if ((_bits & UnscopedRefCompletionBit) != 0) {
                hasUnscopedRefAttribute = (_bits & HasUnscopedRefAttribute) != 0;
                return true;
            }

            hasUnscopedRefAttribute = false;
            return false;
        }

        internal void SetRequiresUnsafe(bool requiresUnsafe) {
            var bitsToSet = (requiresUnsafe ? RequiresUnsafeBit : 0) | RequiresUnsafePopulatedBit;
            ThreadSafeFlagOperations.Set(ref _bits, bitsToSet);
        }

        internal readonly bool TryGetRequiresUnsafe(out bool requiresUnsafe) {
            if ((_bits & RequiresUnsafePopulatedBit) != 0) {
                requiresUnsafe = (_bits & RequiresUnsafeBit) != 0;
                return true;
            }

            requiresUnsafe = false;
            return false;
        }

        internal void SetCustomAttributesPopulated() {
            ThreadSafeFlagOperations.Set(ref _bits, IsCustomAttributesPopulatedBit);
        }
    }
}
