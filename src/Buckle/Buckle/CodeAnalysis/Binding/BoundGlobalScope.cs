using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A bound global scope, stores top level symbols.
/// </summary>
internal sealed class BoundGlobalScope {
    /// <param name="previous">Previous <see cref="BoundGlobalScope" /> (if applicable).</param>
    internal BoundGlobalScope(
        ImmutableArray<(MethodSymbol method, BoundBlockStatement body)> methodBodies,
        ImmutableArray<(StructSymbol @struct, ImmutableList<FieldSymbol> members)> structMembers,
        BoundGlobalScope previous, BelteDiagnosticQueue diagnostics, MethodSymbol mainMethod,
        MethodSymbol scriptMethod, ImmutableArray<MethodSymbol> methods,
        ImmutableArray<VariableSymbol> variables, ImmutableArray<TypeSymbol> types,
        ImmutableArray<BoundStatement> statements) {
        this.methodBodies = methodBodies;
        this.structMembers = structMembers;
        this.previous = previous;
        this.diagnostics = new BelteDiagnosticQueue();
        this.diagnostics.Move(diagnostics);
        this.mainMethod = mainMethod;
        this.scriptMethod = scriptMethod;
        this.methods = methods;
        this.variables = variables;
        this.types = types;
        this.statements = statements;
    }

    internal ImmutableArray<(MethodSymbol method, BoundBlockStatement body)> methodBodies { get; }

    internal ImmutableArray<(StructSymbol @struct, ImmutableList<FieldSymbol> members)> structMembers { get; }

    /// <summary>
    /// Previous <see cref="BoundGlobalScope" /> (if applicable).
    /// </summary>
    internal BoundGlobalScope previous { get; }

    internal BelteDiagnosticQueue diagnostics { get; }

    internal MethodSymbol mainMethod { get; }

    internal MethodSymbol scriptMethod { get; }

    internal ImmutableArray<MethodSymbol> methods { get; }

    internal ImmutableArray<VariableSymbol> variables { get; }

    /// <summary>
    /// All types, not including built in types (which are always in scope).
    /// </summary>
    internal ImmutableArray<TypeSymbol> types { get; }

    internal ImmutableArray<BoundStatement> statements { get; }
}
