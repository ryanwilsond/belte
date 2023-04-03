using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A source of text the compiler uses, usually representing a source file.
/// </summary>
public abstract class SourceText {
    internal const int LargeObjectHeapLimitInChars = 40 * 1024;

    private const int CharBufferSize = 32 * 1024;
    private const int CharBufferCount = 5;

    private static readonly ObjectPool<char[]> _charArrayPool =
        new ObjectPool<char[]>(() => new char[CharBufferSize], CharBufferCount);

    protected ImmutableArray<TextLine> _lines;

    public ImmutableArray<TextLine> lines {
        get {
            EnsureLines();
            return _lines;
        }
    }

    /// <summary>
    /// Number of lines in the <see cref="SourceText" />.
    /// </summary>
    public virtual int lineCount => lines.Length;

    /// <summary>
    /// Indexing the source file contents.
    /// </summary>
    public abstract char this[int index] { get; }

    /// <summary>
    /// The length of the entire <see cref="SourceText" />.
    /// </summary>
    public abstract int length { get; }

    /// <summary>
    /// Creates a <see cref="SourceText" /> from a text, not necessarily relating to a source file.
    /// </summary>
    /// <param name="text">Text.</param>
    /// <param name="fileName">Optional filename if sourced from a file.</param>
    /// <returns>New <see cref="SourceText" />.</returns>
    public static SourceText From(string text, string fileName = "") {
        return new StringText(fileName, text);
    }

    /// <summary>
    /// Gets a line index based on an absolute index. Very expensive.
    /// </summary>
    /// <param name="position">Absolute index.</param>
    /// <returns>Line index.</returns>
    public int GetLineIndex(int position) {
        int lower = 0;
        int upper = lineCount - 1;

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

    public override string ToString() => ToString(new TextSpan(0, length));

    /// <summary>
    /// String representation of part of the text.
    /// </summary>
    /// <param name="span"><see cref="TextSpan" /> of what text to grab (inclusive).</param>
    /// <returns>Substring of the text.</returns>
    public virtual string ToString(TextSpan span) {
        var builder = PooledStringBuilder.GetInstance();
        var buffer = _charArrayPool.Allocate();

        int position = Math.Max(Math.Min(span.start, length), 0);
        int newLength = Math.Min(span.end, length) - position;
        builder.Builder.EnsureCapacity(newLength);

        while (position < length && newLength > 0) {
            int copyLength = Math.Min(buffer.Length, newLength);
            CopyTo(position, buffer, 0, copyLength);
            builder.Builder.Append(buffer, 0, copyLength);
            newLength -= copyLength;
            position += copyLength;
        }

        _charArrayPool.Free(buffer);

        return builder.ToStringAndFree();
    }

    /// <summary>
    /// Copy a range of characters from this to a destination array.
    /// </summary>
    public abstract void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count);

    /// <summary>
    /// Write this to a text writer.
    /// </summary>
    public virtual void Write(TextWriter writer) {
        var buffer = _charArrayPool.Allocate();

        try {
            int offset = 0;
            int end = length;

            while (offset < end) {
                int count = Math.Min(buffer.Length, end - offset);
                CopyTo(offset, buffer, 0, count);
                writer.Write(buffer, 0, count);
                offset += count;
            }
        } finally {
            _charArrayPool.Free(buffer);
        }
    }

    /// <summary>
    /// Checks if the <see cref="TextSpan" /> starts at the end of the text.
    /// Does not return true if the span reaches the end, only if it starts at the end.
    /// If the <see cref="TextSpan" /> extends beyond the text, the result will not be different.
    /// </summary>
    /// <param name="span"><see cref="TextSpan" /> to check.</param>
    /// <returns>If the <see cref="TextSpan" /> is at the end of the text.</returns>
    public bool IsAtEndOfInput(TextSpan span) {
        if (span.start == length)
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
    public virtual SourceText WithChanges(IEnumerable<TextChange> changes) {
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

        var newText = CompositeText.ToSourceText(segments);

        if (newText != null)
            return new ChangedText(this, newText, changeRanges.ToImmutable());
        else
            return this;
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

    protected static ImmutableArray<TextLine> ParseLines(SourceText pointer, string text) {
        var result = ImmutableArray.CreateBuilder<TextLine>();

        if (text == null)
            return result.ToImmutable();

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

    protected abstract void EnsureLines();

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
