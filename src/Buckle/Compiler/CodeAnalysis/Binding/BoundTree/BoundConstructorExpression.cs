using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound constructor expression, produced by the <see cref="Binder" />. No
/// <see cref="Syntax.InternalSyntax.Parser" /> equivalent.<br/>
/// E.g.
/// <code>
/// MyStruct()
/// </code>
/// </summary>
internal sealed class BoundConstructorExpression : BoundExpression {
    internal BoundConstructorExpression(TypeSymbol symbol, ImmutableArray<BoundConstant> templateArguments) {
        this.symbol = symbol;
        this.templateArguments = templateArguments;
    }

    internal override BoundNodeKind kind => BoundNodeKind.ConstructorExpression;

    internal override BoundType type => new BoundType(symbol);

    internal TypeSymbol symbol { get; }

    internal ImmutableArray<BoundConstant> templateArguments { get; }
}
