using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class WrappedNamedTypeSymbol : NamedTypeSymbol {
    internal WrappedNamedTypeSymbol(NamedTypeSymbol underlyingNamedType) {
        this.underlyingNamedType = underlyingNamedType;
    }

    public override string name => underlyingNamedType.name;

    public override string metadataName => underlyingNamedType.metadataName;

    internal override bool mangleName => underlyingNamedType.mangleName;

    internal NamedTypeSymbol underlyingNamedType { get; }

    internal override int arity => underlyingNamedType.arity;

    internal override Accessibility declaredAccessibility => underlyingNamedType.declaredAccessibility;

    internal override TypeKind typeKind => underlyingNamedType.typeKind;

    internal override SyntaxReference syntaxReference => underlyingNamedType.syntaxReference;

    internal override bool isStatic => underlyingNamedType.isStatic;

    internal override bool isAbstract => underlyingNamedType.isAbstract;

    internal override bool isSealed => underlyingNamedType.isSealed;
}
