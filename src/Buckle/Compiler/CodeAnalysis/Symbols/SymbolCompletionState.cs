using System.Threading;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal struct SymbolCompletionState {
    private volatile int _completeParts;

    internal int incompleteParts => ~_completeParts & (int)CompletionParts.All;

    internal CompletionParts nextIncompletePart {
        get {
            var incomplete = incompleteParts;
            var next = incomplete & ~(incomplete - 1);
            return (CompletionParts)next;
        }
    }

    internal void DefaultForceComplete() {
        NotePartComplete(CompletionParts.All);
    }

    internal readonly bool HasComplete(CompletionParts part) {
        return (_completeParts & (int)part) == (int)part;
    }

    internal bool NotePartComplete(CompletionParts part) {
        return ThreadSafeFlagOperations.Set(ref _completeParts, (int)part);
    }

    internal static bool HasAtMostOneBitSet(int bits) {
        return (bits & (bits - 1)) == 0;
    }

    internal readonly void SpinWaitComplete(CompletionParts part) {
        if (HasComplete(part))
            return;

        var spinWait = new SpinWait();

        while (!HasComplete(part))
            spinWait.SpinOnce();
    }
}
