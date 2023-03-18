using Diagnostics;

namespace Belte.Diagnostics;

/// <summary>
/// All predefined fatal messages that can be used by the command line.
/// The return value for all methods is a new diagnostic that needs to be manually handled or added to a
/// <see cref="DiagnosticQueue<T>" />.
/// The parameters for all methods allow the fatal messages to be more dynamic and represent the fatal more
/// accurately.
/// </summary>
internal static class Fatal {
    /// <summary>
    /// CL0011. Run `buckle --explain CL0011` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotSpecifyWithDotnet() {
        var message = "cannot specify '-p', '-s', '-c', or '-t' with .NET integration";
        return new Diagnostic(FatalInfo(DiagnosticCode.ERR_CannotSpecifyWithDotnet), message);
    }

    /// <summary>
    /// CL0012. Run `buckle --explain CL0012` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotSpecifyWithMultipleFiles() {
        var message = "cannot specify output file with '-p', '-s', '-c', or '-t' with multiple files";
        return new Diagnostic(FatalInfo(DiagnosticCode.ERR_CannotSpecifyWithMultipleFiles), message);
    }

    /// <summary>
    /// CL0013. Run `buckle --explain CL0013` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotSpecifyWithInterpreter() {
        var message = "cannot specify output path or use '-p', '-s', '-c', or '-t' with interpreter";
        return new Diagnostic(FatalInfo(DiagnosticCode.ERR_CannotSpecifyWithInterpreter), message);
    }

    /// <summary>
    /// CL0014. Run `buckle --explain CL0014` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotSpecifyModuleNameWithoutDotnet() {
        var message = "cannot specify module name without .NET integration";
        return new Diagnostic(FatalInfo(DiagnosticCode.ERR_CannotSpecifyModuleNameWithoutDotnet), message);
    }

    /// <summary>
    /// CL0015. Run `buckle --explain CL0015` on the command line for more info.
    /// </summary>
    internal static Diagnostic CannotSpecifyReferencesWithoutDotnet() {
        var message = "cannot specify references without .NET integration";
        return new Diagnostic(FatalInfo(DiagnosticCode.ERR_CannotSpecifyReferencesWithoutDotnet), message);
    }

    /// <summary>
    /// CL0016. Run `buckle --explain CL0016` on the command line for more info.
    /// </summary>
    internal static Diagnostic NoInputFiles() {
        var message = "no input files";
        return new Diagnostic(FatalInfo(DiagnosticCode.ERR_NoInputFiles), message);
    }

    private static DiagnosticInfo FatalInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticSeverity.Fatal);
    }
}
