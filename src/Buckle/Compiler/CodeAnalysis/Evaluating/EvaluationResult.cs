using System;
using System.Collections.Generic;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Evaluating;

/// <summary>
/// Result of an evaluation, including Diagnostics.
/// </summary>
public sealed class EvaluationResult {
    /// <summary>
    /// Creates an <see cref="EvaluationResult" />, given the result and Diagnostics (does no computation).
    /// </summary>
    /// <param name="value">Result of evaluation.</param>
    /// <param name="diagnostics">Diagnostics associated with value.</param>
    internal EvaluationResult(
        object value, bool hasValue, BelteDiagnosticQueue diagnostics, List<Exception> exceptions) {
        this.value = value;
        this.hasValue = hasValue;
        this.diagnostics = new BelteDiagnosticQueue();
        this.diagnostics.Move(diagnostics);
        this.exceptions = exceptions is null ? new List<Exception>() : new List<Exception>(exceptions);
    }

    /// <summary>
    /// Creates an empty <see cref="EvaluationResult" />.
    /// </summary>
    internal EvaluationResult() : this(null, false, null, null) { }

    /// <summary>
    /// Diagnostics related to a single evaluation.
    /// </summary>
    public BelteDiagnosticQueue diagnostics { get; set; }

    /// <summary>
    /// Value resulting from evaluation.
    /// </summary>
    public object value { get; set; }

    /// <summary>
    /// Flag to distinguish the lack of value from the value of null.
    /// </summary>
    public bool hasValue { get; set; }

    /// <summary>
    /// All exceptions thrown while evaluating.
    /// </summary>
    internal List<Exception> exceptions { get; set; }
}
