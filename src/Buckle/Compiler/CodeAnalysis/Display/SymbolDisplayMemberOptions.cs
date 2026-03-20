using System;

namespace Buckle.CodeAnalysis.Display;

[Flags]
internal enum SymbolDisplayMemberOptions : byte {
    None = 0,
    IncludeType = 1 << 0,
    IncludeModifiers = 1 << 1,
    IncludeAccessibility = 1 << 2,
    IncludeParameters = 1 << 3,
    IncludeContainingType = 1 << 4,
    IncludeConstantValue = 1 << 5,

    Everything = IncludeType
        | IncludeModifiers
        | IncludeAccessibility
        | IncludeParameters
        | IncludeContainingType
        | IncludeConstantValue,
}
