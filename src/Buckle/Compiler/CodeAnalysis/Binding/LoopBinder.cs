using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal abstract class LoopBinder : LocalScopeBinder {
    private protected LoopBinder(Binder enclosing)
        : base(enclosing) {
        breakLabel = new SynthesizedLabelSymbol("break");
        continueLabel = new SynthesizedLabelSymbol("continue");
    }

    internal override SynthesizedLabelSymbol breakLabel { get; }

    internal override SynthesizedLabelSymbol continueLabel { get; }
}
