using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A named type that is not constructed with with members.
/// </summary>
internal sealed class ConstructedNamedTypeSymbol : NamedTypeSymbol {
    internal ConstructedNamedTypeSymbol(
        NamedTypeSymbol originalDefinition,
        ImmutableArray<TemplateParameterSymbol> templateParameters,
        ImmutableArray<BoundExpression> templateConstraints,
        ImmutableArray<Symbol> members,
        ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments,
        NamedTypeSymbol baseType)
        : base(
            templateParameters,
            templateConstraints,
            members,
            originalDefinition.declaration,
            originalDefinition.modifiers,
            originalDefinition.accessibility
        ) {
        this.defaultFieldAssignments = defaultFieldAssignments;
        this.baseType = baseType;
        originalTypeDefinition = originalDefinition;
    }

    public override ImmutableArray<TypeOrConstant> templateArguments => [];

    public override TemplateMap templateSubstitution => null;

    /// <summary>
    /// Statements that assigns fields with specified initializers. Used in constructors.
    /// </summary>
    internal override ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments { get; }

    /// <summary>
    /// The type this symbol inherits from; Object if not explicitly specified.
    /// </summary>
    internal override NamedTypeSymbol baseType { get; }

    internal override TypeKind typeKind => originalTypeDefinition.typeKind;

    internal new TypeSymbol originalDefinition => originalTypeDefinition;

    internal override TypeSymbol originalTypeDefinition { get; }

    internal override Symbol originalSymbolDefinition => originalTypeDefinition;

    protected override void ConstructLazyMembers() {
        _lazyMembers = [.. members, .. baseType?.GetMembers() ?? []];
    }
}
