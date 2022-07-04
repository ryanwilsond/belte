
namespace Belte.Diagnostics;

internal enum DiagnosticCode : int {
    Unknown = 0,

    ERR_UnknownReplCommand = 1,
    ERR_WrongArgumentCount = 2,
    ERR_UndefinedSymbol = 3,
    ERR_MissingFilenameO = 4,
    ERR_MultipleExplains = 5,
    ERR_MissingCodeExplain = 6,
    ERR_MissingModuleName = 7,
    ERR_MissingReference = 8,
    ERR_MissingEntrySymbol = 9,
    ERR_NoOptionAfterW = 10,
    ERR_UnrecognizedWOption = 11,
    ERR_UnrecognizedOption = 12,
    WRN_ReplInvokeIgnore = 13,
    ERR_CannotSpecifyWithDotnet = 14,
    ERR_CannotSpecifyWithMultipleFiles = 15,
    ERR_CannotSpecifyWithInterpreter = 16,
    ERR_CannotSpecifyModuleNameWithDotnet = 17,
    ERR_CannotSpecifyReferencesWithDotnet = 18,
    ERR_NoInputFiles = 19,
    ERR_NoSuchFileOrDirectory = 20,
    ERR_NoSuchFile = 21,
    WRN_IgnoringUnknownFileType = 22,
    ERR_InvalidErrorCode = 23,
    WRN_IgnoringCompiledFile = 24,
    ERR_UnusedErrorCode = 25,
}
