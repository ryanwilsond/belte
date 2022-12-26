using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
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

    /// <summary>
    /// Searches for a function inside a dictionary of functions based on a name.
    /// </summary>
    /// <param name="functions">What to search.</param>
    /// <param name="name">What to search for.</param>
    /// <typeparam name="T">The value type of the dictionary.</typeparam>
    /// <returns>The found pair in the dictionary, throws if the function is not found.</returns>
    internal static (FunctionSymbol, T) LookupMethod<T>(
        IDictionary<FunctionSymbol, T> functions, string name) {
        foreach (var pair in functions)
            if (pair.Key.name == name)
                return (pair.Key, pair.Value);

        throw new BelteInternalException($"LookupMethod: could not find method '{name}'");
    }

    /// <summary>
    /// Searches for a function from a program, including the programs parents.
    /// Because of the nature of the search, unlike <see cref="LookupMethod" /> this will always return a
    /// <see cref="BoundBlockStatement" />.
    /// </summary>
    /// <param name="program">What to search.</param>
    /// <param name="function">What to search for.</param>
    /// <returns>
    /// The value of the found pair in the function bodies dictionary, throws if the function is not found.
    /// </returns>
    internal static BoundBlockStatement LookupMethodFromParents(BoundProgram program, FunctionSymbol function) {
        var current = program;

        while (current != null) {
            try {
                return LookupMethod(current.functionBodies, function);
            } catch (BelteInternalException) {
                current = current.previous;
            }
        }

        throw new BelteInternalException($"LookupMethodFromParents: could not find method '{function.name}'");
    }

    /// <summary>
    /// Searches for a function from a program, including the programs parents, based on a name.
    /// </summary>
    /// <param name="program">What to search.</param>
    /// <param name="name">What to search for.</param>
    /// <returns>The found pair in the function bodies dictionary, throws if the function is not found.</returns>
    internal static (FunctionSymbol, BoundBlockStatement)
        LookupMethodFromParentsFromName(BoundProgram program, string name) {
        var current = program;

        while (current != null) {
            try {
                return LookupMethod(current.functionBodies, name);
            } catch (BelteInternalException) {
                current = current.previous;
            }
        }

        throw new BelteInternalException($"LookupMethodFromParents: could not find method '{name}'");
    }
}
