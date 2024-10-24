
namespace Belte.Diagnostics;

/// <summary>
/// All codes used to represent each possible error and warning uniquely.
/// </summary>
public enum DiagnosticCode : ushort {
    // Never modify these codes after a release, as that would break backwards compatibility.
    // Instead mark unused errors and warnings in the docs, and append new errors and warnings to use new codes.

    // 0 is unused
    ERR_MissingFilenameO = 1,
    ERR_MultipleExplains = 2,
    ERR_MissingCodeExplain = 3,
    ERR_MissingModuleName = 4,
    ERR_MissingReference = 5,
    ERR_UnableToOpenFile = 6,
    ERR_MissingSeverity = 7,
    ERR_UnrecognizedSeverity = 8,
    ERR_UnrecognizedOption = 9,
    INF_ReplInvokeIgnore = 10,
    FTL_CannotSpecifyWithDotnet = 11,
    FTL_CannotSpecifyWithMultipleFiles = 12,
    FTL_CannotSpecifyWithInterpreter = 13,
    FTL_CannotSpecifyModuleNameWithoutDotnet = 14,
    FTL_CannotSpecifyReferencesWithoutDotnet = 15,
    FTL_NoInputFiles = 16,
    ERR_NoSuchFileOrDirectory = 17,
    INF_IgnoringUnknownFileType = 18,
    ERR_InvalidErrorCode = 19,
    INF_IgnoringCompiledFile = 20,
    ERR_UnusedErrorCode = 21,
    FTL_CannotInterpretWithMultipleFiles = 22,
    FTL_CannotInterpretFile = 23,
    ERR_MissingWarningLevel = 24,
    ERR_InvalidWarningLevel = 25,
    ERR_MissingWIgnoreCode = 26,
    ERR_MissingWIncludeCode = 27,
    ERR_CodeIsNotWarning = 28,
    ERR_MissingType = 29,
    ERR_UnrecognizedType = 30,
}
