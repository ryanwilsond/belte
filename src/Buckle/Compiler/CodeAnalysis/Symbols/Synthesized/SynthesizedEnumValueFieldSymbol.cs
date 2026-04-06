
namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedEnumValueFieldSymbol : SynthesizedFieldSymbolBase {
    internal SynthesizedEnumValueFieldSymbol(SourceNamedTypeSymbol containingEnum)
        : base(
            containingEnum,
            WellKnownMemberNames.EnumBackingFieldName,
            isPublic: true,
            isConst: false,
            isConstExpr: false,
            isStatic: false) { }

    public override RefKind refKind => RefKind.None;

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        return new TypeWithAnnotations(((SourceNamedTypeSymbol)containingType).enumUnderlyingType);
    }
}
