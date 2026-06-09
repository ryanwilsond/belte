using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal class TupleElementFieldSymbol : WrappedFieldSymbol {
    private readonly int _tupleElementIndex;

    private protected readonly NamedTypeSymbol _containingTuple;

    private readonly ImmutableArray<TextLocation> _locations;
    private protected readonly FieldSymbol _correspondingDefaultField;

    private readonly bool _isImplicitlyDeclared;

    public TupleElementFieldSymbol(
        NamedTypeSymbol container,
        FieldSymbol underlyingField,
        int tupleElementIndex,
        ImmutableArray<TextLocation> locations,
        bool isImplicitlyDeclared,
        FieldSymbol correspondingDefaultFieldOpt = null)
        : base(underlyingField) {
        _containingTuple = container;
        _tupleElementIndex = correspondingDefaultFieldOpt is null
            ? tupleElementIndex << 1
            : (tupleElementIndex << 1) + 1;
        _locations = locations;
        _isImplicitlyDeclared = isImplicitlyDeclared;
        _correspondingDefaultField = correspondingDefaultFieldOpt ?? this;
    }

    internal sealed override int tupleElementIndex => _tupleElementIndex >> 1;

    internal sealed override bool isDefaultTupleElement => (_tupleElementIndex & 1) == 0;

    internal sealed override bool isExplicitlyNamedTupleElement => _isImplicitlyDeclared;

    internal sealed override FieldSymbol tupleUnderlyingField => underlyingField;

    internal override FieldSymbol originalDefinition {
        get {
            var originalContainer = containingType.originalDefinition;

            if (!originalContainer.isTupleType)
                return this;

            return originalContainer.GetTupleMemberSymbolForUnderlyingMember(underlyingField.originalDefinition);
        }
    }

    internal sealed override Symbol containingSymbol => _containingTuple;

    public sealed override RefKind refKind => underlyingField.refKind;

    internal override TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound) {
        return underlyingField.GetFieldType(fieldsBeingBound);
    }

    internal override ImmutableArray<AttributeData> GetAttributes() {
        return underlyingField.GetAttributes();
    }

    internal override bool requiresCompletion => underlyingField.requiresCompletion;

    internal override bool HasComplete(CompletionParts part) => underlyingField.HasComplete(part);

    internal override void ForceComplete(TextLocation location) {
        underlyingField.ForceComplete(location);
    }

    public sealed override int GetHashCode() {
        return Hash.Combine(_containingTuple.GetHashCode(), _tupleElementIndex.GetHashCode());
    }

    internal sealed override bool Equals(Symbol obj, TypeCompareKind compareKind) {
        var other = obj as TupleElementFieldSymbol;

        if ((object)other == this)
            return true;

        return other is not null &&
            _tupleElementIndex == other._tupleElementIndex &&
            TypeSymbol.Equals(_containingTuple, other._containingTuple, compareKind);
    }

    internal sealed override ImmutableArray<TextLocation> locations => _locations;

    internal sealed override ImmutableArray<SyntaxReference> declaringSyntaxReferences
        => _isImplicitlyDeclared
            ? []
            : GetDeclaringSyntaxReferenceHelper<BelteSyntaxNode>(_locations);

    internal sealed override bool isImplicitlyDeclared => _isImplicitlyDeclared;

    internal sealed override FieldSymbol correspondingTupleField => _correspondingDefaultField;

    internal override FieldSymbol AsMember(NamedTypeSymbol newOwner) {
        var newUnderlyingOwner = GetNewUnderlyingOwner(newOwner);

        return new TupleElementFieldSymbol(
            newOwner,
            underlyingField.originalDefinition.AsMember(newUnderlyingOwner),
            tupleElementIndex,
            locations,
            isImplicitlyDeclared
        );
    }

    private protected NamedTypeSymbol GetNewUnderlyingOwner(NamedTypeSymbol newOwner) {
        var currentIndex = tupleElementIndex;
        var newUnderlyingOwner = newOwner;

        while (currentIndex >= NamedTypeSymbol.ValueTupleRestIndex) {
            newUnderlyingOwner = (NamedTypeSymbol)newUnderlyingOwner
                .templateArguments[NamedTypeSymbol.ValueTupleRestIndex].type.type;
            currentIndex -= NamedTypeSymbol.ValueTupleRestIndex;
        }

        return newUnderlyingOwner;
    }
}
