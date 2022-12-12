
namespace Diagnostics;

/// <summary>
/// Severity of diagnostic, does not effect how the DiagnosticQueue interacts with them.
/// </summary>
public enum DiagnosticType {
    Error,
    Warning,
    Fatal,
    Unknown,
}

/// <summary>
/// Information about a diagnostic including severity (see DiagnosticType), code, and module.
/// </summary>
public sealed class DiagnosticInfo {
    /// <summary>
    /// Creates an empty DiagnosticInfo (severity is set to DiagnosticType.Unknown).
    /// </summary>
    public DiagnosticInfo() {
        code = null;
        module = null;
        severity = DiagnosticType.Unknown;
    }

    /// <summary>
    /// Creates a new DiagnosticInfo (severity is set to DiagnosticType.Unknown).
    /// </summary>
    /// <param name="code">User defined code for what caused the diagnostic</param>
    /// <param name="module">What module of code produced the diagnostic (user defined)</param>
    public DiagnosticInfo(int code, string module) {
        this.code = code;
        this.module = module;
        this.severity = DiagnosticType.Unknown;
    }

    /// <summary>
    /// Creates am empty DiagnosticInfo with a severity.
    /// </summary>
    /// <param name="severity">Severity of diagnostic (see DiagnosticType)</param>
    public DiagnosticInfo(DiagnosticType severity) {
        code = null;
        this.severity = severity;
    }

    /// <summary>
    /// Creates a new DiagnosticInfo.
    /// </summary>
    /// <param name="code">User defined code for what caused the diagnostic</param>
    /// <param name="module">What module of code produced the diagnostic (user defined)</param>
    /// <param name="severity">Severity of diagnostic (see DiagnosticType)</param>
    public DiagnosticInfo(int code, string module, DiagnosticType severity) {
        this.code = code;
        this.module = module;
        this.severity = severity;
    }

    /// <summary>
    /// The severity of this diagnostic (see DiagnosticType).
    /// </summary>
    public DiagnosticType severity { get; }

    /// <summary>
    /// The user defined code to describe what caused this diagnostic.
    /// </summary>
    public int? code { get; }

    /// <summary>
    /// What module of code produced this diagnostic.
    /// </summary>
    public string module { get; }
}
