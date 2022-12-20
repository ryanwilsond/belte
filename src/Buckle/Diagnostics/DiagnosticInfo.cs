
namespace Diagnostics;

/// <summary>
/// Severity of <see cref="Diagnostic" />, does not effect how the <see cref="DiagnosticQueue" /> interacts with them.
/// </summary>
public enum DiagnosticType {
    Error,
    Warning,
    Fatal,
    Unknown,
}

/// <summary>
/// Information about a <see cref="Diagnostic" /> including severity (see <see cref="DiagnosticType" />),
/// code, and module.
/// </summary>
public sealed class DiagnosticInfo {
    /// <summary>
    /// Creates an empty <see cref="DiagnosticInfo" /> (severity is set to <see cref="DiagnosticType.Unknown" />).
    /// </summary>
    public DiagnosticInfo() {
        code = null;
        module = null;
        severity = DiagnosticType.Unknown;
    }

    /// <summary>
    /// Creates a new <see cref="DiagnosticInfo" /> (severity is set to <see cref="DiagnosticType.Unknown" />).
    /// </summary>
    /// <param name="code">User defined code for what caused the <see cref="Diagnostic" />.</param>
    /// <param name="module">What module of code produced the <see cref="Diagnostic" /> (user defined).</param>
    public DiagnosticInfo(int code, string module) {
        this.code = code;
        this.module = module;
        this.severity = DiagnosticType.Unknown;
    }

    /// <summary>
    /// Creates am empty <see cref="DiagnosticInfo" /> with a severity.
    /// </summary>
    /// <param name="severity">Severity of <see cref="Diagnostic" /> (see <see cref="DiagnosticType" />).</param>
    public DiagnosticInfo(DiagnosticType severity) {
        code = null;
        this.severity = severity;
    }

    /// <summary>
    /// Creates a new <see cref="DiagnosticInfo" />.
    /// </summary>
    /// <param name="code">User defined code for what caused the <see cref="Diagnostic" />.</param>
    /// <param name="module">What module of code produced the <see cref="Diagnostic" /> (user defined).</param>
    /// <param name="severity">Severity of <see cref="Diagnostic" /> (see <see cref="DiagnosticType" />).</param>
    public DiagnosticInfo(int code, string module, DiagnosticType severity) {
        this.code = code;
        this.module = module;
        this.severity = severity;
    }

    /// <summary>
    /// The severity of this <see cref="Diagnostic" /> (see <see cref="DiagnosticType" />).
    /// </summary>
    public DiagnosticType severity { get; }

    /// <summary>
    /// The user defined code to describe what caused this <see cref="Diagnostic" />.
    /// </summary>
    public int? code { get; }

    /// <summary>
    /// What module of code produced this <see cref="Diagnostic" />.
    /// </summary>
    public string module { get; }
}
