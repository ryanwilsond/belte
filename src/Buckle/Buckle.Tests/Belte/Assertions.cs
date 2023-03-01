using System;
using System.IO;
using System.Linq;
using Belte;
using Belte.CommandLine;
using Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace Buckle.Tests.Belte;

/// <summary>
/// All assertions used by Belte tests.
/// </summary>
internal static class Assertions {
    /// <summary>
    /// Asserts that a set of command line arguments will produce a diagnostic.
    /// </summary>
    /// <param name="args">Simulated command line arguments.</param>
    /// <param name="diagnosticText">Diagnostic message(s).</param>
    /// <param name="writer">Writer to write debug into to if the assertion fails.</param>
    /// <param name="assertWarnings">If to assert against warnings as well as errors.</param>
    /// <param name="filesToCreate">Files to create that are needed by the test.</param>
    internal static void AssertDiagnostics(
        string[] args, string diagnosticText, ITestOutputHelper writer,
        bool assertWarnings = false, params string[] filesToCreate) {
        var appSettings = new AppSettings();
        appSettings.executingPath = AppDomain.CurrentDomain.BaseDirectory;
        appSettings.resourcesPath = Path.Combine(appSettings.executingPath, "Resources");

        var firstArgFilename = "BelteTestsAssertDiagnosticDefaultFile.blt";
        var argsList = args.ToList().Append(firstArgFilename);

        foreach (var file in filesToCreate.ToList().Append(firstArgFilename)) {
            var fileStream = File.Create(Path.Combine(appSettings.executingPath, file));
            fileStream.Close();
        }

        var diagnostics = new DiagnosticQueue<Diagnostic>();
        BuckleCommandLine.ProcessArgs(argsList.ToArray(), appSettings, ref diagnostics);

        var expectedDiagnostics = AnnotatedText.UnindentLines(diagnosticText);

        diagnostics = assertWarnings
            ? diagnostics
            : diagnostics.FilterOut(DiagnosticType.Warning);

        if (expectedDiagnostics.Length != diagnostics.count) {
            writer.WriteLine($"Input: {String.Join(' ', argsList)}");

            foreach (var diagnostic in diagnostics.AsList())
                writer.WriteLine($"Diagnostic ({diagnostic.info.severity}): {diagnostic.message}");
        }

        Assert.Equal(expectedDiagnostics.Length, diagnostics.count);

        for (int i=0; i<expectedDiagnostics.Length; i++) {
            var diagnostic = diagnostics.Pop();

            var expectedMessage = expectedDiagnostics[i];
            var actualMessage = diagnostic.message;
            Assert.Equal(expectedMessage, actualMessage);
        }
    }
}
