using System;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a parameter of a method.
/// </summary>
internal abstract class ParameterSymbol : Symbol, IParameterSymbol {
    internal ParameterSymbol() { }

    public override SymbolKind kind => SymbolKind.Parameter;

    public abstract RefKind refKind { get; }

    public abstract int ordinal { get; }

    public bool isNullable => typeWithAnnotations.isNullable;

    public object explicitDefaultValue {
        get {
            if (hasExplicitDefaultValue)
                return explicitDefaultConstantValue.value;

            throw new InvalidOperationException();
        }
    }

    internal override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal override bool isVirtual => false;

    internal override bool isOverride => false;

    internal override bool isStatic => false;

    internal abstract TypeWithAnnotations typeWithAnnotations { get; }

    internal abstract bool isMetadataOptional { get; }

    internal abstract bool hasUnscopedRefAttribute { get; }

    internal abstract ScopedKind effectiveScope { get; }

    internal abstract ConstantValue explicitDefaultConstantValue { get; }

    internal virtual bool isThis => false;

    internal bool hasExplicitDefaultValue => isOptional && explicitDefaultConstantValue is not null;

    internal bool isOptional => refKind == RefKind.None && isMetadataOptional;

    internal TypeSymbol type => typeWithAnnotations.type;

    internal new virtual ParameterSymbol originalDefinition => this;

    private protected sealed override Symbol _originalSymbolDefinition => originalDefinition;

    internal override TResult Accept<TArgument, TResult>(
        SymbolVisitor<TArgument, TResult> visitor,
        TArgument argument) {
        return visitor.VisitParameter(this, argument);
    }

    bool IParameterSymbol.isOptional => isMetadataOptional;

    ITypeSymbol IParameterSymbol.type => type;
}
