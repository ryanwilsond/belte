using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A source of text the compiler uses, usually representing a source file.
/// </summary>
public class SourceText {
    private readonly string _text;

    /// <summary>
    /// Creates a <see cref="SourceText" /> provided the file name and contents.
    /// </summary>
    /// <param name="fileName">File name of the <see cref="SourceText" /> (where the text came from).</param>
    /// <param name="text">The contents of the file the <see cref="SourceText" /> comes from.</param>
    protected SourceText(string fileName, string text) {
        lines = ParseLines(this, text);
        _text = text;
        this.fileName = fileName;
    }

    /// <summary>
    /// All lines in the <see cref="SourceText" />.
    /// </summary>
    public ImmutableArray<TextLine> lines { get; }

    /// <summary>
    /// The file name of the source file.
    /// </summary>
    public string fileName { get; }

    /// <summary>
    /// Indexing the source file contents.
    /// </summary>
    public virtual char this[int index] => _text[index];

    /// <summary>
    /// The length of the entire <see cref="SourceText" />.
    /// </summary>
    public virtual int length => _text.Length;

    /// <summary>
    /// Creates a <see cref="SourceText" /> from a text, not necessarily relating to a source file.
    /// </summary>
    /// <param name="text">Text.</param>
    /// <param name="fileName">Optional filename if sourced from a file.</param>
    /// <returns>New <see cref="SourceText" />.</returns>
    public static SourceText From(string text, string fileName = "") {
        return new SourceText(fileName, text);
    }

    /// <summary>
    /// Gets a line index based on an absolute index.
    /// </summary>
    /// <param name="position">Absolute index.</param>
    /// <returns>Line index.</returns>
    public int GetLineIndex(int position) {
        int lower = 0;
        int upper = lines.Length - 1;

        while (lower <= upper) {
            int index = lower + (upper - lower) / 2;
            int start = lines[index].start;

            if (position == start)
                return index;
            if (start > position)
                upper = index - 1;
            else
                lower = index + 1;
        }

        return lower - 1;
    }

    public override string ToString() => _text;

    /// <summary>
    /// String representation of part of the text.
    /// </summary>
    /// <param name="start">Start index (absolute).</param>
    /// <param name="length">Length of text to grab.</param>
    /// <returns>Substring of the text.</returns>
    public string ToString(int start, int length) => _text.Substring(start, length);

    /// <summary>
    /// String representation of part of the text.
    /// </summary>
    /// <param name="span"><see cref="TextSpan" /> of what text to grab (inclusive).</param>
    /// <returns>Substring of the text.</returns>
    public string ToString(TextSpan span) => ToString(span.start, span.length);

    /// <summary>
    /// Checks if the <see cref="TextSpan" /> starts at the end of the text.
    /// Does not return true if the span reaches the end, only if it starts at the end.
    /// If the <see cref="TextSpan" /> extends beyond the text, the result will not be different.
    /// </summary>
    /// <param name="span"><see cref="TextSpan" /> to check.</param>
    /// <returns>If the <see cref="TextSpan" /> is at the end of the text.</returns>
    public bool IsAtEndOfInput(TextSpan span) {
        if (span.start == _text.Length)
            return true;

        return false;
    }

    /// <summary>
    /// Constructs a new <see cref="SourceText" /> from this with the specified changes.
    /// </summary>
    public SourceText WithChanges(params TextChange[] changes) {
        return WithChanges(ImmutableArray.Create(changes));
    }

    /// <summary>
    /// Constructs a new <see cref="SourceText" /> from this with the specified changes.
    /// </summary>
    public SourceText WithChanges(IEnumerable<TextChange> changes) {
        if (!changes.Any())
            return this;

        var segments = ImmutableArray.CreateBuilder<SourceText>();
        var changeRanges = ImmutableArray.CreateBuilder<TextChangeRange>();

        int position = 0;

        foreach (var change in changes) {
            if (change.span.end > length)
                throw new ArgumentException($"Changes ({nameof(changes)}) must be within bounds of SourceText");

            if (change.span.start < position) {
                if (change.span.end <= changeRanges.Last().span.start) {
                    changes = (from c in changes
                               where !(c.span.length == 0) || c.newText?.Length > 0
                               orderby c.span
                               select c).ToList();

                    return WithChanges(changes);
                }

                throw new ArgumentException($"Changes ({nameof(changes)}) must not overlap");
            }

            var newTextLength = change.newText?.Length ?? 0;

            if (change.span.length == 0 && newTextLength == 0)
                continue;

            if (change.span.start > position) {
                var subText = GetSubText(new TextSpan(position, change.span.start - position));
                CompositeText.AddSegments(segments, subText);
            }

            if (newTextLength > 0) {
                var segment = SourceText.From(change.newText);
                CompositeText.AddSegments(segments, segment);
            }

            position = change.span.end;
            changeRanges.Add(new TextChangeRange(change.span, newTextLength));
        }

        if (position == 0 && segments.Count == 0)
            return this;

        if (position < length) {
            var subText = GetSubText(new TextSpan(position, length - position));
            CompositeText.AddSegments(segments, subText);
        }

        var newText = CompositeText.ToSourceText(segments.ToImmutable());
        return new ChangedText(this, newText, changeRanges.ToImmutable());
    }

    /// <summary>
    /// Gets all changes between <param name="oldText" /> and this.
    /// </summary>
    /// <returns>A collection of all changes (a delta collection).</returns>
    internal virtual ImmutableArray<TextChangeRange> GetChangeRanges(SourceText oldText) {
        if (oldText == this)
            return ImmutableArray<TextChangeRange>.Empty;

        return ImmutableArray.Create(new TextChangeRange(new TextSpan(0, oldText.length), length));
    }

    /// <summary>
    /// Gets a <see cref="SourceText" /> that contains the characters in the specified span of this text.
    /// </summary>
    internal virtual SourceText GetSubText(TextSpan span) {
        var spanLength = span.length;

        if (spanLength == 0)
            return SourceText.From("");
        else if (spanLength == length && span.start == 0)
            return this;
        else
            return new SubText(this, span);
    }

    private static ImmutableArray<TextLine> ParseLines(SourceText pointer, string text) {
        var result = ImmutableArray.CreateBuilder<TextLine>();

        int position = 0;
        int lineStart = 0;

        while (position < text.Length) {
            var lineBreakWidth = GetLineBreakWidth(text, position);

            if (lineBreakWidth == 0) {
                position++;
            } else {
                AddLine(result, pointer, position, lineStart, lineBreakWidth);
                position += lineBreakWidth;
                lineStart = position;
            }
        }

        if (position >= lineStart)
            AddLine(result, pointer, position, lineStart, 0);

        return result.ToImmutable();
    }

    private static void AddLine(ImmutableArray<TextLine>.Builder result, SourceText pointer,
        int position, int lineStart, int lineBreakWidth) {
        var lineLength = position - lineStart;
        var line = new TextLine(pointer, lineStart, lineLength, lineLength + lineBreakWidth);
        result.Add(line);
    }

    private static int GetLineBreakWidth(string text, int i) {
        var c = text[i];
        var l = i + 1 >= text.Length ? '\0' : text[i + 1];

        if (c == '\r' && l == '\n')
            return 2;
        if (c == '\r' || c == '\n')
            return 1;

        return 0;
    }
}
