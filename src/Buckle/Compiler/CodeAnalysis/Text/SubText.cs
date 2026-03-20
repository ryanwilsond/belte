using System;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A <see cref="SourceText" /> that represents a subrange of another <see cref="SourceText" />.
/// </summary>
internal sealed class SubText : SourceText {
    /// <summary>
    /// Creates an instance of <see cref="SubText" />.
    /// </summary>
    internal SubText(SourceText text, TextSpan span) {
        underlyingText = text;
        underlyingSpan = span;
    }

    internal SourceText underlyingText { get; }

    internal TextSpan underlyingSpan { get; }

    public override int lineCount => underlyingText.lineCount;

    public override int length => underlyingSpan.length;

    public override char this[int index] => underlyingText[underlyingSpan.start + index];

    public override string ToString(TextSpan span) {
        return underlyingText.ToString(GetCompositeSpan(span.start, span.length));
    }

    public override string ToString() {
        return underlyingText.ToString(GetCompositeSpan(0, length));
    }

    public override TextLine GetLine(int index) {
        var line = base.GetLine(index);
        return new TextLine(this, line.start - underlyingSpan.start, line.length, line.lengthWithBreak);
    }

    public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) {
        var span = GetCompositeSpan(sourceIndex, count);
        underlyingText.CopyTo(span.start, destination, destinationIndex, span.length);
    }

    public override int GetLineIndex(int position) {
        return base.GetLineIndex(position + underlyingSpan.start);
    }

    internal override SourceText GetSubText(TextSpan span) {
        return new SubText(underlyingText, GetCompositeSpan(span.start, span.length));
    }

    private protected override void EnsureLines() {
        if (_lines is not null)
            return;

        var lines = underlyingText.GetLines();
        var builder = ArrayBuilder<TextLine>.GetInstance();

        foreach (var line in lines) {
            var start = line.spanWithBreak.start;
            var end = line.spanWithBreak.end;

            //       [span]  or  [span]
            // [line]                  [line]
            if (end <= underlyingSpan.start || start >= underlyingSpan.end)
                continue;

            //  [span]
            //  [line]
            if (start >= underlyingSpan.start && end <= underlyingSpan.end) {
                builder.Add(line);
                continue;
            }

            //   [span]
            // [--line--]
            if (start <= underlyingSpan.start && end >= underlyingSpan.end) {
                var newLength = Math.Min(underlyingSpan.length, end - underlyingSpan.start);
                var subLine = new TextLine(underlyingText, underlyingSpan.start, newLength, newLength);
                builder.Add(subLine);
                continue;
            }

            //    [span]
            // [line]
            if (start <= underlyingSpan.start && end <= underlyingSpan.end) {
                var newLength = end - underlyingSpan.start;
                var subLine = new TextLine(underlyingText, underlyingSpan.start, newLength, newLength);
                builder.Add(subLine);
                continue;
            }

            // [span]
            //    [line]
            if (start >= underlyingSpan.start && end >= underlyingSpan.end) {
                var newLength = underlyingSpan.end - start;
                var subLine = new TextLine(underlyingText, start, newLength, newLength);
                builder.Add(subLine);
                continue;
            }
        }

        _lines = builder.ToImmutableAndFree();
    }

    private TextSpan GetCompositeSpan(int start, int length) {
        var compositeStart = Math.Min(underlyingText.length, underlyingSpan.start + start);
        var compositeEnd = Math.Min(underlyingText.length, compositeStart + length);
        return new TextSpan(compositeStart, compositeEnd - compositeStart);
    }
}
