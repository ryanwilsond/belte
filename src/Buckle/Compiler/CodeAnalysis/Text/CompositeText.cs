using System;
using System.Collections.Immutable;
using System.Linq;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A composite of a sequence of SourceTexts.
/// </summary>
internal sealed class CompositeText : SourceText {
    private readonly ImmutableArray<SourceText> _segments;
    private readonly int _length;
    private readonly int[] _segmentOffsets;

    private CompositeText(ImmutableArray<SourceText> segments) {
        _segments = segments;

        foreach (var segment in _segments)
            _length += segment.length;

        _segmentOffsets = new int[segments.Length];
        var offset = 0;

        for (var i = 0; i < _segmentOffsets.Length; i++) {
            _segmentOffsets[i] = offset;
            offset += _segments[i].length;
        }
    }

    public override int length => _length;

    public override char this[int index] {
        get {
            GetIndexAndOffset(index, out var position, out var offset);

            return _segments[position][offset];
        }
    }

    private const int TargetSegmentCountAfterReduction = 32;
    private const int MaximumSegmentCountBeforeReduction = 64;
    private const int InitialSegmentSizeForCombining = 32;
    private const int MaximumSegmentSizeForCombining = int.MaxValue / 16;

    public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) {
        if (!CheckCopyToArguments(sourceIndex, destination, destinationIndex, count))
            return;

        GetIndexAndOffset(sourceIndex, out var segmentIndex, out var segmentOffset);

        while (segmentIndex < _segments.Length && count > 0) {
            var segment = _segments[segmentIndex];
            var copyLength = Math.Min(count, segment.length - segmentOffset);

            segment.CopyTo(segmentOffset, destination, destinationIndex, copyLength);

            count -= copyLength;
            destinationIndex += copyLength;
            segmentIndex++;
            segmentOffset = 0;
        }
    }

    internal override SourceText GetSubText(TextSpan span) {
        var sourceIndex = span.start;
        var count = span.length;

        GetIndexAndOffset(sourceIndex, out var segmentIndex, out var segmentOffset);

        var newSegments = ImmutableArray.CreateBuilder<SourceText>();

        while (segmentIndex < _segments.Length && count > 0) {
            var segment = _segments[segmentIndex];
            var copyLength = Math.Min(count, segment.length - segmentOffset);

            AddSegments(newSegments, segment.GetSubText(new TextSpan(segmentOffset, copyLength)));

            count -= copyLength;
            segmentIndex++;
            segmentOffset = 0;
        }

        return ToSourceText(newSegments);
    }

    /// <summary>
    /// Adds a SourceText's segments to an array of segments.
    /// </summary>
    internal static void AddSegments(ImmutableArray<SourceText>.Builder segments, SourceText text) {
        if (text is not CompositeText composite)
            segments.Add(text);
        else
            segments.AddRange(composite._segments);
    }

    /// <summary>
    /// Converts an array of segments to a <see cref="SourceText" />.
    /// </summary>
    internal static SourceText ToSourceText(ImmutableArray<SourceText>.Builder segments) {
        ReduceSegmentCountIfNecessary(segments);

        if (segments.Count == 0)
            return SourceText.From("");
        else if (segments.Count == 1)
            return segments[0];
        else
            return new CompositeText(segments.ToImmutable());
    }

    protected override void EnsureLines() {
        if (_lines != null)
            return;

        var builder = ImmutableArray.CreateBuilder<SourceText>();
        builder.AddRange(_segments);

        if (GetSegmentCountIfCombined(builder, int.MaxValue) > 1)
            throw new BelteInternalException("EnsureLines: cannot get the lines of this composite");

        CombineSegments(builder, int.MaxValue);
        var singleText = builder.Single();
        _lines = singleText.GetLines();
    }

    private static void ReduceSegmentCountIfNecessary(ImmutableArray<SourceText>.Builder segments) {
        if (segments.Count > MaximumSegmentCountBeforeReduction) {
            var segmentSize = GetMinimalSegmentSizeToUseForCombining(segments);
            CombineSegments(segments, segmentSize);
        }
    }

    private static int GetMinimalSegmentSizeToUseForCombining(ImmutableArray<SourceText>.Builder segments) {
        for (var segmentSize = InitialSegmentSizeForCombining;
             segmentSize <= MaximumSegmentSizeForCombining;
             segmentSize *= 2) {
            if (GetSegmentCountIfCombined(segments, segmentSize) <= TargetSegmentCountAfterReduction)
                return segmentSize;
        }

        return MaximumSegmentSizeForCombining;
    }

    private static int GetSegmentCountIfCombined(ImmutableArray<SourceText>.Builder segments, int segmentSize) {
        var numberOfSegmentsReduced = 0;

        for (var i = 0; i < segments.Count - 1; i++) {
            if (segments[i].length <= segmentSize) {
                var count = 1;

                for (var j = i + 1; j < segments.Count; j++) {
                    if (segments[j].length <= segmentSize)
                        count++;
                }

                if (count > 1) {
                    var removed = count - 1;
                    numberOfSegmentsReduced += removed;
                    i += removed;
                }
            }
        }

        return segments.Count - numberOfSegmentsReduced;
    }

    private static void CombineSegments(ImmutableArray<SourceText>.Builder segments, int segmentSize) {
        for (var i = 0; i < segments.Count - 1; i++) {
            if (segments[i].length <= segmentSize) {
                var combinedLength = segments[i].length;

                var count = 1;

                for (var j = i + 1; j < segments.Count; j++) {
                    if (segments[j].length <= segmentSize) {
                        count++;
                        combinedLength += segments[j].length;
                    }
                }

                if (count > 1) {
                    var writer = SourceTextWriter.Create(combinedLength);

                    while (count > 0) {
                        segments[i].Write(writer);
                        segments.RemoveAt(i);
                        count--;
                    }

                    var newText = writer.ToSourceText();
                    segments.Insert(i, newText);
                }
            }
        }
    }

    private bool CheckCopyToArguments(int sourceIndex, char[] destination, int destinationIndex, int count) {
        if (destination is null)
            throw new BelteInternalException("CheckCopyToArguments", new ArgumentNullException(nameof(destination)));

        if (sourceIndex < 0) {
            throw new BelteInternalException(
                "CheckCopyToArguments", new ArgumentOutOfRangeException(nameof(sourceIndex))
            );
        }

        if (destinationIndex < 0) {
            throw new BelteInternalException(
                "CheckCopyToArguments", new ArgumentOutOfRangeException(nameof(destinationIndex))
            );
        }

        if (count < 0 || count > length - sourceIndex || count > destination.Length - destinationIndex)
            throw new BelteInternalException("CheckCopyToArguments", new ArgumentOutOfRangeException(nameof(count)));

        return count > 0;
    }

    private void GetIndexAndOffset(int position, out int index, out int offset) {
        var tempIndex = _segmentOffsets.BinarySearch(position);
        index = tempIndex >= 0 ? tempIndex : (~tempIndex - 1);
        offset = position - _segmentOffsets[index];
    }
}
