using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// Represents a change to a span of text.
/// </summary>
internal sealed partial class TextChangeRange {
    /// <summary>
    /// Creates a <see cref="TextChangeRange" /> instance.
    /// </summary>
    internal TextChangeRange(TextSpan span, int newLength) {
        this.span = span;
        this.newLength = newLength;
    }

    /// <summary>
    /// The span of text before the edit which is being changed.
    /// </summary>
    internal TextSpan span { get; }

    /// <summary>
    /// Width of the span after the edit. A 0 here would represent a delete.
    /// </summary>
    internal int newLength { get; }


    /// <summary>
    /// Collapses a set of TextChangeRanges into a single encompassing range.
    /// </summary>
    internal static TextChangeRange Collapse(IEnumerable<TextChangeRange> changes) {
        var diff = 0;
        var start = int.MaxValue;
        var end = 0;

        foreach (var change in changes) {
            diff += change.newLength - change.span.length;

            if (change.span.start < start)
                start = change.span.start;

            if (change.span.end > end)
                end = change.span.end;
        }

        if (start > end)
            return null;

        var combined = TextSpan.FromBounds(start, end);
        var collapsedLength = combined.length + diff;

        return new TextChangeRange(combined, collapsedLength);
    }

    /// <summary>
    /// Merges the new change ranges into the old change ranges.
    /// </summary>
    internal static ImmutableArray<TextChangeRange> Merge(
        ImmutableArray<TextChangeRange> oldChanges, ImmutableArray<TextChangeRange> newChanges) {
        // Literally have no clue how this works
        var builder = ImmutableArray.CreateBuilder<TextChangeRange>();

        var oldChange = oldChanges[0];
        var newChange = new UnadjustedNewChange(newChanges[0]);

        var oldIndex = 0;
        var newIndex = 0;
        var oldDelta = 0;

        bool TryGetNextOldChange() {
            oldIndex++;

            if (oldIndex < oldChanges.Length) {
                oldChange = oldChanges[oldIndex];
                return true;
            } else {
                oldChange = default;
                return false;
            }
        }

        bool TryGetNextNewChange() {
            newIndex++;

            if (newIndex < newChanges.Length) {
                newChange = new UnadjustedNewChange(newChanges[newIndex]);
                return true;
            } else {
                newChange = default;
                return false;
            }
        }

        void AddAndAdjustOldDelta(
            ImmutableArray<TextChangeRange>.Builder builder, ref int oldDelta, TextChangeRange oldChange) {
            oldDelta = oldDelta - oldChange.span.length + oldChange.newLength;
            Add(builder, oldChange);
        }

        void AdjustAndAddNewChange(
            ImmutableArray<TextChangeRange>.Builder builder, int oldDelta, UnadjustedNewChange newChange) {
            Add(
                builder,
                new TextChangeRange(
                    new TextSpan(newChange.spanStart - oldDelta, newChange.spanLength), newChange.newLength
                )
            );
        }

        void Add(ImmutableArray<TextChangeRange>.Builder builder, TextChangeRange change) {
            if (builder.Count > 0) {
                var last = builder[^1];

                if (last.span.end == change.span.start) {
                    builder[^1] = new TextChangeRange(
                        new TextSpan(last.span.start, last.span.length + change.span.length),
                        last.newLength + change.newLength
                    );
                    return;
                } else if (last.span.end > change.span.start) {
                    throw new BelteInternalException("Merge.Add", new ArgumentOutOfRangeException(nameof(change)));
                }
            }

            builder.Add(change);
        }

        while (true) {
            if (oldChange.span.length == 0 && oldChange.newLength == 0) {
                if (TryGetNextOldChange())
                    continue;
                else
                    break;
            } else if (newChange.spanLength == 0 && newChange.newLength == 0) {
                if (TryGetNextNewChange())
                    continue;
                else
                    break;
            } else if (newChange.spanEnd <= oldChange.span.start + oldDelta) {
                AdjustAndAddNewChange(builder, oldDelta, newChange);

                if (TryGetNextNewChange())
                    continue;
                else
                    break;
            } else if (newChange.spanStart >= NewEnd(oldChange) + oldDelta) {
                AddAndAdjustOldDelta(builder, ref oldDelta, oldChange);

                if (TryGetNextOldChange())
                    continue;
                else
                    break;
            } else if (newChange.spanStart < oldChange.span.start + oldDelta) {
                var newChangeLeadingDeletion = oldChange.span.start + oldDelta - newChange.spanStart;
                AdjustAndAddNewChange(
                    builder, oldDelta, new UnadjustedNewChange(newChange.spanStart, newChangeLeadingDeletion, 0)
                );

                newChange = new UnadjustedNewChange(
                    oldChange.span.start + oldDelta,
                    newChange.spanLength - newChangeLeadingDeletion,
                    newChange.newLength
                );

                continue;
            } else if (newChange.spanStart > oldChange.span.start + oldDelta) {
                var oldChangeLeadingInsertion = newChange.spanStart - (oldChange.span.start + oldDelta);
                var oldChangeLeadingDeletion = Math.Min(oldChange.span.length, oldChangeLeadingInsertion);
                AddAndAdjustOldDelta(
                    builder,
                    ref oldDelta,
                    new TextChangeRange(
                        new TextSpan(oldChange.span.start, oldChangeLeadingDeletion), oldChangeLeadingInsertion
                    )
                );

                oldChange = new TextChangeRange(
                    new TextSpan(
                        newChange.spanStart - oldDelta,
                        oldChange.span.length - oldChangeLeadingDeletion
                    ),
                    oldChange.newLength - oldChangeLeadingInsertion
                );

                continue;
            } else {
                if (newChange.spanLength <= oldChange.newLength) {
                    oldChange = new TextChangeRange(oldChange.span, oldChange.newLength - newChange.spanLength);
                    oldDelta = oldDelta + newChange.spanLength;
                    newChange = new UnadjustedNewChange(newChange.spanEnd, 0, newChange.newLength);
                    AdjustAndAddNewChange(builder, oldDelta, newChange);

                    if (TryGetNextNewChange())
                        continue;
                    else
                        break;
                } else {
                    oldDelta = oldDelta - oldChange.span.length + oldChange.newLength;
                    var newDeletion = newChange.spanLength + oldChange.span.length - oldChange.newLength;
                    newChange = new UnadjustedNewChange(
                        oldChange.span.start + oldDelta, newDeletion, newChange.newLength
                    );

                    if (TryGetNextOldChange())
                        continue;
                    else
                        break;
                }
            }
        }

        if (!((oldIndex == oldChanges.Length) ^ (newIndex == newChanges.Length)))
            throw new BelteInternalException("Merge", new InvalidOperationException());

        while (oldIndex < oldChanges.Length) {
            AddAndAdjustOldDelta(builder, ref oldDelta, oldChange);
            TryGetNextOldChange();
        }

        while (newIndex < newChanges.Length) {
            AdjustAndAddNewChange(builder, oldDelta, newChange);
            TryGetNextNewChange();
        }

        return builder.ToImmutable();
    }

    private static int NewEnd(TextChangeRange range) => range.span.start + range.newLength;
}
