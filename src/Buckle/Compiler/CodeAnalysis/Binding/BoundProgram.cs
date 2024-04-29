using System.Collections.Generic;
using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// Bound program.
/// </summary>
internal sealed class BoundProgram {
    /// <param name="previous">Previous <see cref="BoundProgram" /> (if applicable).</param>
    internal BoundProgram(
        BoundProgram previous,
        BelteDiagnosticQueue diagnostics,
        Dictionary<string, MethodSymbol> wellKnownMethods,
        ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies,
        ImmutableArray<NamedTypeSymbol> types) {
        this.previous = previous;
        this.diagnostics = diagnostics;
        this.wellKnownMethods = wellKnownMethods;
        this.methodBodies = methodBodies;
        this.types = types;
    }

    /// <summary>
    /// Previous <see cref="BoundProgram" /> (if applicable).
    /// </summary>
    internal BoundProgram previous { get; }

    internal BelteDiagnosticQueue diagnostics { get; }

    internal MethodSymbol entryPoint => wellKnownMethods[WellKnownMethodNames.EntryPoint];

    internal Dictionary<string, MethodSymbol> wellKnownMethods { get; }

    internal ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies { get; }

    internal ImmutableArray<NamedTypeSymbol> types { get; }
}
