using System;

namespace Buckle.CodeAnalysis.Symbols;

[Flags]
internal enum MethodImportAttributes : short {
    None = 0x0,
    ExactSpelling = 0x0001,
    BestFitMappingDisable = 0x0020,
    BestFitMappingEnable = 0x0010,
    BestFitMappingMask = 0x0030,
    CharSetAnsi = 0x0002,
    CharSetUnicode = 0x0004,
    CharSetAuto = 0x0006,
    CharSetMask = 0x0006,
    ThrowOnUnmappableCharEnable = 0x1000,
    ThrowOnUnmappableCharDisable = 0x2000,
    ThrowOnUnmappableCharMask = 0x3000,
    SetLastError = 0x0040,
    CallingConventionWinApi = 0x0100,
    CallingConventionCDecl = 0x0200,
    CallingConventionStdCall = 0x0300,
    CallingConventionThisCall = 0x0400,
    CallingConventionFastCall = 0x0500,
    CallingConventionMask = 0x0700,
}
