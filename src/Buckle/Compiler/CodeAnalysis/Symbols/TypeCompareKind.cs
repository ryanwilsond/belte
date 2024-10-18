using System;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Specifies the different kinds of comparison between types.
/// </summary>
[Flags]
internal enum TypeCompareKind : byte {
    ConsiderEverything = 0,
    IgnoreNullability = 1 << 0,
    IgnoreArraySizesAndLowerBounds = 1 << 1,

    AllIgnoreOptions = IgnoreNullability | IgnoreArraySizesAndLowerBounds,
}
