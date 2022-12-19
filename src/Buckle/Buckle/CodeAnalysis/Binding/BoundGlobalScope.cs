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
        ImmutableArray<(FunctionSymbol function, BoundBlockStatement body)> functionBodies,
        ImmutableArray<(StructSymbol @struct, ImmutableList<FieldSymbol> body)> structBodies, BoundGlobalScope previous,
        BelteDiagnosticQueue diagnostics, FunctionSymbol mainFunction, FunctionSymbol scriptFunction,
        ImmutableArray<FunctionSymbol> functions, ImmutableArray<VariableSymbol> variables,
        ImmutableArray<TypeSymbol> types, ImmutableArray<BoundStatement> statements) {
        this.functionBodies = functionBodies;
        this.structBodies = structBodies;
        this.previous = previous;
        this.diagnostics = new BelteDiagnosticQueue();
        this.diagnostics.Move(diagnostics);
        this.mainFunction = mainFunction;
        this.scriptFunction = scriptFunction;
        this.functions = functions;
        this.variables = variables;
        this.types = types;
        this.statements = statements;
    }

    internal ImmutableArray<(FunctionSymbol function, BoundBlockStatement body)> functionBodies { get; }

    internal ImmutableArray<(StructSymbol @struct, ImmutableList<FieldSymbol> body)> structBodies { get; }

    /// <summary>
    /// Previous <see cref="BoundGlobalScope" /> (if applicable).
    /// </summary>
    internal BoundGlobalScope previous { get; }

    internal BelteDiagnosticQueue diagnostics { get; }

    internal FunctionSymbol mainFunction { get; }

    internal FunctionSymbol scriptFunction { get; }

    internal ImmutableArray<FunctionSymbol> functions { get; }

    internal ImmutableArray<VariableSymbol> variables { get; }

    /// <summary>
    /// All types, not including built in types (which are always in scope).
    /// </summary>
    internal ImmutableArray<TypeSymbol> types { get; }

    internal ImmutableArray<BoundStatement> statements { get; }
}
