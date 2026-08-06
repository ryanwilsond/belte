using System;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract partial class SourceMethodSymbol {
    [Flags]
    private protected enum BehaviorSpecifiers : byte {
        None = 0,
        Pure = 1 << 0,
        Memoize = 1 << 1,
        NoAlloc = 1 << 2,
        NoThrow = 1 << 3
    }
}
