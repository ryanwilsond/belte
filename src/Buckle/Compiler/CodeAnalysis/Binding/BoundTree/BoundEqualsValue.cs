using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal abstract class BoundEqualsValue : BoundNode {
    private protected BoundEqualsValue(ImmutableArray<LocalSymbol> locals, BoundExpression value) {
        this.locals = locals;
        this.value = value;
    }

    internal ImmutableArray<LocalSymbol> locals { get; }

    internal BoundExpression value { get; }
}
