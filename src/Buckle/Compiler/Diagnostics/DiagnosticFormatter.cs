using System;
using System.Text.RegularExpressions;
using Buckle.CodeAnalysis.Text;
using Diagnostics;

namespace Buckle.Diagnostics;

public static partial class DiagnosticFormatter {
    public static string Format(BelteDiagnostic diagnostic) {
        return ToDisplayParts(diagnostic, diagnostic.location).ToString();
    }

    public static void PrettyPrint(BelteDiagnostic diagnostic, ConsoleColor? foregroundColor = null) {
        ToDisplayParts(diagnostic, diagnostic.location, foregroundColor).Write();
    }

    public static void PrettyPrint(Diagnostic diagnostic, ConsoleColor? foregroundColor = null) {
        ToDisplayParts(diagnostic, null, foregroundColor).Write();
    }

    public static void PrettyPrintException(Exception exception, ConsoleColor? foregroundColor = null) {
        ToDisplayParts(exception, foregroundColor).Write();
    }

    private static DisplayParts ToDisplayParts(Exception exception, ConsoleColor? foregroundColor = null) {
        var displayParts = new DisplayParts();
        var initialColor = foregroundColor ?? Console.ForegroundColor;
        var highlightColor = ConsoleColor.Red;

        displayParts.Add($"Unhandled exception. {exception.GetType()}: ", highlightColor);
        displayParts.Add($"{exception.Message}\n", initialColor);

        if (exception is not BelteEvaluatorException belteException || belteException.location is null)
            return displayParts;

        displayParts.Add("    at ", initialColor);
        AddLocationToDisplayParts(displayParts, belteException.location, initialColor);

        displayParts.Add("\n", initialColor);

        AddTextAtLocation(displayParts, belteException.location, [], initialColor, highlightColor);

        return displayParts;
    }

    private static void AddLocationToDisplayParts(
        DisplayParts displayParts,
        TextLocation location,
        ConsoleColor color) {
        var span = location.span;
        var text = location.text;

        var lineNumber = text.GetLineIndex(span.start);
        var line = text.GetLine(lineNumber);
        var column = span.start - line.start + 1;

        var fileName = location.fileName;

        if (!string.IsNullOrEmpty(fileName))
            displayParts.Add($"{fileName}:", color);

        displayParts.Add($"{lineNumber + 1}:{column}: ", color);
    }

    private static DisplayParts ToDisplayParts(
        Diagnostic diagnostic,
        TextLocation location,
        ConsoleColor? foregroundColor = null) {
        var displayParts = new DisplayParts();
        var initialColor = foregroundColor ?? Console.ForegroundColor;

        if (location?.span is not null)
            AddLocationToDisplayParts(displayParts, location, initialColor);

        var highlightColor = ConsoleColor.White;
        var severity = diagnostic.info.severity;

        switch (severity) {
            case DiagnosticSeverity.Debug:
                highlightColor = ConsoleColor.DarkGray;
                displayParts.Add("debug", highlightColor);
                break;
            case DiagnosticSeverity.Info:
                highlightColor = ConsoleColor.Yellow;
                displayParts.Add("info", highlightColor);
                break;
            case DiagnosticSeverity.Warning:
                highlightColor = ConsoleColor.Magenta;
                displayParts.Add("warning", highlightColor);
                break;
            case DiagnosticSeverity.Error:
                highlightColor = ConsoleColor.Red;
                displayParts.Add("error", highlightColor);
                break;
            case DiagnosticSeverity.Fatal:
                highlightColor = ConsoleColor.Red;
                displayParts.Add("fatal", highlightColor);
                break;
        }

        if (diagnostic.info.code is not null && diagnostic.info.code > 0)
            displayParts.Add($" {diagnostic.info}: ", highlightColor);
        else
            displayParts.Add(": ", highlightColor);

        displayParts.Add($"{diagnostic.message}\n", initialColor);

        if (location?.span is not null)
            AddTextAtLocation(displayParts, location, diagnostic.suggestions, initialColor, highlightColor);

        return displayParts;
    }

    private static void AddTextAtLocation(
        DisplayParts displayParts,
        TextLocation location,
        string[] suggestions,
        ConsoleColor initialColor,
        ConsoleColor highlightColor) {
        var span = location.span;
        var text = location.text;

        var lineNumber = text.GetLineIndex(span.start);
        var line = text.GetLine(lineNumber);
        var column = span.start - line.start + 1;
        var lineText = line.ToString();

        if (text.IsAtEndOfInput(span))
            return;

        var prefixSpan = TextSpan.FromBounds(line.start, span.start);
        var suffixSpan = span.end > line.end
            ? TextSpan.FromBounds(line.end, line.end)
            : TextSpan.FromBounds(span.end, line.end);

        var prefix = text.ToString(prefixSpan);
        var focus = text.ToString(span);
        var suffix = text.ToString(suffixSpan);

        displayParts.Add($" {prefix}", initialColor);
        displayParts.Add(focus, highlightColor);
        displayParts.Add($"{suffix}\n", initialColor);

        var markerPrefix = " " + MyRegex().Replace(prefix, " ");
        var marker = "^";

        if (span.length > 0 && column != lineText.Length)
            marker += new string('~', span.length - 1);

        displayParts.Add($"{markerPrefix}{marker}\n", highlightColor);

        if (suggestions.Length > 0) {
            var firstSuggestion = suggestions[0].Replace("%", focus);
            displayParts.Add($"{markerPrefix}{firstSuggestion}\n", ConsoleColor.Green);

            for (var i = 1; i < suggestions.Length; i++) {
                var suggestion = suggestions[i].Replace("%", focus);
                displayParts.Add(string.Concat(markerPrefix.AsSpan(0, markerPrefix.Length - 3), "or "), initialColor);
                displayParts.Add($"{suggestion}\n", ConsoleColor.Green);
            }
        }
    }

    [GeneratedRegex(@"\S")]
    private static partial Regex MyRegex();
}
