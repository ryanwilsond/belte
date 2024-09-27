using System.Collections.Immutable;
using Buckle.CodeAnalysis.Syntax;

namespace Buckle.CodeAnalysis.Symbols;

/// <summary>
/// A named type that has template arguments substituted.
/// </summary>
internal sealed class SubstitutedNamedTypeSymbol : WrappedNamedTypeSymbol {
    internal SubstitutedNamedTypeSymbol(
        Symbol newContainer,
        TemplateMap map,
        NamedTypeSymbol originalDefinition,
        NamedTypeSymbol constructedFrom)
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

    public override ImmutableArray<TypeOrConstant> templateArguments { get; }

    public override TemplateMap templateSubstitution { get; }

    internal override ImmutableArray<(FieldSymbol, ExpressionSyntax)> defaultFieldAssignments
        => (originalTypeDefinition as NamedTypeSymbol).defaultFieldAssignments;

    /// <summary>
    /// The type this symbol inherits from; Object if not explicitly specified.
    /// </summary>
    internal override NamedTypeSymbol baseType => originalTypeDefinition.baseType;

    internal override TypeKind typeKind => originalTypeDefinition.typeKind;

    internal new TypeSymbol originalDefinition => originalTypeDefinition;

    internal override TypeSymbol originalTypeDefinition { get; }

    internal override Symbol originalSymbolDefinition => originalTypeDefinition;

    internal override bool InheritsFrom(TypeSymbol other) {
        if (other is null)
            return false;

        if (this == other)
            return true;

        if (typeKind != other.typeKind)
            return false;

        if (originalDefinition == other.originalDefinition) {
            if (other is not SubstitutedNamedTypeSymbol s)
                return false;

            foreach (var templateParameter in templateParameters) {
                if (!templateSubstitution.SubstituteTemplate(templateParameter).Equals(
                    s.templateSubstitution.SubstituteTemplate(templateParameter), TypeCompareKind.ConsiderEverything)) {
                    return false;
                }
            }

            return true;
        }

        return InheritsFrom(other.baseType);
    }
}
