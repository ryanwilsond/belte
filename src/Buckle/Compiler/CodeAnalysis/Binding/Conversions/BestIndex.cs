
namespace Buckle.CodeAnalysis.Binding;

internal readonly struct BestIndex {
    internal readonly BestIndexKind kind;
    internal readonly int best;
    internal readonly int ambiguous1;
    internal readonly int ambiguous2;

    internal static BestIndex None() { return new BestIndex(BestIndexKind.None, 0, 0, 0); }
    internal static BestIndex HasBest(int best) { return new BestIndex(BestIndexKind.Best, best, 0, 0); }
    internal static BestIndex IsAmbiguous(int ambig1, int ambig2) { return new BestIndex(BestIndexKind.Ambiguous, 0, ambig1, ambig2); }

    private BestIndex(BestIndexKind kind, int best, int ambig1, int ambig2) {
        this.kind = kind;
        this.best = best;
        ambiguous1 = ambig1;
        ambiguous2 = ambig2;
    }
}
