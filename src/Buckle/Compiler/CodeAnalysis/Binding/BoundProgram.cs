using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Symbols;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BoundProgram {
    private ImmutableDictionary<MethodSymbol, BoundBlockStatement> _lazyOriginalDefinitions;

    internal BoundProgram(
        Compilation compilation,
        ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies,
        ImmutableArray<NamedTypeSymbol> types,
        MultiDictionary<NamedTypeSymbol, NamedTypeSymbol> nestedTypes,
        MethodSymbol entryPoint,
        MethodSymbol updatePoint,
        BoundProgram previous = null) {
        this.compilation = compilation;
        this.methodBodies = methodBodies;
        this.types = types;
        this.nestedTypes = nestedTypes;
        this.entryPoint = entryPoint;
        this.updatePoint = updatePoint;
        this.previous = previous;
    }

    internal Compilation compilation { get; }

    internal ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies { get; }

    internal ImmutableArray<NamedTypeSymbol> types { get; }

    internal MultiDictionary<NamedTypeSymbol, NamedTypeSymbol> nestedTypes { get; }

    internal MethodSymbol entryPoint { get; }

    internal MethodSymbol updatePoint { get; }

    internal BoundProgram previous { get; }

    private ImmutableDictionary<MethodSymbol, BoundBlockStatement> _originalDefinitions {
        get {
            if (_lazyOriginalDefinitions is null)
                Interlocked.CompareExchange(ref _lazyOriginalDefinitions, CreateOriginalDefinitions(), null);

            return _lazyOriginalDefinitions;
        }
    }

    internal bool TryGetMethodBodyIncludingParents(
        MethodSymbol method,
        out BoundBlockStatement body,
        bool useOriginalDefinitions = false) {
        if (useOriginalDefinitions)
            return MethodBodyLookupUsingOriginals(method, out body);

        var current = this;

        while (current is not null) {
            if (current.methodBodies.TryGetValue(method.originalDefinition, out var value)) {
                body = value;
                return true;
            }

            current = current.previous;
        }

        body = null;
        return false;
    }

    private bool MethodBodyLookupUsingOriginals(MethodSymbol method, out BoundBlockStatement body) {
        var current = this;

        while (current is not null) {
            if (current._originalDefinitions.TryGetValue(GetOriginalDefinition(method), out var value)) {
                body = value;
                return true;
            }

            current = current.previous;
        }

        body = null;
        return false;
    }

    private ImmutableDictionary<MethodSymbol, BoundBlockStatement> CreateOriginalDefinitions() {
        return methodBodies.ToDictionary(pair => GetOriginalDefinition(pair.Key), pair => pair.Value)
            .ToImmutableDictionary();
    }

    private static MethodSymbol GetOriginalDefinition(MethodSymbol method) {
        return method is SynthesizedMethodSymbolBase b ? b.baseMethod.originalDefinition : method.originalDefinition;
    }
}
