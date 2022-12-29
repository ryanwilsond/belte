
namespace Repl.Diagnostics;

internal enum DiagnosticCode : int {
    // 0 is unused
    ERR_UnknownReplCommand = 1,
    ERR_WrongArgumentCount = 2,
    ERR_UndefinedSymbol = 3,
    ERR_NoSuchFile = 4,
    ERR_InvalidArgument = 5,
    ERR_NoSuchFunction = 6,
    ERR_AmbiguousSignature = 7,
}
