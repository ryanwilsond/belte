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
    internal static Diagnostic CannotSpecifyWithDotnet() {
        var message = "cannot specify '-p', '-s', '-c', or '-t' with .NET integration";
        return new Diagnostic(FatalInfo(DiagnosticCode.FTL_CannotSpecifyWithDotnet), message);
    }

    internal static Diagnostic CannotSpecifyWithMultipleFiles() {
        var message = "cannot specify output file with '-p', '-s', '-c', or '-t' with multiple files";
        return new Diagnostic(FatalInfo(DiagnosticCode.FTL_CannotSpecifyWithMultipleFiles), message);
    }

    internal static Diagnostic CannotSpecifyWithInterpreter() {
        var message = "cannot specify output path or use '-p', '-s', '-c', or '-t' with interpreter";
        return new Diagnostic(FatalInfo(DiagnosticCode.FTL_CannotSpecifyWithInterpreter), message);
    }

    internal static Diagnostic CannotSpecifyModuleNameWithoutDotnet() {
        var message = "cannot specify module name without .NET integration";
        return new Diagnostic(FatalInfo(DiagnosticCode.FTL_CannotSpecifyModuleNameWithoutDotnet), message);
    }

    internal static Diagnostic CannotSpecifyReferencesWithoutDotnet() {
        var message = "cannot specify references without .NET integration";
        return new Diagnostic(FatalInfo(DiagnosticCode.FTL_CannotSpecifyReferencesWithoutDotnet), message);
    }

    internal static Diagnostic NoInputFiles() {
        var message = "no input files";
        return new Diagnostic(FatalInfo(DiagnosticCode.FTL_NoInputFiles), message);
    }

    internal static Diagnostic CannotInterpretWithMultipleFiles() {
        var message = "cannot pass multiple files when running as a script";
        return new Diagnostic(FatalInfo(DiagnosticCode.FTL_CannotInterpretWithMultipleFiles), message);
    }

    internal static Diagnostic CannotInterpretFile() {
        var message = "cannot interpret file";
        return new Diagnostic(FatalInfo(DiagnosticCode.FTL_CannotInterpretFile), message);
    }

    internal static Diagnostic DLLWithWrongBuildMode() {
        var message = "cannot compile to a dynamically linked library without .NET integration";
        return new Diagnostic(FatalInfo(DiagnosticCode.FTL_DLLWithWrongBuildMode), message);
    }

    internal static Diagnostic CannotSpecifyOutAndModuleWithDll() {
        var message = "cannot specify an output file and module name when building a dynamically linked library";
        return new Diagnostic(FatalInfo(DiagnosticCode.FTL_CannotSpecifyOutAndModuleWithDll), message);
    }

    private static DiagnosticInfo FatalInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "CL", DiagnosticSeverity.Fatal);
    }
}
