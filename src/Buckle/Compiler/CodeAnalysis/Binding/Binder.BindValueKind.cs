using System;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    private const int ValueKindInsignificantBits = 2;
    private const BindValueKind ValueKindSignificantBitsMask
        = unchecked((BindValueKind)~((1 << ValueKindInsignificantBits) - 1));

    [Flags]
    internal enum BindValueKind : ushort {
        RValue = 1 << ValueKindInsignificantBits,
        Assignable = 2 << ValueKindInsignificantBits,
        RefersToLocation = 4 << ValueKindInsignificantBits,
        RefAssignable = 8 << ValueKindInsignificantBits,
        RValueOrMethodGroup = RValue + 1,
        CompoundAssignment = RValue | Assignable,
        IncrementDecrement = CompoundAssignment + 1,
        RefOrOut = RefersToLocation | RValue | Assignable,
        RefReturn = RefOrOut + 1,
        RefConst = RefersToLocation | RValue,
    }
}
