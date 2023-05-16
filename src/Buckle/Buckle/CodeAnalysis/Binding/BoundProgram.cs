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
        BoundProgram previous, BelteDiagnosticQueue diagnostics, MethodSymbol entryPoint,
        ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies,
        ImmutableDictionary<StructSymbol, ImmutableList<Symbol>> structMembers,
        ImmutableDictionary<ClassSymbol, ImmutableList<Symbol>> classMembers) {
        this.previous = previous;
        this.diagnostics = diagnostics;
        this.entryPoint = entryPoint;
        this.methodBodies = methodBodies;
        this.structMembers = structMembers;
        this.classMembers = classMembers;
    }

    /// <summary>
    /// Previous <see cref="BoundProgram" /> (if applicable).
    /// </summary>
    internal BoundProgram previous { get; }

    internal BelteDiagnosticQueue diagnostics { get; }

    internal MethodSymbol entryPoint { get; }

    internal ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies { get; }

    internal ImmutableDictionary<StructSymbol, ImmutableList<Symbol>> structMembers { get; }

    internal ImmutableDictionary<ClassSymbol, ImmutableList<Symbol>> classMembers { get; }
}
