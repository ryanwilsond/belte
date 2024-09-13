using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound error expression, signally that binding a expression failed.
/// Using at temporary error expression allows catching of future unrelated issues before quitting.
/// Also prevents cascading errors, as if a <see cref="BoundErrorExpression" /> is seen, the <see cref="Binder" />
/// ignores it as it knows the error was already reported.
/// </summary>
internal sealed class BoundErrorExpression : BoundExpression {
    internal BoundErrorExpression() { }

    internal override BoundNodeKind kind => BoundNodeKind.ErrorExpression;

    internal override BoundType type => new BoundType(TypeSymbol.Error);
}
