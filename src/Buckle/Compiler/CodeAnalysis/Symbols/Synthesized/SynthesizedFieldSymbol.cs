
namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedFieldSymbol : SynthesizedFieldSymbolBase {
    private readonly TypeWithAnnotations _type;

    internal SynthesizedFieldSymbol(
        NamedTypeSymbol containingType,
        TypeSymbol type,
        string name,
        bool isPublic,
        bool isConst,
        bool isStatic)
        : base(containingType, name, isPublic, isConst, isStatic) {
        _type = new TypeWithAnnotations(type);
    }

    internal override bool isRef => false;

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        return _type;
    }
}
