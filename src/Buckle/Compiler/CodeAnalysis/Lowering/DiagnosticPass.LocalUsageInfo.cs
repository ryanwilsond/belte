using System;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed partial class DiagnosticPass {
    [Flags]
    private enum LocalUsageInfo {
        NotUsed = 0,
        Used = 1,
        PassedByRefVar = 1 << 1,
        PassedByRefFinal = 1 << 2,
        PassedByRefConst = 1 << 3,
        Reassigned = 1 << 4,
        Mutated = 1 << 5,

        HasConstExprInitializer = 1 << 6,

        UsagePertaining = HasConstExprInitializer - 1,
    }
}
