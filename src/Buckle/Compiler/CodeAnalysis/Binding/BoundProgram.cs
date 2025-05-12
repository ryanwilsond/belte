using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed class BoundProgram {
    internal BoundProgram(
        ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies,
        ImmutableArray<NamedTypeSymbol> types,
        MethodSymbol entryPoint,
        MethodSymbol updatePoint,
        BoundProgram previous = null) {
        this.methodBodies = methodBodies;
        this.types = types;
        this.entryPoint = entryPoint;
        this.updatePoint = updatePoint;
        this.previous = previous;
    }

    internal ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies { get; }

    internal ImmutableArray<NamedTypeSymbol> types { get; }

    internal MethodSymbol entryPoint { get; }

    internal MethodSymbol updatePoint { get; }

    internal BoundProgram previous { get; }

    internal bool TryGetMethodBodyIncludingParents(MethodSymbol method, out BoundBlockStatement body) {
        var current = this;

        while (current is not null) {
            if (current.methodBodies.TryGetValue(method, out var value)) {
                body = value;
                return true;
            }

            current = current.previous;
        }

        body = null;
        return false;
    }
}
