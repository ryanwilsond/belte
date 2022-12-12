using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Result of an evaluation, including diagnostics.
/// </summary>
internal sealed class EvaluationResult {
    /// <summary>
    /// Creates an evaluation result, given the result and diagnostics (does no computation).
    /// </summary>
    /// <param name="value">Result of evaluation</param>
    /// <param name="diagnostics">Diagnostics associated with value</param>
    internal EvaluationResult(object value, BelteDiagnosticQueue diagnostics) {
        this.value = value;
        this.diagnostics = new BelteDiagnosticQueue();
        this.diagnostics.Move(diagnostics);
    }

    /// <summary>
    /// Creates an empty evaluation result.
    /// </summary>
    internal EvaluationResult() : this(null, null) { }

    /// <summary>
    /// Diagnostics related to a single evaluation.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// Value resulting from evaluation.
    /// </summary>
    internal object value { get; set; }
}
