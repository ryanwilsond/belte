using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Buckle.CodeAnalysis.Text;

namespace Buckle.Tests.CodeAnalysis;

internal sealed class AnnotatedText {
    internal string text { get; }
    internal ImmutableArray<TextSpan> spans { get; }

    private AnnotatedText(string text, ImmutableArray<TextSpan> spans) {
        this.text = text;
        this.spans = spans;
    }

    internal static AnnotatedText Parse(string text) {
        text = Unindent(text);

        var textBuilder = new StringBuilder();
        var spanBuilder = ImmutableArray.CreateBuilder<TextSpan>();
        var startStack = new Stack<int>();

        var position = 0;

        foreach (var c in text) {
            if (c == '[') {
                startStack.Push(position);
            } else if (c == ']') {
                if (startStack.Count == 0)
                    throw new ArgumentException("']' without corresponding '[' in text", nameof(text));

                var start = startStack.Pop();
                var end = position;
                var span = TextSpan.FromBounds(start, end);
                spanBuilder.Add(span);
            } else {
                position++;
                textBuilder.Append(c);
            }
        }

        if (startStack.Count != 0)
            throw new ArgumentException("'[' without corresponding ']' in text", nameof(text));

        return new AnnotatedText(textBuilder.ToString(), spanBuilder.ToImmutable());
    }

    private static string Unindent(string text) {
        var lines = UnindentLines(text);
        return string.Join(Environment.NewLine, lines);
    }

    internal static string[] UnindentLines(string text) {
        var lines = new List<string>();

        using (var stringReader = new StringReader(text)) {
            string line;

            while ((line = stringReader.ReadLine()) != null)
                lines.Add(line);
        }

        var minIndent = int.MaxValue;
        for (int i=0; i<lines.Count; i++) {
            var line = lines[i];

            if (line.Trim().Length == 0) {
                lines[i] = string.Empty;
                continue;
            }

            var indent = line.Length - line.TrimStart().Length;
            minIndent = Math.Min(minIndent, indent);
        }

        for (var i=0; i<lines.Count; i++) {
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
}
