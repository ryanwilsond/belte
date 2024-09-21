using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal static class SymbolUtilities {
    internal static ImmutableArray<ParameterSymbol> NoParameters {
        get {
            return ImmutableArray<ParameterSymbol>.Empty;
        }
    }

    internal static BoundExpression NoDefault {
        get {
            return null;
        }
    }
}
