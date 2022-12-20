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
        BoundProgram previous, BelteDiagnosticQueue diagnostics, FunctionSymbol mainFunction,
        FunctionSymbol scriptFunction, ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies,
        ImmutableDictionary<StructSymbol, ImmutableList<FieldSymbol>> structMembers) {
        this.previous = previous;
        this.diagnostics = diagnostics;
        this.mainFunction = mainFunction;
        this.scriptFunction = scriptFunction;
        this.functionBodies = functionBodies;
        this.structMembers = structMembers;
    }

    /// <summary>
    /// Previous <see cref="BoundProgram" /> (if applicable).
    /// </summary>
    internal BoundProgram previous { get; }

    internal BelteDiagnosticQueue diagnostics { get; }

    internal FunctionSymbol mainFunction { get; }

    internal FunctionSymbol scriptFunction { get; }

    internal ImmutableDictionary<FunctionSymbol, BoundBlockStatement> functionBodies { get; }

    internal ImmutableDictionary<StructSymbol, ImmutableList<FieldSymbol>> structMembers { get; }
}
