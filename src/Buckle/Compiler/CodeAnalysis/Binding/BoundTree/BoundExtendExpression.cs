
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound type extend clause, bound from a <see cref="Syntax.TemplateParameterConstraintClauseSyntax" />.
/// </summary>
internal sealed class BoundExtendExpression : BoundExpression {
    internal BoundExtendExpression(ParameterSymbol template, BoundType extension) {
        this.template = template;
        this.extension = extension;
    }

    internal override BoundNodeKind kind => BoundNodeKind.ExtendExpression;

    internal override BoundType type => BoundType.Bool;

    internal ParameterSymbol template { get; }

    internal BoundType extension { get; }
}
