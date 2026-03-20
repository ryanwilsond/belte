
namespace Diagnostics;

/// <summary>
/// Severity of <see cref="Diagnostic" />,
/// does not effect how the <see cref="DiagnosticQueue<T>" /> interacts with them.
/// </summary>
public enum DiagnosticSeverity : byte {
    All = 0,
    Debug = 1,
    Info = 2,
    Warning = 3,
    Error = 4,
    Fatal = 5,
}
