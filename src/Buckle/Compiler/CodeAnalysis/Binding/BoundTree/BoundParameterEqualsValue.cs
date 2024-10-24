using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class BoundParameterEqualsValue : BoundEqualsValue {
    internal BoundParameterEqualsValue(
        Symbol parameter,
        ImmutableArray<LocalSymbol> locals,
        BoundExpression value)
        : base(locals, value) {
        this.parameter = parameter;
    }

    internal override BoundNodeKind kind => BoundNodeKind.ParameterEqualsValue;

    internal Symbol parameter { get; }
}
