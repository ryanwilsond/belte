
namespace Belte.Diagnostics;

internal enum DiagnosticCode : int {
    // 0 is unused
    ERR_MissingFilenameO = 1,
    ERR_MultipleExplains = 2,
    ERR_MissingCodeExplain = 3,
    ERR_MissingModuleName = 4,
    ERR_MissingReference = 5,
    ERR_MissingEntrySymbol = 6,
    ERR_NoOptionAfterW = 7,
    ERR_UnrecognizedWOption = 8,
    ERR_UnrecognizedOption = 9,
    WRN_ReplInvokeIgnore = 10,
    ERR_CannotSpecifyWithDotnet = 11,
    ERR_CannotSpecifyWithMultipleFiles = 12,
    ERR_CannotSpecifyWithInterpreter = 13,
    ERR_CannotSpecifyModuleNameWithoutDotnet = 14,
    ERR_CannotSpecifyReferencesWithoutDotnet = 15,
    ERR_NoInputFiles = 16,
    ERR_NoSuchFileOrDirectory = 17,
    WRN_IgnoringUnknownFileType = 18,
    ERR_InvalidErrorCode = 19,
    WRN_IgnoringCompiledFile = 20,
    ERR_UnusedErrorCode = 21,
    WRN_CorruptInstallation = 22,
}
