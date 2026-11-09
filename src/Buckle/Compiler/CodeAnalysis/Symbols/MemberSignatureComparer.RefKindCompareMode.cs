using System;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class MemberSignatureComparer
{
    [Flags]
    internal enum RefKindCompareMode
    {
        IgnoreRefKind = 1 << 0,
        TreatAllRefAsEquivalent = 1 << 1,
        ConsiderDifferences = 1 << 2,
    }
}
