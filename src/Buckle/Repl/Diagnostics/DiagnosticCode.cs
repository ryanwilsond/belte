
namespace Repl.Diagnostics;

/// <summary>
/// All codes used to represent each possible error and warning uniquely.
/// </summary>
public enum DiagnosticCode : ushort {
    // Never modify these codes after a release, as that would break backwards compatibility.
    // Instead mark unused errors and warnings in the docs, and append new errors and warnings to use new codes.

    // 0 is unused
    ERR_UnknownReplCommand = 1,
    ERR_WrongArgumentCount = 2,
    ERR_UndefinedSymbol = 3,
    ERR_NoSuchFile = 4,
    ERR_InvalidArgument = 5,
    ERR_NoSuchMethod = 6,
    ERR_AmbiguousSignature = 7,
    ERR_FailedILGeneration = 8,
}
