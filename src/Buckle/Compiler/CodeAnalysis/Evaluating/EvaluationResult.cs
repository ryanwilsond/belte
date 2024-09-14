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
        object value,
        bool hasValue,
        BelteDiagnosticQueue diagnostics,
        List<Exception> exceptions,
        bool lastOutputWasPrint,
        bool containsIO) {
        this.value = value;
        this.hasValue = hasValue;
        this.diagnostics = new BelteDiagnosticQueue();
        this.diagnostics.Move(diagnostics);
        this.exceptions = exceptions is null ? new List<Exception>() : new List<Exception>(exceptions);
        this.lastOutputWasPrint = lastOutputWasPrint;
        this.containsIO = containsIO;
    }

    /// <summary>
    /// Creates an empty <see cref="EvaluationResult" />.
    /// </summary>
    internal EvaluationResult() : this(null, false, null, null, false, false) { }

    internal static EvaluationResult Failed(BelteDiagnosticQueue diagnostics) {
        return new EvaluationResult(null, false, diagnostics, null, false, false);
    }

    /// <summary>
    /// Diagnostics related to a single evaluation.
    /// </summary>
    public BelteDiagnosticQueue diagnostics { get; }

    /// <summary>
    /// Value resulting from evaluation.
    /// </summary>
    public object value { get; }

    /// <summary>
    /// Flag to distinguish the lack of value from the value of null.
    /// </summary>
    public bool hasValue { get; }

    /// <summary>
    /// If the last output to the terminal was a `Print`, and not a `PrintLine`, meaning the caller might want to write
    /// an extra line to prevent formatting problems.
    /// </summary>
    public bool lastOutputWasPrint { get; }

    /// <summary>
    /// If the submission contains File/Directory IO.
    /// </summary>
    public bool containsIO { get; }

    /// <summary>
    /// All exceptions thrown while evaluating.
    /// </summary>
    internal List<Exception> exceptions { get; }
}
