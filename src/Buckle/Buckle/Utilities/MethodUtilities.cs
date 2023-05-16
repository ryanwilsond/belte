using System.Collections.Generic;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.Utilities;

/// <summary>
/// Utilities related to the <see cref="MethodSymbol" /> class.
/// </summary>
internal static class MethodUtilities {
    /// <summary>
    /// Searches for a method inside a dictionary of methods.
    /// </summary>
    /// <param name="methods">What to search.</param>
    /// <param name="method">What to search for.</param>
    /// <typeparam name="T">The value type of the dictionary.</typeparam>
    /// <returns>The value of the found pair in the dictionary, throws if the method is not found.</returns>
    internal static T LookupMethod<T>(IDictionary<MethodSymbol, T> methods, MethodSymbol method) {
        foreach (var pair in methods) {
            if (pair.Key.MethodMatches(method))
                return pair.Value;
        }

        throw new BelteInternalException($"LookupMethod: could not find method '{method.name}'");
    }

    /// <summary>
    /// Searches for a method from a program, including the programs parents.
    /// Because of the nature of the search, unlike <see cref="LookupMethod" /> this will always return a
    /// <see cref="BoundBlockStatement" />.
    /// </summary>
    /// <param name="program">What to search.</param>
    /// <param name="method">What to search for.</param>
    /// <returns>
    /// The value of the found pair in the method bodies dictionary, throws if the method is not found.
    /// </returns>
    internal static BoundBlockStatement LookupMethodFromParents(BoundProgram program, MethodSymbol method) {
        var current = program;

        while (current != null) {
            try {
                return LookupMethod(current.methodBodies, method);
            } catch (BelteInternalException) {
                current = current.previous;
            }
        }

        throw new BelteInternalException($"LookupMethodFromParents: could not find method '{method.name}'");
    }

    /// <summary>
    /// Searches for a method from a program, including the programs parents, based on a name.
    /// </summary>
    /// <param name="program">What to search.</param>
    /// <param name="name">What to search for.</param>
    /// <returns>The found pair in the method bodies dictionary, throws if the method is not found.</returns>
    internal static (MethodSymbol, BoundBlockStatement)
        LookupMethodFromParentsFromName(BoundProgram program, string name) {
        var current = program;

        while (current != null) {
            try {
                return LookupMethod(current.methodBodies, name);
            } catch (BelteInternalException) {
                current = current.previous;
            }
        }

        throw new BelteInternalException($"LookupMethodFromParents: could not find method '{name}'");
    }

    private static (MethodSymbol, T) LookupMethod<T>(IDictionary<MethodSymbol, T> methods, string name) {
        foreach (var pair in methods) {
            if (pair.Key.name == name)
                return (pair.Key, pair.Value);
        }

        throw new BelteInternalException($"LookupMethod: could not find method '{name}'");
    }
}
