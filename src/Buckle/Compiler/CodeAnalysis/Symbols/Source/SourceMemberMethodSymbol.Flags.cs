using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMemberMethodSymbol {
    private protected struct Flags {
        private int _flags;

        private const int MethodKindOffset = 0;
        private const int MethodKindSize = 5;
        private const int MethodKindMask = (1 << MethodKindSize) - 1;

        private const int RefKindOffset = MethodKindOffset + MethodKindSize;
        private const int RefKindSize = 3;
        private const int RefKindMask = (1 << RefKindSize) - 1;

        private const int IsMetadataVirtualOffset = RefKindOffset + RefKindSize;
        private const int IsMetadataVirtualSize = 1;

        private const int IsMetadataVirtualLockedOffset = IsMetadataVirtualOffset + IsMetadataVirtualSize;
        private const int IsMetadataVirtualLockedSize = 1;

        private const int ReturnsVoidOffset = IsMetadataVirtualLockedOffset + IsMetadataVirtualLockedSize;
        private const int ReturnsVoidSize = 2;

        private const int HasAnyBodyOffset = ReturnsVoidOffset + ReturnsVoidSize;
        private const int HasAnyBodySize = 1;

        private const int HasThisInitializerOffset = HasAnyBodyOffset + HasAnyBodySize;

        private const int HasAnyBodyBit = 1 << HasAnyBodyOffset;
        private const int IsMetadataVirtualBit = 1 << IsMetadataVirtualOffset;
        private const int IsMetadataVirtualLockedBit = 1 << IsMetadataVirtualLockedOffset;
        private const int ReturnsVoidBit = 1 << ReturnsVoidOffset;
        private const int ReturnsVoidIsSetBit = 1 << ReturnsVoidOffset + 1;
        private const int HasThisInitializerBit = 1 << HasThisInitializerOffset;

        internal readonly MethodKind methodKind => (MethodKind)((_flags >> MethodKindOffset) & MethodKindMask);

        internal readonly RefKind refKind => (RefKind)((_flags >> RefKindOffset) & RefKindMask);

        internal readonly bool returnsVoid => (_flags & ReturnsVoidBit) != 0;

        internal readonly bool hasAnyBody => (_flags & HasAnyBodyBit) != 0;

        internal readonly bool isMetadataVirtual => (_flags & IsMetadataVirtualBit) != 0;

        internal readonly bool isMetadataVirtualLocked => (_flags & IsMetadataVirtualLockedBit) != 0;

        internal readonly bool hasThisInitializer => (_flags & HasThisInitializerBit) != 0;

        internal Flags(
            MethodKind methodKind,
            RefKind refKind,
            DeclarationModifiers modifiers,
            bool returnsVoid,
            bool returnsVoidIsSet,
            bool hasAnyBody,
            bool hasThisInitializer) {
            var isMetadataVirtual = (modifiers &
                (DeclarationModifiers.Abstract | DeclarationModifiers.Virtual | DeclarationModifiers.Override))
                != 0;

            var methodKindInt = ((int)methodKind & MethodKindMask) << MethodKindOffset;
            var refKindInt = ((int)refKind & RefKindMask) << RefKindOffset;
            var hasAnyBodyInt = hasAnyBody ? HasAnyBodyBit : 0;
            var isMetadataVirtualInt = isMetadataVirtual ? IsMetadataVirtualBit : 0;
            var returnsVoidInt = returnsVoid ? ReturnsVoidBit : 0;
            var returnsVoidIsSetInt = returnsVoidIsSet ? ReturnsVoidIsSetBit : 0;
            var hasThisInitializerInt = hasThisInitializer ? HasThisInitializerBit : 0;

            _flags = methodKindInt
                | refKindInt
                | hasAnyBodyInt
                | isMetadataVirtualInt
                | returnsVoidInt
                | returnsVoidIsSetInt
                | hasThisInitializerInt;
        }

        internal void SetReturnsVoid(bool value) {
            ThreadSafeFlagOperations.Set(ref _flags, ReturnsVoidIsSetBit | (value ? ReturnsVoidBit : 0));
        }
    }
}
