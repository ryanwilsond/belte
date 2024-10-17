using System;
using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Text;

internal sealed partial class ChangedText {
    private class ChangeInfo {
        internal ImmutableArray<TextChangeRange> changeRanges { get; }

        // The weak reference here prevents unwanted chains of old text
        internal WeakReference<SourceText> weakOldText { get; }

        internal ChangeInfo? previous { get; private set; }

        internal ChangeInfo(
            ImmutableArray<TextChangeRange> changeRanges, WeakReference<SourceText> weakOldText, ChangeInfo previous) {
            this.changeRanges = changeRanges;
            this.weakOldText = weakOldText;
            this.previous = previous;
            Clean();
        }

        private void Clean() {
            var lastInfo = this;

            // Look for last info in the chain that still has a reference to old text
            for (var info = this; info is not null; info = info.previous) {

                if (info.weakOldText.TryGetTarget(out _))
                    lastInfo = info;
            }

            // Break the chain for any infos beyond that so they get garbage collected
            ChangeInfo previous;
            while (lastInfo is not null) {
                previous = lastInfo.previous;
                lastInfo.previous = null;
                lastInfo = previous;
            }
        }
    }
}
