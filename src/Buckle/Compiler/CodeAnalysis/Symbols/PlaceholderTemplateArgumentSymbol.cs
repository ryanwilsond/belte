using System.Collections.Immutable;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class PlaceholderTemplateArgumentSymbol : ErrorTypeSymbol {
    private static readonly TypeOrConstant Instance
        = new TypeOrConstant(new PlaceholderTemplateArgumentSymbol());

    private PlaceholderTemplateArgumentSymbol() { }

    public static ImmutableArray<TypeOrConstant> CreateTemplateArguments(
        ImmutableArray<TemplateParameterSymbol> typeParameters) {
        return typeParameters.SelectAsArray(_ => Instance);
    }

    public override string name => "";

    internal override bool mangleName => false;

    internal override BelteDiagnostic error => null;

    internal override bool Equals(TypeSymbol t2, TypeCompareKind comparison) {
        return (object)t2 == this;
    }

    public override int GetHashCode() {
        return System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
    }
}
