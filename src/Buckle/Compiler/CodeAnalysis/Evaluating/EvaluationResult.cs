using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
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
        TypeSymbol type,
        bool hasValue,
        BelteDiagnosticQueue diagnostics,
        List<Exception> exceptions,
        bool lastOutputWasPrint,
        bool containsIO,
        Heap heap) {
        this.value = value;
        this.type = type;
        this.hasValue = hasValue;
        this.diagnostics = new BelteDiagnosticQueue();
        this.diagnostics.Move(diagnostics);
        this.exceptions = exceptions is null ? [] : exceptions;
        this.lastOutputWasPrint = lastOutputWasPrint;
        this.containsIO = containsIO;
        this.heap = heap;
    }

    /// <summary>
    /// Diagnostics related to a single evaluation.
    /// </summary>
    public BelteDiagnosticQueue diagnostics { get; }

    /// <summary>
    /// Value resulting from evaluation.
    /// </summary>
    public object value { get; private set; }

    /// <summary>
    /// Type of the result value.
    /// </summary>
    public ITypeSymbol type { get; private set; }

    /// <summary>
    /// Flag to distinguish the lack of value from the value of null.
    /// </summary>
    public bool hasValue { get; private set; }

    /// <summary>
    /// If the last output to the terminal was a `Print`, and not a `PrintLine`, meaning the caller might want to write
    /// an extra line to prevent formatting problems.
    /// </summary>
    public bool lastOutputWasPrint { get; private set; }

    /// <summary>
    /// If the submission contains File/Directory IO.
    /// </summary>
    public bool containsIO { get; private set; }

    /// <summary>
    /// All exceptions thrown while evaluating.
    /// </summary>
    public List<Exception> exceptions { get; }

    internal Heap heap { get; private set; }

    internal static EvaluationResult Failed(BelteDiagnosticQueue diagnostics) {
        return new EvaluationResult(null, null, false, diagnostics, null, false, false, null);
    }

    internal void Update(
        object value,
        TypeSymbol type,
        bool hasValue,
        BelteDiagnosticQueue diagnostics,
        List<Exception> exceptions,
        bool lastOutputWasPrint,
        bool containsIO,
        Heap heap) {
        if (hasValue) {
            this.value = value;
            this.hasValue = true;
            this.type = type;
        }

        this.diagnostics.PushRange(diagnostics);
        this.exceptions.AddRange(exceptions);
        this.lastOutputWasPrint = lastOutputWasPrint;
        this.containsIO |= containsIO;
        this.heap = heap;
    }
}
