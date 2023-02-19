using System;
using System.Collections.Generic;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;
using Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Buckle.Tests.Buckle;

/// <summary>
/// All assertions used by Buckle tests.
/// </summary>
internal static class Assertions {
    /// <summary>
    /// Asserts that a piece of Belte code evaluates to a value.
    /// </summary>
    /// <param name="text">Belte code.</param>
    /// <param name="expectedValue">Expected result.</param>
    internal static void AssertValue(string text, object expectedValue) {
        var syntaxTree = SyntaxTree.Parse(text);
        var compilation = Compilation.CreateScript(null, syntaxTree);
        var variables = new Dictionary<VariableSymbol, EvaluatorObject>();
        var _ = false;
        var result = compilation.Evaluate(variables, ref _);

        if (result.value is double && (Convert.ToDouble(expectedValue)).CompareTo(result.value) == 0)
            expectedValue = Convert.ToDouble(expectedValue);

        Assert.Empty(result.diagnostics.FilterOut(DiagnosticType.Warning).ToArray());
        Assert.Equal(expectedValue, result.value);
    }

    /// <summary>
    /// Asserts that a piece of Belte code throws an exception when evaluating.
    /// </summary>
    /// <param name="text">Belte code.</param>
    /// <param name="writer">Writer to write debug info to if the assertion fails.</param>
    /// <param name="exceptions">Expected exception(s) thrown.</param>
    internal static void AssertExceptions(string text, ITestOutputHelper writer, params Exception[] exceptions) {
        var syntaxTree = SyntaxTree.Parse(text);
        var compilation = Compilation.CreateScript(null, syntaxTree);
        var _ = false;
        var result = compilation.Evaluate(new Dictionary<VariableSymbol, EvaluatorObject>(), ref _);

        if (exceptions.Length != result.exceptions.Count) {
            writer.WriteLine($"Input: {text}");

            foreach (var exception in result.exceptions)
                writer.WriteLine($"Exception ({exception}): {exception.Message}");
        }

        Assert.Equal(exceptions.Length, result.exceptions.Count);

        for (int i=0; i<exceptions.Length; i++)
            Assert.Equal(exceptions[i].GetType(), result.exceptions[i].GetType());
    }

    /// <summary>
    /// Asserts that a piece of Belte code will produce a diagnostic when compiling.
    /// </summary>
    /// <param name="text">Belte code.</param>
    /// <param name="diagnosticText">Diagnostic message(s).</param>
    /// <param name="writer">Writer to write debug into to if the assertion fails.</param>
    /// <param name="assertWarnings">If to assert against warnings as well as errors.</param>
    internal static void AssertDiagnostics(
        string text, string diagnosticText, ITestOutputHelper writer, bool assertWarnings = false) {
        var annotatedText = AnnotatedText.Parse(text);
        var syntaxTree = SyntaxTree.Parse(annotatedText.text);

        var tempDiagnostics = new BelteDiagnosticQueue();

        if (syntaxTree.diagnostics.FilterOut(DiagnosticType.Warning).Any()) {
            tempDiagnostics.Move(syntaxTree.diagnostics);
        } else {
            var compilation = Compilation.CreateScript(null, syntaxTree);
            var _ = false;
            var result = compilation.Evaluate(new Dictionary<VariableSymbol, EvaluatorObject>(), ref _);
            tempDiagnostics = result.diagnostics;
        }

        var expectedDiagnostics = AnnotatedText.UnindentLines(diagnosticText);

        if (annotatedText.spans.Length != expectedDiagnostics.Length)
            throw new Exception("must mark as many spans as there are diagnostics");

        var diagnostics = assertWarnings
            ? tempDiagnostics
            : tempDiagnostics.FilterOut(DiagnosticType.Warning);

        if (expectedDiagnostics.Length != diagnostics.count) {
            writer.WriteLine($"Input: {annotatedText.text}");

            foreach (var diagnostic in diagnostics.AsList())
                writer.WriteLine($"Diagnostic ({diagnostic.info.severity}): {diagnostic.message}");
        }

        Assert.Equal(expectedDiagnostics.Length, diagnostics.count);

        for (int i=0; i<expectedDiagnostics.Length; i++) {
            var diagnostic = diagnostics.Pop();

            var expectedMessage = expectedDiagnostics[i];
            var actualMessage = diagnostic.message;
            Assert.Equal(expectedMessage, actualMessage);

            var expectedSpan = annotatedText.spans[i];
            var actualSpan = diagnostic.location.span;
            writer.WriteLine($"start: {expectedSpan.start}, {actualSpan.start}");
            Assert.Equal(expectedSpan.start, actualSpan.start);
            writer.WriteLine($"end: {expectedSpan.end}, {actualSpan.end}");
            Assert.Equal(expectedSpan.end, actualSpan.end);
            writer.WriteLine($"length: {expectedSpan.length}, {actualSpan.length}");
            Assert.Equal(expectedSpan.length, actualSpan.length);
        }
    }

    /// <summary>
    /// Asserts that a piece of Belte code emits into the expected text.
    /// </summary>
    /// <param name="text">Belte code.</param>
    /// <param name="expectedText">Expected text after emitting.</param>
    /// <param name="buildMode">Which emitter to use.</param>
    internal static void AssertText(string text, string expectedText, BuildMode buildMode) {
        var syntaxTree = SyntaxTree.Parse(text);
        var compilation = Compilation.Create(true, syntaxTree);
        var result = compilation.EmitToString(buildMode, "EmitterTests", false);

        Assert.Empty(compilation.diagnostics.FilterOut(DiagnosticType.Warning).ToArray());
        Assert.Equal(expectedText, result);
    }
}
