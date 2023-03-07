using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Belte.CommandLine;
using Diagnostics;
using Shared.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Belte.Tests;

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
        bool assertWarnings = false, bool noInputFiles = false, params string[] filesToCreate) {
        var appSettings = new AppSettings();
        appSettings.executingPath = AppDomain.CurrentDomain.BaseDirectory;
        appSettings.resourcesPath = Path.Combine(appSettings.executingPath, "Resources");

        var firstArgFilename = "BelteTestsAssertDiagnosticDefaultFile.blt";

        var argsList = args.AsEnumerable<string>();

        if (!noInputFiles)
            argsList = argsList.Prepend(firstArgFilename);

        argsList = argsList.Prepend("--no-out");

        foreach (var file in filesToCreate.ToList().Append(firstArgFilename)) {
            var fileStream = File.Create(Path.Combine(appSettings.executingPath, file));
            fileStream.Close();
        }

        var stringWriter = new StringWriter();
        Console.SetOut(stringWriter);
        BuckleCommandLine.ProcessArgs(argsList.ToArray(), appSettings);

        var expectedDiagnostics = AnnotatedText.UnindentLines(diagnosticText);
        var diagnostics = stringWriter.ToString().Split(Environment.NewLine).ToList();

        diagnostics = assertWarnings
            ? diagnostics.Where(t => !string.IsNullOrEmpty(t)).ToList()
            : diagnostics.Where(t => !t.Contains(": Warning: ") && !string.IsNullOrEmpty(t)).ToList();

        if (expectedDiagnostics.Length != diagnostics.Count) {
            writer.WriteLine($"Input: {String.Join(' ', argsList)}");

            foreach (var diagnostic in diagnostics)
                writer.WriteLine(diagnostic);
        }

        Assert.Equal(expectedDiagnostics.Length, diagnostics.Count);

        for (int i=0; i<expectedDiagnostics.Length; i++) {
            var diagnosticParts = diagnostics[i].Split(": ").Skip(2);
            var diagnostic = diagnosticParts.Count() == 1
                ? diagnosticParts.Single()
                : string.Join(": ", diagnosticParts);

            var expectedMessage = expectedDiagnostics[i];
            Assert.Equal(expectedMessage, diagnostic);
        }
    }
}
