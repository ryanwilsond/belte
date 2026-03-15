using Buckle.CodeAnalysis.Binding;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a field in a class or struct.
/// </summary>
internal abstract class FieldSymbol : Symbol, IFieldSymbol {
    internal FieldSymbol() { }

    public override SymbolKind kind => SymbolKind.Field;

    public abstract bool isConst { get; }

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

    internal new virtual FieldSymbol originalDefinition => this;

    private protected sealed override Symbol _originalSymbolDefinition => originalDefinition;

    internal TypeWithAnnotations typeWithAnnotations => GetFieldType(ConsList<FieldSymbol>.Empty);

    internal TypeSymbol type => typeWithAnnotations.type;

    internal virtual bool requiresInstanceReceiver => !isStatic;

    internal sealed override bool isAbstract => false;

    internal sealed override bool isOverride => false;

    internal sealed override bool isSealed => false;

    internal sealed override bool isVirtual => false;

    internal virtual bool isCapturedFrame => false;

    internal bool isMetadataConstant => isConstExpr;

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
