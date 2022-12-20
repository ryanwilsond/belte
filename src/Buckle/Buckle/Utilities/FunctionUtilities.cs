using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.Utilities;

static class FunctionUtilities {
    internal static T LookupMethod<T>(
        IDictionary<FunctionSymbol, T> functions, FunctionSymbol function) {
        foreach (var pair in functions)
            if (pair.Key.MethodMatches(function))
                return pair.Value;

        throw new BelteInternalException($"LookupMethod: could not find method '{function.name}'");
    }
}
