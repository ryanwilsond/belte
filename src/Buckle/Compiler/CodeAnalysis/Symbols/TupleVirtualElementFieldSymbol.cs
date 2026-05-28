using System.Collections.Immutable;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TupleVirtualElementFieldSymbol : TupleElementFieldSymbol {
    private readonly bool _cannotUse;

    public TupleVirtualElementFieldSymbol(
        NamedTypeSymbol container,
        FieldSymbol underlyingField,
        string name,
        int tupleElementIndex,
        ImmutableArray<TextLocation> locations,
        bool cannotUse,
        bool isImplicitlyDeclared,
        FieldSymbol correspondingDefaultFieldOpt)
        : base(
            container,
            underlyingField,
            tupleElementIndex,
            locations,
            isImplicitlyDeclared,
            correspondingDefaultFieldOpt) {
        this.name = name;
        _cannotUse = cannotUse;
    }

    public override string name { get; }

    internal override FieldSymbol originalDefinition => this;

    internal override bool isVirtualTupleField => true;

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        return underlyingField.GetFieldType(fieldsBeingBound);
    }

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return underlyingField.GetAttributes();
    }

    internal override FieldSymbol AsMember(NamedTypeSymbol newOwner) {
        var newUnderlyingOwner = GetNewUnderlyingOwner(newOwner);
        FieldSymbol newCorrespondingDefaultFieldOpt = null;

        if ((object)_correspondingDefaultField != this)
            newCorrespondingDefaultFieldOpt = _correspondingDefaultField.originalDefinition.AsMember(newOwner);

        return new TupleVirtualElementFieldSymbol(
            newOwner,
            underlyingField.originalDefinition.AsMember(newUnderlyingOwner),
            name,
            tupleElementIndex,
            locations,
            _cannotUse,
            isImplicitlyDeclared,
            newCorrespondingDefaultFieldOpt
        );
    }
}
