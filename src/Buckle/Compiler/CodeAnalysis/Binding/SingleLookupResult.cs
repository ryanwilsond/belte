using Buckle.CodeAnalysis.Symbols;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct SingleLookupResult {
    internal readonly LookupResultKind kind;
    internal readonly Symbol symbol;
    internal readonly BelteDiagnostic error;

    internal SingleLookupResult(LookupResultKind kind, Symbol symbol, BelteDiagnostic error) {
        this.kind = kind;
        this.symbol = symbol;
        this.error = error;
    }
}
