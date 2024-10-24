using System;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// Represents a parameter of a method.
/// </summary>
internal abstract class ParameterSymbol : Symbol {
    internal ParameterSymbol() { }

    public override SymbolKind kind => SymbolKind.Parameter;

    internal override Accessibility declaredAccessibility => Accessibility.NotApplicable;

    internal override bool isAbstract => false;

    internal override bool isSealed => false;

    internal override bool isVirtual => false;

    internal override bool isOverride => false;

    internal override bool isStatic => false;

    internal abstract TypeWithAnnotations typeWithAnnotations { get; }

    internal abstract RefKind refKind { get; }

    internal abstract int ordinal { get; }

    internal abstract bool isMetadataOptional { get; }

    internal abstract ScopedKind effectiveScope { get; }

    internal abstract ConstantValue explicitDefaultConstantValue { get; }

    internal bool hasExplicitDefaultValue => isOptional && explicitDefaultConstantValue is not null;

    internal object explicitDefaultValue {
        get {
            if (hasExplicitDefaultValue)
                return explicitDefaultConstantValue.value;

            throw new InvalidOperationException();
        }
    }

    internal bool isOptional => refKind == RefKind.None && isMetadataOptional;

    internal TypeSymbol type => typeWithAnnotations.type;

    internal new virtual ParameterSymbol originalDefinition => this;

    private protected sealed override Symbol _originalSymbolDefinition => originalDefinition;
}
