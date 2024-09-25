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

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override TemplateMap templateSubstitution => null;

    internal override ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments => [];

    internal override TypeKind typeKind { get; }

    internal override NamedTypeSymbol baseType => null;

    internal new TypeSymbol originalDefinition => originalTypeDefinition;

    internal override TypeSymbol originalTypeDefinition => this;

    internal override Symbol originalSymbolDefinition => originalTypeDefinition;
}
