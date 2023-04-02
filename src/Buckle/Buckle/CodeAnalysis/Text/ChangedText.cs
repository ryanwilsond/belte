using System;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Text;

/// <summary>
/// A <see cref="SourceText" /> that has changes from an older <see cref="SourceText" />.
/// </summary>
internal sealed partial class ChangedText : SourceText {
    // Incremental compilation is very complicated, so much of this code is taken from dotnet/roslyn
    private readonly SourceText _newText;
    private readonly ChangeInfo _info;

    internal ChangedText(SourceText oldText, SourceText newText, ImmutableArray<TextChangeRange> changeRanges)
        : base(null, null) {
        _newText = newText;
        _info = new ChangeInfo(changeRanges, new WeakReference<SourceText>(oldText), (oldText as ChangedText)?._info);
    }

    internal override ImmutableArray<TextChangeRange> GetChangeRanges(SourceText oldText) {
        if (IsChangedFrom(oldText)) {
            var changes = GetChangesBetween(oldText, this);

            if (changes.Length > 1)
                return Merge(changes);
        }

        return ImmutableArray.Create(new TextChangeRange(new TextSpan(0, oldText.length), length));
    }

    private bool IsChangedFrom(SourceText oldText) {
        for (var info=_info; info!=null; info=info.previous) {
            SourceText temp;

            if (info.weakOldText.TryGetTarget(out temp) && temp == oldText)
                return true;
        }

        return false;
    }

    private static ImmutableArray<ImmutableArray<TextChangeRange>> GetChangesBetween(
        SourceText oldText, ChangedText newText) {
        var builder = ImmutableArray.CreateBuilder<ImmutableArray<TextChangeRange>>();
        var change = newText._info;
        builder.Add(change.changeRanges);

        while (change != null) {
            SourceText actualOldText;
            change.weakOldText.TryGetTarget(out actualOldText);

            if (actualOldText == oldText)
                return builder.ToImmutable();

            change = change.previous;

            if (change != null)
                builder.Insert(0, change.changeRanges);
        }

        // No old text, no changes ("failed")
        builder.Clear();
        return builder.ToImmutable();
    }

    private static ImmutableArray<TextChangeRange> Merge(ImmutableArray<ImmutableArray<TextChangeRange>> changeSets) {
        var merged = changeSets[0];

        for (int i=1; i<changeSets.Length; i++)
            merged = TextChangeRange.Merge(merged, changeSets[i]);

        return merged;
    }
}
