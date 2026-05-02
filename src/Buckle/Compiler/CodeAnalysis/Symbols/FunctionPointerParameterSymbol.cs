using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class FunctionPointerParameterSymbol : ParameterSymbol {
    private readonly FunctionPointerMethodSymbol _containingSymbol;

    public FunctionPointerParameterSymbol(
        TypeWithAnnotations typeWithAnnotations,
        RefKind refKind,
        int ordinal,
        FunctionPointerMethodSymbol containingSymbol) {
        this.typeWithAnnotations = typeWithAnnotations;
        this.refKind = refKind;
        this.ordinal = ordinal;
        _containingSymbol = containingSymbol;
    }

    internal override TypeWithAnnotations typeWithAnnotations { get; }

    public override RefKind refKind { get; }

    public override int ordinal { get; }

    internal override Symbol containingSymbol => _containingSymbol;

    internal override ScopedKind effectiveScope => ScopedKind.None;

    internal override bool hasUnscopedRefAttribute => false;

    internal override bool Equals(Symbol other, TypeCompareKind compareKind) {
        if (ReferenceEquals(this, other)) {
            return true;
        }

        if (!(other is FunctionPointerParameterSymbol param)) {
            return false;
        }

        return Equals(param, compareKind);
    }

    internal bool Equals(FunctionPointerParameterSymbol other, TypeCompareKind compareKind) {
        return other.ordinal == ordinal && _containingSymbol.Equals(other._containingSymbol, compareKind);
    }

    internal bool MethodEqualityChecks(FunctionPointerParameterSymbol other, TypeCompareKind compareKind) {
        return FunctionPointerTypeSymbol.RefKindEquals(compareKind, refKind, other.refKind) &&
            typeWithAnnotations.Equals(other.typeWithAnnotations, compareKind);
    }

    public override int GetHashCode() {
        return Hash.Combine(_containingSymbol.GetHashCode(), ordinal + 1);
    }

    internal int MethodHashCode() {
        return Hash.Combine(typeWithAnnotations.GetHashCode(),
            ((int)FunctionPointerTypeSymbol.GetRefKindForHashCode(refKind)).GetHashCode());
    }

    internal override ImmutableArray<TextLocation> locations => [];

    internal override TextLocation location => null;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences => [];

    internal override SyntaxReference syntaxReference => null;

    internal override bool isImplicitlyDeclared => true;

    internal override bool isMetadataOptional => false;

    internal override bool isMetadataOut => refKind == RefKind.Out;

    internal override ConstantValue explicitDefaultConstantValue => null;
}
