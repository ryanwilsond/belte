using System;
using Buckle.CodeAnalysis;
using Buckle.CodeAnalysis.Evaluating;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Diagnostics;
using Buckle.Libraries;
using Shared.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Buckle.Tests;

/// <summary>
/// All assertions used by Buckle tests.
/// </summary>
internal static class Assertions {
    private static CompilationOptions DefaultEvalOptions
        => new CompilationOptions(BuildMode.Evaluate, OutputKind.ConsoleApplication, []);
    private static CompilationOptions DefaultExecOptions
        => new CompilationOptions(BuildMode.Execute, OutputKind.ConsoleApplication, []);

    private readonly static Compilation BaseCompilation;

    static Assertions() {
        var compilation = LibraryHelpers.LoadLibraries();
        _ = compilation.boundProgram;
        BaseCompilation = compilation;
    }

    /// <summary>
    /// Asserts that a piece of Belte code evaluates to a value.
    /// </summary>
    /// <param name="text">Belte code.</param>
    /// <param name="expectedValue">Expected result.</param>
    internal static void AssertValue(string text, object expectedValue, bool evaluator = true, bool executor = true) {
        var syntaxTree = SyntaxTree.Parse(text);

        object execResult = null;
        EvaluationResult evalResult = null;
        object computedValue = null;

        if (executor) {
            var execCompilation = Compilation.CreateScript(
                "Tests",
                DefaultExecOptions,
                syntaxTree,
                BaseCompilation
            );

            var execDiags = execCompilation.Execute(false, false, null, false, out execResult);
            Assert.Empty(execDiags.Errors().ToArray());

            computedValue = execResult;
        }

        if (evaluator) {
            var evalCompilation = Compilation.CreateScript(
                "Tests",
                DefaultEvalOptions,
                syntaxTree,
                BaseCompilation
            );

            evalResult = evalCompilation.Evaluate(false);
            Assert.Empty(evalResult.diagnostics.Errors().ToArray());

            computedValue = evalResult.value;
        }

        if (computedValue is double && Convert.ToDouble(expectedValue).CompareTo(computedValue) == 0)
            expectedValue = Convert.ToDouble(expectedValue);
        else if (computedValue is long && Convert.ToInt64(expectedValue).CompareTo(computedValue) == 0)
            expectedValue = Convert.ToInt64(expectedValue);
        else if (computedValue is short && Convert.ToInt16(expectedValue).CompareTo(computedValue) == 0)
            expectedValue = Convert.ToInt16(expectedValue);
        else if (computedValue is sbyte && Convert.ToSByte(expectedValue).CompareTo(computedValue) == 0)
            expectedValue = Convert.ToSByte(expectedValue);
        else if (computedValue is ushort && Convert.ToUInt16(expectedValue).CompareTo(computedValue) == 0)
            expectedValue = Convert.ToUInt16(expectedValue);
        else if (computedValue is byte && Convert.ToByte(expectedValue).CompareTo(computedValue) == 0)
            expectedValue = Convert.ToByte(expectedValue);
        else if (computedValue is uint && Convert.ToUInt32(expectedValue).CompareTo(computedValue) == 0)
            expectedValue = Convert.ToUInt32(expectedValue);
        else if (computedValue is ulong && Convert.ToUInt64(expectedValue).CompareTo(computedValue) == 0)
            expectedValue = Convert.ToUInt64(expectedValue);

        if (evaluator)
            Assert.Equal(expectedValue, evalResult.value);

        if (executor)
            Assert.Equal(expectedValue, execResult);
    }

    /// <summary>
    /// Asserts that a piece of Belte code throws an exception when evaluating.
    /// </summary>
    /// <param name="text">Belte code.</param>
    /// <param name="writer">Writer to write debug info to if the assertion fails.</param>
    /// <param name="exceptions">Expected exception(s) thrown.</param>
    internal static void AssertExceptions(string text, ITestOutputHelper writer, params Exception[] exceptions) {
        var syntaxTree = SyntaxTree.Parse(text);
        var compilation = Compilation.CreateScript(
            "Tests",
            DefaultEvalOptions,
            syntaxTree,
            BaseCompilation
        );

        var result = compilation.Evaluate(false);

        if (exceptions.Length != result.exceptions.Count) {
            writer.WriteLine($"Input: {text}");

            foreach (var exception in result.exceptions)
                writer.WriteLine($"Exception ({exception}): {exception.Message}");
        }

        Assert.Equal(exceptions.Length, result.exceptions.Count);

        for (var i = 0; i < exceptions.Length; i++)
            Assert.Equal(exceptions[i].GetType(), result.exceptions[i].GetType());
    }

    /// <summary>
    /// Asserts that a piece of Belte code will produce a diagnostic when compiling.
    /// </summary>
    /// <param name="text">Belte code.</param>
    /// <param name="diagnosticText">Diagnostic message(s).</param>
    /// <param name="writer">Writer to write debug into to if the assertion fails.</param>
    /// <param name="assertWarnings">If to assert against warnings as well as errors.</param>
    /// <param name="script">If the compilation should be in script mode.</param>
    internal static void AssertDiagnostics(
        string text,
        string diagnosticText,
        ITestOutputHelper writer,
        bool assertWarnings = false,
        bool script = true) {
        var annotatedText = AnnotatedText.Parse(text);
        var syntaxTree = SyntaxTree.Parse(annotatedText.text);

        var tempDiagnostics = new BelteDiagnosticQueue();
        var treeDiagnostics = syntaxTree.GetDiagnostics();

        if (treeDiagnostics.AnyErrors()) {
            tempDiagnostics.Move(treeDiagnostics);
        } else {
            var compilation = script
                ? Compilation.CreateScript(
                    "Tests",
                    DefaultEvalOptions,
                    syntaxTree,
                    BaseCompilation
                )
                : Compilation.Create("Tests", DefaultEvalOptions, BaseCompilation, syntaxTree);

            var result = compilation.Evaluate(false);
            tempDiagnostics = result.diagnostics;
        }

        var expectedDiagnostics = AnnotatedText.UnindentLines(diagnosticText);

        if (annotatedText.spans.Length != expectedDiagnostics.Length)
            throw new Exception("must mark as many spans as there are diagnostics");

        var diagnostics = assertWarnings
            ? tempDiagnostics
            : tempDiagnostics.Errors();

        if (expectedDiagnostics.Length != diagnostics.Count) {
            writer.WriteLine($"Input: {annotatedText.text}");

            foreach (var diagnostic in diagnostics.ToList())
                writer.WriteLine($"Diagnostic ({diagnostic.info.severity}): {diagnostic.message}");
        }

        Assert.Equal(expectedDiagnostics.Length, diagnostics.Count);

        // All this does is ensure predictable ordering
        diagnostics = BelteDiagnosticQueue.CleanDiagnostics(diagnostics);

        for (var i = 0; i < expectedDiagnostics.Length; i++) {
            var diagnostic = diagnostics.Pop();

            var expectedMessage = expectedDiagnostics[i];
            var actualMessage = diagnostic.message;
            Assert.Equal(expectedMessage, actualMessage);

            var expectedSpan = annotatedText.spans[i];
            var actualSpan = diagnostic.location?.span
                ?? TextSpan.FromBounds(annotatedText.text.Length, annotatedText.text.Length);

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
        var options = new CompilationOptions(buildMode, OutputKind.ConsoleApplication, [], false, false);
        var compilation = Compilation.Create(
            "EmitterTests",
            options,
            BaseCompilation,
            syntaxTree
        );

        var result = compilation.EmitToString(out var diagnostics);

        Assert.Empty(diagnostics.Errors().ToArray());
        Assert.Equal(expectedText, result);
    }
}
