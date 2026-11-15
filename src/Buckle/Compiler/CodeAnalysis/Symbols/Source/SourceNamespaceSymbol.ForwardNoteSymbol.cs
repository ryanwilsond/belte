using Buckle.CodeAnalysis.Syntax;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal partial class SourceNamespaceSymbol {
    // ! TODO This is a huge hack to store this information in basesBeingResolved
    // TODO Need a better way to prevent cycles with forwards + global usings
    internal sealed class ForwardNoteSymbol : ErrorTypeSymbol {
        internal ForwardNoteSymbol(ForwardDirectiveSyntax forwardDirective) {
            this.forwardDirective = forwardDirective;
        }

        internal override BelteDiagnostic error => null;

        internal override bool mangleName => false;

        internal ForwardDirectiveSyntax forwardDirective { get; }

        private protected override NamedTypeSymbol WithTupleDataCore(TupleExtraData newData) {
            return this;
        }
    }
}
