using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// The results of the <see cref="OverloadResolution" />. Describes if it succeeded, and which method was picked if it
/// succeeded.
/// </summary>
internal sealed class OverloadResolutionResult<T> where T : ISymbol {
    private OverloadResolutionResult(T[] bestOverloads, ImmutableArray<BoundExpression> arguments, bool succeeded) {
        this.bestOverloads = bestOverloads;
        this.arguments = arguments;
        this.succeeded = succeeded;
        ambiguous = false;
    }

    /// <summary>
    /// Creates a failed result, indicating the <see cref="OverloadResolution" /> failed to resolve a single overload.
    /// </summary>
    internal static OverloadResolutionResult<T> Failed() {
        return new OverloadResolutionResult<T>(default, ImmutableArray<BoundExpression>.Empty, false);
    }

    /// <summary>
    /// Creates a failed result due to an ambiguity error.
    /// </summary>
    internal static OverloadResolutionResult<T> Ambiguous() {
        var result = Failed();
        result.ambiguous = true;
        return result;
    }

    /// <summary>
    /// Creates a succeeded result with a single chosen overload and the resulting fully-bound arguments.
    /// </summary>
    internal static OverloadResolutionResult<T> Succeeded(T bestOverload, ImmutableArray<BoundExpression> arguments) {
        return new OverloadResolutionResult<T>([bestOverload], arguments, true);
    }

    /// <summary>
    /// Creates a succeeded result with multiple chosen overloads and the resulting fully-bound arguments.
    /// </summary>
    internal static OverloadResolutionResult<T> Succeeded(T[] bestOverloads, ImmutableArray<BoundExpression> arguments) {
        return new OverloadResolutionResult<T>(bestOverloads, arguments, true);
    }

    /// <summary>
    /// If the <see cref="OverloadResolution" /> successfully resolved a single overload.
    /// </summary>
    internal bool succeeded { get; }

    /// <summary>
    /// If the <see cref="OverloadResolution"/> resulted in an ambiguous failure.
    /// </summary>
    /// <value></value>
    internal bool ambiguous { get; private set; }

    internal T[] bestOverloads { get; }

    internal T bestOverload => bestOverloads[0];

    /// <summary>
    /// Modified arguments (accounts for default parameters, etc.)
    /// </summary>
    internal ImmutableArray<BoundExpression> arguments { get; }
}
