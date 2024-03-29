using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// The results of the <see cref="OverloadResolution" />. Describes if it succeeded, and which method was picked if it
/// succeeded.
/// </summary>
internal sealed class OverloadResolutionResult<T> where T : Symbol {
    private OverloadResolutionResult(T bestOverload, ImmutableArray<BoundExpression> arguments, bool succeeded) {
        this.bestOverload = bestOverload;
        this.arguments = arguments;
        this.succeeded = succeeded;
    }

    /// <summary>
    /// Creates a failed result, indicating the <see cref="OverloadResolution" /> failed to resolve a single overload.
    /// </summary>
    internal static OverloadResolutionResult<T> Failed() {
        return new OverloadResolutionResult<T>(null, ImmutableArray<BoundExpression>.Empty, false);
    }

    /// <summary>
    /// Creates a succeeded result with a single chosen overload and the resulting fully-bound arguments.
    /// </summary>
    internal static OverloadResolutionResult<T> Succeeded(T bestOverload, ImmutableArray<BoundExpression> arguments) {
        return new OverloadResolutionResult<T>(bestOverload, arguments, true);
    }

    /// <summary>
    /// If the <see cref="OverloadResolution" /> successfully resolved a single overload.
    /// </summary>
    internal bool succeeded { get; }

    internal T bestOverload { get; }

    /// <summary>
    /// Modified arguments (accounts for default parameters, etc.)
    /// </summary>
    internal ImmutableArray<BoundExpression> arguments { get; }
}
