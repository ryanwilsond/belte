using Buckle.Diagnostics;
using Diagnostics;

/// <summary>
/// All predefined fatal messages that can be used by the compiler.
/// The return value for all methods is a new diagnostic that needs to be manually handled or added to a
/// <see cref="DiagnosticQueue<T>" />.
/// The parameters for all methods allow the fatal messages to be more dynamic and represent the fatal more accurately.
/// </summary>
internal class Fatal {
    /// <summary>
    /// Temporary fatal messages.
    /// Once the compiler is finished, this class will be unnecessary.
    /// </summary>
    internal static class Unsupported {
        /// <summary>
        /// BU9003. Run `buckle --explain BU9003` on the command line for more info.
        /// </summary>
        internal static BelteDiagnostic IndependentCompilation() {
            var message = "unsupported: cannot compile independently; must specify '-i', '-t', or '-r'";
            return new BelteDiagnostic(FatalInfo(DiagnosticCode.UNS_IndependentCompilation), message);
        }

        /// <summary>
        /// BU9004. Run `buckle --explain BU9004` on the command line for more info.
        /// </summary>
        internal static BelteDiagnostic DotnetCompilation() {
            var message = "unsupported: cannot compile with .NET integration; must specify '-i', '-t', or '-r'";
            return new BelteDiagnostic(FatalInfo(DiagnosticCode.UNS_DotnetCompilation), message);
        }
    }

    private static DiagnosticInfo FatalInfo(DiagnosticCode code) {
        return new DiagnosticInfo((int)code, "BU", DiagnosticSeverity.Fatal);
    }
}
