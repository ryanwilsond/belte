using System;
using System.Collections.Immutable;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A composite of a sequence of SourceTexts.
/// </summary>
internal sealed class CompositeText : SourceText {
    private readonly ImmutableArray<SourceText> _segments;
    private readonly int _length;
    private readonly int[] _segmentOffsets;

    private CompositeText(ImmutableArray<SourceText> segments) : base(null, null) {
        _segments = segments;

        foreach (var segment in _segments)
            _length += segment.length;

        _segmentOffsets = new int[segments.Length];
        int offset = 0;

        for (int i=0; i<_segmentOffsets.Length; i++) {
            _segmentOffsets[i] = offset;
            offset += _segments[i].length;
        }
    }

    public override int length => _length;

    public override char this[int index] {
        get {
            int position;
            int offset;
            GetIndexAndOffset(index, out position, out offset);

            return _segments[index][offset];
        }
    }

    internal override SourceText GetSubText(TextSpan span) {
        var sourceIndex = span.start;
        var count = span.length;

        int segmentIndex;
        int segmentOffset;
        GetIndexAndOffset(sourceIndex, out segmentIndex, out segmentOffset);

        var newSegments = ImmutableArray.CreateBuilder<SourceText>();

        while (segmentIndex < _segments.Length && count > 0) {
            var segment = _segments[segmentIndex];
            var copyLength = Math.Min(count, segment.length - segmentOffset);

            AddSegments(newSegments, segment.GetSubText(new TextSpan(segmentOffset, copyLength)));

            count -= copyLength;
            segmentIndex++;
            segmentOffset = 0;
        }

        return ToSourceText(newSegments.ToImmutable());
    }

    /// <summary>
    /// Adds a SourceText's segments to an array of segments.
    /// </summary>
    internal static void AddSegments(ImmutableArray<SourceText>.Builder segments, SourceText text) {
        var composite = text as CompositeText;

        if (composite == null)
            segments.Add(text);
        else
            segments.AddRange(composite._segments);
    }

    /// <summary>
    /// Converts an array of segments to a <see cref="SourceText" />.
    /// </summary>
    internal static SourceText ToSourceText(ImmutableArray<SourceText> segments) {
        if (segments.Length == 0)
            return SourceText.From("");
        else if (segments.Length == 1)
            return segments[0];
        else
            return new CompositeText(segments);
    }

    private void GetIndexAndOffset(int position, out int index, out int offset) {
        var tempIndex = _segmentOffsets.BinarySearch(position);
        index = tempIndex >= 0 ? tempIndex : (~tempIndex - 1);
        offset = position - _segmentOffsets[index];
    }
}
