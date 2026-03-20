using System;
using System.IO;
using System.Linq;
using Diagnostics;
using Shared.Tests;
using Xunit;
using Xunit.Abstractions;

namespace CommandLine.Tests;

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
        DiagnosticSeverity lowestAssert = DiagnosticSeverity.Error,
        bool noInputFiles = false, params string[] filesToCreate) {
        var executingPath = AppDomain.CurrentDomain.BaseDirectory;

        var firstArgFilename = "BelteTestsAssertDiagnosticDefaultFile.blt";

        var argsList = args.AsEnumerable();

        if (!noInputFiles)
            argsList = argsList.Prepend(firstArgFilename);

        argsList = argsList.Prepend("--noout");
        argsList = argsList.Prepend("--severity=all");

        foreach (var file in filesToCreate.ToList().Append(firstArgFilename)) {
            var fileStream = File.Create(Path.Combine(executingPath, file));
            fileStream.Close();
        }

        var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);
        BuckleCommandLine.ProcessArgs(argsList.ToArray());

        var expectedDiagnostics = AnnotatedText.UnindentLines(diagnosticText);
        var diagnostics = stringWriter.ToString().Split(Environment.NewLine).ToList();

        diagnostics = diagnostics
            .Where(t => !string.IsNullOrEmpty(t))
            .Where(t => !Enum.TryParse<DiagnosticSeverity>(t.Split(' ')[1], true, out var severity) ||
                (int)severity >= (int)lowestAssert)
            .ToList();

        if (expectedDiagnostics.Length != diagnostics.Count) {
            writer.WriteLine($"Input: {string.Join(' ', argsList)}");

            foreach (var diagnostic in diagnostics)
                writer.WriteLine(diagnostic);
        }

        Assert.Equal(expectedDiagnostics.Length, diagnostics.Count);

        for (var i = 0; i < expectedDiagnostics.Length; i++) {
            var diagnosticParts = diagnostics[i].Split(": ").Skip(2);
            var diagnostic = (!diagnosticParts.Any()
                ? diagnostics[i]
                : diagnosticParts.Count() == 1
                    ? diagnosticParts.Single()
                    : string.Join(": ", diagnosticParts)).Trim();

            var expectedMessage = expectedDiagnostics[i];
            Assert.Equal(expectedMessage, diagnostic);
        }
    }
}
