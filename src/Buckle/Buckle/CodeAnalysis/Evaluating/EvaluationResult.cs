using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Result of an evaluation, including Diagnostics.
/// </summary>
internal sealed class EvaluationResult {
    /// <summary>
    /// Creates an <see cref="EvaluationResult" />, given the result and Diagnostics (does no computation).
    /// </summary>
    /// <param name="value">Result of evaluation.</param>
    /// <param name="diagnostics">Diagnostics associated with value.</param>
    internal EvaluationResult(object value, bool hasValue, BelteDiagnosticQueue diagnostics) {
        this.value = value;
        this.hasValue = hasValue;
        this.diagnostics = new BelteDiagnosticQueue();
        this.diagnostics.Move(diagnostics);
    }

    /// <summary>
    /// Creates an empty <see cref="EvaluationResult" />.
    /// </summary>
    internal EvaluationResult() : this(null, false, null) { }

    /// <summary>
    /// Diagnostics related to a single evaluation.
    /// </summary>
    internal BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// Value resulting from evaluation.
    /// </summary>
    internal object value { get; set; }

    /// <summary>
    /// Flag to distinguish the lack of value from the value of null.
    /// </summary>
    internal bool hasValue { get; set; }
}
