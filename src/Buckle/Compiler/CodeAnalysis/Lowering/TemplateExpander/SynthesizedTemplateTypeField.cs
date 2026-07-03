using System.Collections.Immutable;
using Buckle.CodeAnalysis.Symbols;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Lowering;

internal sealed class SynthesizedTemplateTypeField : WrappedFieldSymbol {
    private readonly SynthesizedTemplateType _containingType;
    private readonly TypeWithAnnotations _type;

    private int _hashCode;

    internal SynthesizedTemplateTypeField(
        TemplateExpander templateExpander,
        SynthesizedTemplateType newOwner,
        FieldSymbol field)
        : base(field) {
        _containingType = newOwner;
        _type = templateExpander.SubstituteType(field.typeWithAnnotations, newOwner, field, field.location);
    }

    internal override Symbol containingSymbol => _containingType;

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return originalDefinition.GetAttributes();
    }

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        return _type;
    }

    internal override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        if ((object)this == obj)
            return true;

        return obj is FieldSymbol other &&
            TypeSymbol.Equals(_containingType, other.containingType, compareKind) &&
            originalDefinition == other.originalDefinition;
    }

    public override int GetHashCode() {
        if (_hashCode == 0)
            _hashCode = ComputeHashCode();

        return _hashCode;
    }

    private int ComputeHashCode() {
        var code = System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(this);
        var containingHashCode = _containingType.GetHashCode();

        if (containingHashCode != originalDefinition.containingType.GetHashCode())
            code = Hash.Combine(containingHashCode, code);

        return code;
    }
}
