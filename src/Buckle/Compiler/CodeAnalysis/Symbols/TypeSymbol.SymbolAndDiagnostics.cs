using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

#pragma warning disable CS0660

internal abstract partial class TypeSymbol {
    internal class SymbolAndDiagnostics {
        internal static readonly SymbolAndDiagnostics Empty
            = new SymbolAndDiagnostics(null, BelteDiagnosticQueue.Discarded);

        internal readonly Symbol symbol;
        // TODO Perf: this could be a light-weight read-only struct diagnostic collection instead of a proper queue
        internal readonly BelteDiagnosticQueue diagnostics;

        internal SymbolAndDiagnostics(Symbol symbol, BelteDiagnosticQueue diagnostics) {
            this.symbol = symbol;
            this.diagnostics = diagnostics;
        }
    }
}
