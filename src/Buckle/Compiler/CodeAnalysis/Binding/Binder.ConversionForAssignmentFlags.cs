using System;

namespace Buckle.CodeAnalysis.Binding;

internal partial class Binder {
    [Flags]
    internal enum ConversionForAssignmentFlags : byte {
        None = 0,
        DefaultParameter = 1 << 0,
        RefAssignment = 1 << 1,
        IncrementAssignment = 1 << 2,
        CompoundAssignment = 1 << 3,
        PredefinedOperator = 1 << 4,
    }
}
