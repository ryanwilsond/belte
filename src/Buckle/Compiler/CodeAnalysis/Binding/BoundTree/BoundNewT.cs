using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BoundNewT {
    internal override MethodSymbol constructor => null;

    internal override ImmutableArray<BoundExpression> arguments => [];

    internal override ImmutableArray<RefKind> argumentRefKinds => default;

    internal override ImmutableArray<int> argsToParams => default;

    internal override BitVector defaultArguments => default;
}
