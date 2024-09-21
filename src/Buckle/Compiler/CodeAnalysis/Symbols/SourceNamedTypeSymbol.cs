using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A named type that is not constructed with type arguments, if generic.
/// </summary>
internal sealed class SourceNamedTypeSymbol : NamedTypeSymbol {
    internal SourceNamedTypeSymbol(
        TypeKind typeKind,
        TypeDeclarationSyntax declaration,
        DeclarationModifiers modifiers,
        Accessibility accessibility)
        : base([], [], [], declaration, modifiers, accessibility) {
        this.typeKind = typeKind;
    }

    internal override ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments => [];

    internal override TypeKind typeKind { get; }

    internal override NamedTypeSymbol baseType => null;

    internal override TypeWithAnnotations typeWithAnnotations => null;

    internal override bool isRef => false;

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override TemplateMap templateSubstitution => null;

    public new TypeSymbol originalDefinition => originalTypeDefinition;

    public override TypeSymbol originalTypeDefinition => this;

    public override Symbol originalSymbolDefinition => originalTypeDefinition;
}
