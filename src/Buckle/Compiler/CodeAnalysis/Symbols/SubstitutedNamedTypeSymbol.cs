using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A named type that is not constructed with with members.
/// </summary>
internal sealed class SubstitutedNamedTypeSymbol : NamedTypeSymbol {
    internal SubstitutedNamedTypeSymbol(
        NamedTypeSymbol originalDefinition,
        ImmutableArray<TypeOrConstant> templateArguments,
        TemplateMap templateSubstitution)
        : base(
            originalDefinition.templateParameters,
            originalDefinition.templateConstraints,
            originalDefinition.members,
            originalDefinition.declaration,
            originalDefinition.modifiers,
            originalDefinition.accessibility) {
        originalTypeDefinition = originalDefinition;
        this.templateArguments = templateArguments;
        this.templateSubstitution = templateSubstitution;
    }

    internal override ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments
        => (originalTypeDefinition as NamedTypeSymbol).defaultFieldAssignments;

    /// <summary>
    /// The type this symbol inherits from; Object if not explicitly specified.
    /// </summary>
    internal override NamedTypeSymbol baseType => originalTypeDefinition.baseType;

    internal override TypeKind typeKind => originalTypeDefinition.typeKind;

    public override ImmutableArray<TypeOrConstant> templateArguments { get; }

    public override TemplateMap templateSubstitution { get; }

    public new TypeSymbol originalDefinition => originalTypeDefinition;

    public override TypeSymbol originalTypeDefinition { get; }

    public override Symbol originalSymbolDefinition => originalTypeDefinition;
}
