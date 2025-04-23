using Buckle.CodeAnalysis.Syntax;
using Buckle.CodeAnalysis.Text;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedNamedTypeSymbol : NamedTypeSymbol {
    internal WrappedNamedTypeSymbol(NamedTypeSymbol underlyingNamedType) {
        this.underlyingNamedType = underlyingNamedType;
    }

    public override string name => underlyingNamedType.name;

    public override string metadataName => underlyingNamedType.metadataName;

    public override int arity => underlyingNamedType.arity;

    public override TypeKind typeKind => underlyingNamedType.typeKind;

    public override SpecialType specialType => underlyingNamedType.specialType;

    internal override bool mangleName => underlyingNamedType.mangleName;

    internal NamedTypeSymbol underlyingNamedType { get; }

    internal override Accessibility declaredAccessibility => underlyingNamedType.declaredAccessibility;

    internal override SyntaxReference syntaxReference => underlyingNamedType.syntaxReference;

    internal override TextLocation location => underlyingNamedType.location;

    internal override bool isStatic => underlyingNamedType.isStatic;

    internal override bool isAbstract => underlyingNamedType.isAbstract;

    internal override bool isSealed => underlyingNamedType.isSealed;

    internal override bool isRefLikeType => underlyingNamedType.isRefLikeType;
}
