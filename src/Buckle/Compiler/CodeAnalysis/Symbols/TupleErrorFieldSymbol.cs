using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class TupleErrorFieldSymbol : SynthesizedFieldSymbolBase {
    private readonly TypeWithAnnotations _type;
    private readonly int _tupleElementIndex;

    private readonly ImmutableArray<TextLocation> _locations;
    private readonly TupleErrorFieldSymbol _correspondingDefaultField;

    private readonly bool _isImplicitlyDeclared;

    public TupleErrorFieldSymbol(
        NamedTypeSymbol container,
        string name,
        int tupleElementIndex,
        TextLocation location,
        TypeWithAnnotations type,
        bool isImplicitlyDeclared,
        TupleErrorFieldSymbol correspondingDefaultFieldOpt)
        : base(container, name, isPublic: true, isConst: false, isFinal: false, isConstExpr: false, isStatic: false) {
        _type = type;
        _locations = location == null ? ImmutableArray<TextLocation>.Empty : ImmutableArray.Create(location);
        _tupleElementIndex = correspondingDefaultFieldOpt is null
            ? tupleElementIndex << 1
            : (tupleElementIndex << 1) + 1;
        _isImplicitlyDeclared = isImplicitlyDeclared;
        _correspondingDefaultField = correspondingDefaultFieldOpt ?? this;
    }

    internal override int tupleElementIndex {
        get {
            if (_tupleElementIndex < 0)
                return -1;

            return _tupleElementIndex >> 1;
        }
    }

    internal override bool isDefaultTupleElement => (_tupleElementIndex & ((1 << 31) | 1)) == 0;

    internal override bool isExplicitlyNamedTupleElement => _tupleElementIndex >= 0 && !_isImplicitlyDeclared;

    internal override FieldSymbol tupleUnderlyingField => null;

    internal override FieldSymbol originalDefinition => this;

    internal override ImmutableArray<TextLocation> locations => _locations;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences {
        get {
            return _isImplicitlyDeclared
                ? ImmutableArray<SyntaxReference>.Empty
                : GetDeclaringSyntaxReferenceHelper<BelteSyntaxNode>(_locations);
        }
    }

    internal override bool isImplicitlyDeclared => _isImplicitlyDeclared;

    internal override FieldSymbol correspondingTupleField => _correspondingDefaultField;

    public override RefKind refKind => RefKind.None;

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        return _type;
    }

    public sealed override int GetHashCode() {
        return Hash.Combine(containingType.GetHashCode(), _tupleElementIndex.GetHashCode());
    }

    internal override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        return Equals(obj as TupleErrorFieldSymbol, compareKind);
    }

    public bool Equals(TupleErrorFieldSymbol other, TypeCompareKind compareKind) {
        if ((object)other == this)
            return true;

        return other is not null &&
            _tupleElementIndex == other._tupleElementIndex &&
            TypeSymbol.Equals(containingType, other.containingType, compareKind);
    }

    internal override FieldSymbol AsMember(NamedTypeSymbol newOwner) {
        if (ReferenceEquals(newOwner, containingType))
            return this;

        TupleErrorFieldSymbol newCorrespondingField = null;

        if (!ReferenceEquals(_correspondingDefaultField, this))
            newCorrespondingField = (TupleErrorFieldSymbol)_correspondingDefaultField.AsMember(newOwner);

        return new TupleErrorFieldSymbol(
            newOwner,
            name,
            tupleElementIndex,
            _locations.IsEmpty ? null : location,
            newOwner.tupleElementTypes[tupleElementIndex].type,
            _isImplicitlyDeclared,
            newCorrespondingField
        );
    }
}
