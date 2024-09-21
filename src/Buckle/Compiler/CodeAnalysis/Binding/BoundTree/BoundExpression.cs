using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound expression, bound from a <see cref="Syntax.ExpressionSyntax" />.
/// All Expressions have a possible constant value, used for <see cref="ConstantFolding" />.
/// If folding is not possible, <see cref="constantValue" /> is null.
/// </summary>
internal abstract class BoundExpression : BoundNode {
    internal abstract TypeSymbol type { get; }

    internal virtual ConstantValue constantValue => null;
}
