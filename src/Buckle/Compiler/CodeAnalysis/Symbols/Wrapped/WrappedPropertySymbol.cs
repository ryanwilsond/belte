using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedPropertySymbol : PropertySymbol {
    private protected readonly PropertySymbol _underlyingProperty;

    internal WrappedPropertySymbol(PropertySymbol underlyingProperty) {
        _underlyingProperty = underlyingProperty;
    }

    public override string name => _underlyingProperty.name;

    public override string metadataName => _underlyingProperty.metadataName;

    internal PropertySymbol underlyingProperty => _underlyingProperty;

    internal override bool isImplicitlyDeclared => _underlyingProperty.isImplicitlyDeclared;

    internal override RefKind refKind => _underlyingProperty.refKind;

    internal override CallingConvention callingConvention => _underlyingProperty.callingConvention;

    internal override bool hasSpecialName => _underlyingProperty.hasSpecialName;

    internal override ImmutableArray<TextLocation> locations => _underlyingProperty.locations;

    internal override TextLocation location => _underlyingProperty.location;

    internal override ImmutableArray<SyntaxReference> declaringSyntaxReferences
        => _underlyingProperty.declaringSyntaxReferences;

    internal override SyntaxReference syntaxReference => _underlyingProperty.syntaxReference;

    internal override Accessibility declaredAccessibility => _underlyingProperty.declaredAccessibility;

    internal override bool isStatic => _underlyingProperty.isStatic;

    internal override bool isVirtual => _underlyingProperty.isVirtual;

    internal override bool isOverride => _underlyingProperty.isOverride;

    internal override bool isAbstract => _underlyingProperty.isAbstract;

    internal override bool isSealed => _underlyingProperty.isSealed;

    internal override bool isExtern => _underlyingProperty.isExtern;
}
