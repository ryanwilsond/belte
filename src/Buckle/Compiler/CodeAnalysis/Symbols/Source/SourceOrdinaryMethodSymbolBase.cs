using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

internal abstract class SourceOrdinaryMethodSymbolBase : SourceOrdinaryMethodOrUserDefinedOperatorSymbol {
    private protected SourceOrdinaryMethodSymbolBase(
        NamedTypeSymbol containingType,
        string name,
        BelteSyntaxNode syntax,
        (DeclarationModifiers modifiers, Flags flags) modifiersAndFlags)
        : base(containingType, new SyntaxReference(syntax), modifiersAndFlags) {
        this.name = name;
    }

    public override string name { get; }

    public abstract override ImmutableArray<TemplateParameterSymbol> templateParameters { get; }

    internal abstract override OneOrMany<SyntaxList<AttributeListSyntax>> GetAttributeDeclarations();
}
