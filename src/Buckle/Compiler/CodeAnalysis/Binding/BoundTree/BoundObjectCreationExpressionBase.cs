using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal abstract partial class BoundObjectCreationExpressionBase {
    internal abstract MethodSymbol constructor { get; }

    internal abstract ImmutableArray<BoundExpression> arguments { get; }

    internal abstract ImmutableArray<RefKind> argumentRefKinds { get; }

    internal abstract ImmutableArray<int> argsToParams { get; }

    internal abstract BitVector defaultArguments { get; }

    internal abstract bool wasTargetTyped { get; }
}
