using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Buckle.CodeAnalysis.Lowering;
using Buckle.CodeAnalysis.Symbols;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class BoundProgram {
    private ImmutableDictionary<MethodSymbol, BoundBlockStatement> _lazyOriginalDefinitions;

    internal BoundProgram(
        Compilation compilation,
        ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies,
        ImmutableDictionary<MethodSymbol, EvaluatorSlotManager> methodLayouts,
        ImmutableArray<NamedTypeSymbol> types,
        ImmutableDictionary<NamedTypeSymbol, EvaluatorSlotManager> typeLayouts,
        MultiDictionary<NamedTypeSymbol, NamedTypeSymbol> nestedTypes,
        ImmutableDictionary<FieldSymbol, NamedTypeSymbol> fixedImplementationTypes,
        MethodSymbol entryPoint,
        MethodSymbol updatePoint,
        BoundProgram previous = null) {
        this.compilation = compilation;
        this.methodBodies = methodBodies;
        this.methodLayouts = methodLayouts;
        this.types = types;
        this.typeLayouts = typeLayouts;
        this.nestedTypes = nestedTypes;
        this.entryPoint = entryPoint;
        this.updatePoint = updatePoint;
        this.previous = previous;
        this.fixedImplementationTypes = fixedImplementationTypes;
    }

    internal Compilation compilation { get; }

    internal ImmutableDictionary<MethodSymbol, BoundBlockStatement> methodBodies { get; }

    internal ImmutableDictionary<MethodSymbol, EvaluatorSlotManager> methodLayouts { get; }

    internal ImmutableArray<NamedTypeSymbol> types { get; }

    internal ImmutableDictionary<NamedTypeSymbol, EvaluatorSlotManager> typeLayouts { get; }

    internal MultiDictionary<NamedTypeSymbol, NamedTypeSymbol> nestedTypes { get; }

    internal ImmutableDictionary<FieldSymbol, NamedTypeSymbol> fixedImplementationTypes { get; }

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

    internal bool TryGetMethodLayoutIncludingParents(MethodSymbol method, out EvaluatorSlotManager layout) {
        var current = this;

        while (current is not null) {
            if (current.methodLayouts.TryGetValue(method.originalDefinition, out var value)) {
                layout = value;
                return true;
            }

            current = current.previous;
        }

        layout = null;
        return false;
    }

    internal bool TryGetTypeLayoutIncludingParents(NamedTypeSymbol type, out EvaluatorSlotManager layout) {
        var current = this;

        while (current is not null) {
            if (current.typeLayouts.TryGetValue(type.originalDefinition, out var value)) {
                layout = value;
                return true;
            }

            current = current.previous;
        }

        layout = null;
        return false;
    }

    internal ImmutableArray<TypeSymbol> GetAllTypes() {
        var builder = ArrayBuilder<TypeSymbol>.GetInstance();

        for (var current = this; current is not null; current = current.previous)
            builder.AddRange(current.types);

        return builder.ToImmutableAndFree();
    }

    internal ImmutableArray<(MethodSymbol, BoundBlockStatement)> GetAllMethodBodies() {
        var builder = ArrayBuilder<(MethodSymbol, BoundBlockStatement)>.GetInstance();

        for (var current = this; current is not null; current = current.previous)
            builder.AddRange(current.methodBodies.Select(p => (p.Key, p.Value)));

        return builder.ToImmutableAndFree();
    }

    internal ImmutableArray<NamedTypeSymbol> GetTypesToEmit(params SpecialType[] typesToInclude) {
        var types = GetAllTypes();
        var length = types.Length;
        var builder = ArrayBuilder<NamedTypeSymbol>.GetInstance(length);

        for (var i = 0; i < length; i++) {
            var t = types[i];

            if (t.kind == SymbolKind.NamedType &&
                t.containingSymbol.kind == SymbolKind.Namespace &&
                (t.specialType is SpecialType.None or SpecialType.List or
                                  SpecialType.Dictionary or SpecialType.Enumerator ||
                 System.MemoryExtensions.Contains(typesToInclude, t.specialType)) &&
                t.originalDefinition is not PENamedTypeSymbol) {
                builder.Add((NamedTypeSymbol)t);
            }
        }

        return builder.ToImmutableAndFree();
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
