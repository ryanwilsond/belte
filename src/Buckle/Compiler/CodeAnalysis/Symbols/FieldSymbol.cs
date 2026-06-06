using Buckle.CodeAnalysis.Binding;
using Buckle.Diagnostics;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a field in a class or struct.
/// </summary>
internal abstract class FieldSymbol : Symbol, IFieldSymbol {
    internal FieldSymbol() { }

    public override SymbolKind kind => SymbolKind.Field;

    public abstract bool isConst { get; }

    public abstract bool isFinal { get; }

    public abstract bool isConstExpr { get; }

    public abstract RefKind refKind { get; }

    public bool isNullable => typeWithAnnotations.isNullable;

    public virtual bool hasConstantValue {
        get {
            if (!isConst && !isConstExpr)
                return false;

            var constantValue = GetConstantValue(ConstantFieldsInProgress.Empty);
            return constantValue is not null;
        }
    }

    public virtual object constantValue {
        get {
            if (!isConst && !isConstExpr)
                return null;

            var constantValue = GetConstantValue(ConstantFieldsInProgress.Empty);
            return constantValue?.value;
        }
    }

    internal virtual int tupleElementIndex {
        get {
            if (!containingType.isTupleType)
                return -1;

            if (!containingType.isDefinition)
                return originalDefinition.tupleElementIndex;

            var tupleElementPosition = NamedTypeSymbol.MatchesCanonicalTupleElementName(name);
            var arity = containingType.arity;

            if (tupleElementPosition <= 0 || tupleElementPosition > arity)
                return -1;

            var wellKnownMember = NamedTypeSymbol.GetTupleTypeMember(arity, tupleElementPosition);
            Symbol found = null;
            // TODO Do we really want to transiently have a well known member for every field?
            // TODO The only other solution is to rely upon the actual assembly definition, which complicated the Evaluator a lot!
            // MemberDescriptor descriptor = WellKnownMembers.GetDescriptor(wellKnownMember);
            // Symbol found = CSharpCompilation.GetRuntimeMember(ImmutableArray.Create<Symbol>(this), descriptor, CSharpCompilation.SpecialMembersSignatureComparer.Instance,
            //     accessWithinOpt: null); // force lookup of public members only

            return found is not null
                ? tupleElementPosition - 1
                : -1;
        }
    }

    internal virtual bool isDefaultTupleElement => tupleElementIndex >= 0;

    internal virtual bool isExplicitlyNamedTupleElement => false;

    internal virtual FieldSymbol tupleUnderlyingField => containingType.isTupleType ? this : null;

    internal virtual FieldSymbol correspondingTupleField => tupleElementIndex >= 0 ? this : null;

    internal virtual bool isVirtualTupleField => false;

    internal new virtual FieldSymbol originalDefinition => this;

    private protected sealed override Symbol _originalSymbolDefinition => originalDefinition;

    internal TypeWithAnnotations typeWithAnnotations => GetFieldType(ConsList<FieldSymbol>.Empty);

    internal TypeSymbol type => typeWithAnnotations.type;

    internal virtual bool isFixedSizeBuffer => false;

    internal virtual int fixedSize => 0;

    internal virtual bool requiresInstanceReceiver => !isStatic;

    internal virtual bool isAnonymousUnionMember => false;

    internal virtual int unionGroupId => -1;

    internal sealed override bool isAbstract => false;

    internal sealed override bool isOverride => false;

    internal sealed override bool isSealed => false;

    internal sealed override bool isVirtual => false;

    internal sealed override bool isExtern => false;

    internal virtual bool isCapturedFrame => false;

    internal bool isMetadataConstant => isConstExpr;

    internal virtual BelteDiagnostic definiteAssignmentError => null;

    internal abstract ConstantValue GetConstantValue(ConstantFieldsInProgress inProgress);

    internal abstract TypeWithAnnotations GetFieldType(ConsList<FieldSymbol> fieldsBeingBound);

    internal override void Accept(SymbolVisitor visitor) {
        visitor.VisitField(this);
    }

    internal override TResult Accept<TArgument, TResult>(
        SymbolVisitor<TArgument, TResult> visitor,
        TArgument argument) {
        return visitor.VisitField(this, argument);
    }

    internal virtual FieldSymbol AsMember(NamedTypeSymbol newOwner) {
        return newOwner.isDefinition ? this : new SubstitutedFieldSymbol(newOwner as SubstitutedNamedTypeSymbol, this);
    }

    internal override bool Equals(Symbol other, TypeCompareKind compareKind) {
        if (other is SubstitutedFieldSymbol sfs)
            return sfs.Equals(this, compareKind);

        return base.Equals(other, compareKind);
    }

    public override int GetHashCode() {
        return base.GetHashCode();
    }

    ITypeSymbol IFieldSymbol.type => type;
}
