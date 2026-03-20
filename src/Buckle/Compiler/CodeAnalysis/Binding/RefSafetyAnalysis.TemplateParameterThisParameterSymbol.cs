using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class RefSafetyAnalysis {
    private sealed class TemplateParameterThisParameterSymbol : ThisParameterSymbolBase {
        private readonly TemplateParameterSymbol _type;
        private readonly ParameterSymbol _underlyingParameter;

        internal TemplateParameterThisParameterSymbol(ParameterSymbol underlyingParameter, TemplateParameterSymbol type) {
            _underlyingParameter = underlyingParameter;
            _type = type;
        }

        internal override TypeWithAnnotations typeWithAnnotations => new TypeWithAnnotations(_type);

        public override RefKind refKind {
            get {
                if (_underlyingParameter.refKind is not RefKind.None and var underlyingRefKind)
                    return underlyingRefKind;

                return RefKind.None;
            }
        }

        internal override ImmutableArray<TextLocation> locations => _underlyingParameter.locations;

        internal override TextLocation location => _underlyingParameter.location;

        internal override Symbol containingSymbol => _underlyingParameter.containingSymbol;

        internal override ScopedKind effectiveScope => ScopedKind.None;

        internal override bool hasUnscopedRefAttribute => _underlyingParameter.hasUnscopedRefAttribute;
    }
}
