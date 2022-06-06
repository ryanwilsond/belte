
namespace Buckle.Diagnostics;

internal enum DiagnosticCode : int {
    Unknown = 0,

    ERR_Info = 1,
    ERR_GlobalReturnValue = 2,
    ERR_InvalidReference = 3,
    ERR_InvalidType = 4,
    ERR_BadCharacter = 5,
    ERR_UnexpectedToken = 6,
    ERR_CannotConvertImplicity = 7,
}
