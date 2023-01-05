using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal static partial class BoundFactory {
    internal static BoundGlobalScope GlobalScope(BoundGlobalScope previous, BelteDiagnosticQueue diagnostics) {
        return new BoundGlobalScope(ImmutableArray<(FunctionSymbol function, BoundBlockStatement body)>.Empty,
            ImmutableArray<(StructSymbol function, ImmutableList<FieldSymbol> members)>.Empty, previous,
            diagnostics, null, null, ImmutableArray<FunctionSymbol>.Empty,
            ImmutableArray<VariableSymbol>.Empty, ImmutableArray<TypeSymbol>.Empty,
            ImmutableArray<BoundStatement>.Empty
        );
    }

    internal static BoundProgram Program(BoundProgram previous, BelteDiagnosticQueue diagnostics) {
        return new BoundProgram(previous, diagnostics,
            null, null, ImmutableDictionary<FunctionSymbol, BoundBlockStatement>.Empty,
            ImmutableDictionary<StructSymbol, ImmutableList<FieldSymbol>>.Empty
        );
    }
}
