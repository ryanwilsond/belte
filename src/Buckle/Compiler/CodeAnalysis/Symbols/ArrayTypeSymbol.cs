using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// An array type symbol.
/// </summary>
internal sealed class ArrayTypeSymbol : NamedTypeSymbol {
    internal ArrayTypeSymbol(TypeSymbol underlyingType, int rank)
        : base([], [], [], null, DeclarationModifiers.None, Accessibility.NotApplicable) {
        this.underlyingType = underlyingType;
        this.rank = rank;
    }

    internal override TypeKind typeKind => TypeKind.Array;

    internal override NamedTypeSymbol baseType => underlyingType.baseType;

    public override ImmutableArray<TypeOrConstant> templateArguments
        => underlyingType is NamedTypeSymbol n ? n.templateArguments : [];

    public override TemplateMap templateSubstitution
        => underlyingType is NamedTypeSymbol n ? n.templateSubstitution : null;

    internal override ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments
        => underlyingType is NamedTypeSymbol n ? n.defaultFieldAssignments : [];

    internal TypeSymbol underlyingType { get; }

    internal int rank { get; }
}
