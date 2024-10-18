using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A <see cref="SourceText" /> that has changes from an older <see cref="SourceText" />.
/// </summary>
internal sealed partial class ChangedText : SourceText {
    // Incremental compilation is very complicated, so much of this code is taken from dotnet/roslyn
    private readonly SourceText _newText;
    private readonly ChangeInfo _info;

    /// <summary>
    /// Creates a new <see cref="ChangedText" />.
    /// </summary>
    /// <param name="oldText">The old <see cref="SourceText" /> that the changes apply to.</param>
    /// <param name="newText">The new <see cref="SourceText" /> that reflects the given changes to the old text.</param>
    /// <param name="changeRanges">The changes from the old text to the new text.</param>
    internal ChangedText(SourceText oldText, SourceText newText, ImmutableArray<TextChangeRange> changeRanges) {
        _newText = newText;
        _info = new ChangeInfo(changeRanges, new WeakReference<SourceText>(oldText), (oldText as ChangedText)?._info);
    }

    public override int lineCount => _newText.lineCount;

    public override char this[int index] => _newText[index];

    public override int length => _newText.length;

    public override string ToString(TextSpan span) {
        return _newText.ToString(span);
    }

    public override string ToString() {
        return _newText.ToString();
    }

    public override void CopyTo(int sourceIndex, char[] destination, int destinationIndex, int count) {
        _newText.CopyTo(sourceIndex, destination, destinationIndex, count);
    }

    public override SourceText WithChanges(IEnumerable<TextChange> changes) {
        if (_newText.WithChanges(changes) is ChangedText changed)
            return new ChangedText(this, changed._newText, changed._info.changeRanges);
        else
            return this;
    }

    private protected override void EnsureLines() {
        _lines ??= _newText.GetLines();
    }

    internal override ImmutableArray<TextChangeRange> GetChangeRanges(SourceText oldText) {
        if (this == oldText)
            return ImmutableArray<TextChangeRange>.Empty;

        if (_info.weakOldText.TryGetTarget(out var actualOldText) && actualOldText == oldText)
            // Checks if the given oldText is what we reference, if so our changes must be accurate
            return _info.changeRanges;

        if (IsChangedFrom(oldText)) {
            var changes = GetChangesBetween(oldText, this);

            if (changes.Length > 0)
                return Merge(changes);
        }

        if (actualOldText is not null && actualOldText.GetChangeRanges(oldText).Length == 0)
            // Checks if the given oldText is equal to what we reference, if so our changes must be accurate
            return _info.changeRanges;

        return ImmutableArray.Create(new TextChangeRange(new TextSpan(0, oldText.length), length));
    }

    internal override SourceText GetSubText(TextSpan span) {
        return _newText.GetSubText(span);
    }

    private bool IsChangedFrom(SourceText oldText) {
        for (var info = _info; info is not null; info = info.previous) {

            if (info.weakOldText.TryGetTarget(out var temp) && temp == oldText)
                return true;
        }

        return false;
    }

    private static ImmutableArray<ImmutableArray<TextChangeRange>> GetChangesBetween(
        SourceText oldText, ChangedText newText) {
        var builder = ImmutableArray.CreateBuilder<ImmutableArray<TextChangeRange>>();
        var change = newText._info;
        builder.Add(change.changeRanges);

        while (change is not null) {
            change.weakOldText.TryGetTarget(out var actualOldText);

            if (actualOldText == oldText)
                return builder.ToImmutable();

            change = change.previous;

            if (change is not null)
                builder.Insert(0, change.changeRanges);
        }

        // No old text, no changes ("failed")
        builder.Clear();
        return builder.ToImmutable();
    }

    private static ImmutableArray<TextChangeRange> Merge(ImmutableArray<ImmutableArray<TextChangeRange>> changeSets) {
        var merged = changeSets[0];

        for (var i = 1; i < changeSets.Length; i++)
            merged = TextChangeRange.Merge(merged, changeSets[i]);

        return merged;
    }
}
