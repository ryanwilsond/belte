using System;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class PEParameterSymbol {
    [Flags]
    private enum WellKnownAttributeFlags : byte {
        HasIDispatchConstantAttribute = 0x1 << 0,
        HasIUnknownConstantAttribute = 0x1 << 1,
        HasCallerFilePathAttribute = 0x1 << 2,
        HasCallerLineNumberAttribute = 0x1 << 3,
        HasCallerMemberNameAttribute = 0x1 << 4,
        IsCallerFilePath = 0x1 << 5,
        IsCallerLineNumber = 0x1 << 6,
        IsCallerMemberName = 0x1 << 7,
    }
}
