using Diagnostics;

namespace Buckle;

/// <summary>
/// Compiler state options related to diagnostic reporting.
/// </summary>
public class TaskDiagnosticOptions {
    /// <summary>
    /// Lowest severity to report.
    /// </summary>
    public DiagnosticSeverity severity;

    /// <summary>
    /// Highest warning level to report.
    /// </summary>
    public int warningLevel;

    /// <summary>
    /// Warnings to not suppress.
    /// </summary>
    public DiagnosticInfo[] includeWarnings;

    /// <summary>
    /// Warnings to suppress.
    /// </summary>
    public DiagnosticInfo[] excludeWarnings;

    /// <summary>
    /// If to treat warnings as errors.
    /// </summary>
    public bool warningsAsErrors;

    /// <summary>
    /// Warnings to promote to errors.
    /// </summary>
    public DiagnosticInfo[] includeWarningsAsErrors;

    /// <summary>
    /// Warnings to not promote to errors.
    /// </summary>
    public DiagnosticInfo[] excludeWarningsAsErrors;
}
