using System;

namespace Buckle.CodeAnalysis.Display;

[Flags]
internal enum SymbolDisplayParameterOptions : byte {
    None = 0,
    IncludeModifiers = 1 << 0,
    IncludeType = 1 << 1,
    IncludeName = 1 << 2,
    IncludeDefaultValue = 1 << 3,

    Everything = IncludeModifiers | IncludeType | IncludeName | IncludeDefaultValue,
}
