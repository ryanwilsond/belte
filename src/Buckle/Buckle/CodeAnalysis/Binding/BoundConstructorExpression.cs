using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound constructor expression, produced by the <see cref="Binder" />. No <see cref="Parser" />
/// equivalent.<br/>
/// E.g.
/// <code>
/// MyStruct()
/// </code>
/// </summary>
internal sealed class BoundConstructorExpression : BoundExpression {
    internal BoundConstructorExpression(TypeSymbol symbol) {
        this.symbol = symbol;
    }

    internal TypeSymbol symbol { get; }

    internal override BoundTypeClause typeClause => new BoundTypeClause(symbol);

    internal override BoundNodeType type => BoundNodeType.ConstructorExpression;
}
