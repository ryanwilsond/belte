using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Buckle.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Shared.Tests;

/// <summary>
/// Allows tracking TextSpans in a piece of text.
/// </summary>
public sealed class AnnotatedText {
    public string text { get; }
    public ImmutableArray<TextSpan> spans { get; }

    private AnnotatedText(string text, ImmutableArray<TextSpan> spans) {
        this.text = text;
        this.spans = spans;
    }

    /// <summary>
    /// Converts a piece of text into an <see cref="AnnotatedText" />. Removes square brackets, which indicate to
    /// track a span.
    /// </summary>
    /// <param name="text">Text to parse.</param>
    /// <returns>Parsed text.</returns>
    public static AnnotatedText Parse(string text) {
        text = Unindent(text);

        var textBuilder = new StringBuilder();
        var spanBuilder = ArrayBuilder<TextSpan>.GetInstance();
        var startStack = new Stack<int>();

        var position = 0;

        for (var i = 0; i < text.Length; i++) {
            if (text[i] == '\\' && i < text.Length - 1) {
                position++;

                if (text[i + 1] == '[' || text[i + 1] == ']') {
                    i++;
                    textBuilder.Append(text[i]);
                } else {
                    textBuilder.Append(text[i]);
                }

                continue;
            }

            if (text[i] == '[') {
                startStack.Push(position);
            } else if (text[i] == ']') {
                if (startStack.Count == 0)
                    throw new ArgumentException("']' without corresponding '[' in text", nameof(text));

                var start = startStack.Pop();
                var end = position;
                var span = TextSpan.FromBounds(start, end);
                spanBuilder.Add(span);
            } else {
                position++;
                textBuilder.Append(text[i]);
            }
        }

        if (startStack.Count > 0)
            throw new ArgumentException("'[' without corresponding ']' in text", nameof(text));

        return new AnnotatedText(textBuilder.ToString(), spanBuilder.ToImmutable());
    }

    /// <summary>
    /// Removes any leading indentation on a piece of text.
    /// </summary>
    /// <param name="text">Text to unindent.</param>
    /// <returns>Text without indentation.</returns>
    public static string[] UnindentLines(string text) {
        var lines = new List<string>();

        using (var stringReader = new StringReader(text)) {
            string line;

            while ((line = stringReader.ReadLine()) is not null)
                lines.Add(line);
        }

        var minIndent = int.MaxValue;

        for (var i = 0; i < lines.Count; i++) {
            var line = lines[i];

            if (line.Trim().Length == 0) {
                lines[i] = "";
                continue;
            }

            var indent = line.Length - line.TrimStart().Length;
            minIndent = Math.Min(minIndent, indent);
        }

        for (var i = 0; i < lines.Count; i++) {
            if (lines[i].Length == 0)
                continue;

            lines[i] = lines[i].Substring(minIndent);
        }

        while (lines.Count > 0 && lines[0].Length == 0)
            lines.RemoveAt(0);

        while (lines.Count > 0 && lines[lines.Count - 1].Length == 0)
            lines.RemoveAt(lines.Count - 1);

        return lines.ToArray();
    }

    private static string Unindent(string text) {
        var lines = UnindentLines(text);
        return string.Join(Environment.NewLine, lines);
    }
}
