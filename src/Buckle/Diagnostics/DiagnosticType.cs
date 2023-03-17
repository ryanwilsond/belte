
namespace Diagnostics;

/// <summary>
/// Severity of <see cref="Diagnostic" />, does not effect how the <see cref="DiagnosticQueue<T>" /> interacts with them.
/// </summary>
public enum DiagnosticType {
    Unknown,
    Fatal,
    Error,
    Warning,
    Info,
}
