using System.Collections.Generic;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.Utilities;

/// <summary>
/// Utilities related to the <see cref="FunctionSymbol" /> class.
/// </summary>
public static class FunctionUtilities {
    /// <summary>
    /// Searches for a function inside a dictionary of functions.
    /// </summary>
    /// <param name="functions">What to search.</param>
    /// <param name="function">What to search for.</param>
    /// <typeparam name="T">The value type of the dictionary.</typeparam>
    /// <returns>The value of the found pair in the dictionary, throws if the function is not found.</returns>
    internal static T LookupMethod<T>(
        IDictionary<FunctionSymbol, T> functions, FunctionSymbol function) {
        foreach (var pair in functions)
            if (pair.Key.MethodMatches(function))
                return pair.Value;

        throw new BelteInternalException($"LookupMethod: could not find method '{function.name}'");
    }
}
