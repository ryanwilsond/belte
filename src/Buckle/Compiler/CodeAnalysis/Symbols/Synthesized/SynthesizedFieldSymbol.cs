using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class SynthesizedFieldSymbol : SynthesizedFieldSymbolBase {
    private readonly TypeWithAnnotations _type;

    internal SynthesizedFieldSymbol(
        NamedTypeSymbol containingType,
        TypeSymbol type,
        string name,
        bool isPublic,
        bool isConst,
        bool isConstExpr,
        bool isStatic,
        bool hasConstantValue = false,
        object constantValue = null)
        : base(containingType, name, isPublic, isConst, isConstExpr, isStatic) {
        _type = new TypeWithAnnotations(type);
        this.hasConstantValue = hasConstantValue;
        this.constantValue = constantValue;
    }

    public override RefKind refKind => RefKind.None;

    public override bool hasConstantValue { get; }

    public override object constantValue { get; }

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        return _type;
    }

    internal override ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress) {
        if (hasConstantValue)
            return new ConstantValue(constantValue, _type.specialType);

        return null;
    }
}
