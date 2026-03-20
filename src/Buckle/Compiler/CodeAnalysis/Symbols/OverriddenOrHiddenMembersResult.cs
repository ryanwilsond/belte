using System.Collections.Immutable;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class OverriddenOrHiddenMembersResult {
    internal static readonly OverriddenOrHiddenMembersResult Empty = new OverriddenOrHiddenMembersResult([], []);

    private OverriddenOrHiddenMembersResult(
        ImmutableArray<Symbol> overriddenMembers,
        ImmutableArray<Symbol> hiddenMembers) {
        this.overriddenMembers = overriddenMembers;
        this.hiddenMembers = hiddenMembers;
    }

    internal ImmutableArray<Symbol> overriddenMembers { get; }

    internal ImmutableArray<Symbol> hiddenMembers { get; }

    public static OverriddenOrHiddenMembersResult Create(
        ImmutableArray<Symbol> overriddenMembers,
        ImmutableArray<Symbol> hiddenMembers) {
        if (overriddenMembers.IsEmpty && hiddenMembers.IsEmpty)
            return Empty;
        else
            return new OverriddenOrHiddenMembersResult(overriddenMembers, hiddenMembers);
    }

    internal static Symbol GetOverriddenMember(Symbol substitutedOverridingMember, Symbol overriddenByDefinitionMember) {
        if (overriddenByDefinitionMember is not null) {
            var overriddenByDefinitionContaining = overriddenByDefinitionMember.containingType;
            var overriddenByDefinitionContainingTypeDefinition = overriddenByDefinitionContaining.originalDefinition;

            for (var baseType = substitutedOverridingMember.containingType.baseType;
                baseType is not null;
                baseType = baseType.baseType) {
                if (TypeSymbol.Equals(
                    baseType.originalDefinition,
                    overriddenByDefinitionContainingTypeDefinition,
                    TypeCompareKind.ConsiderEverything)) {
                    if (TypeSymbol.Equals(
                        baseType,
                        overriddenByDefinitionContaining,
                        TypeCompareKind.ConsiderEverything)) {
                        return overriddenByDefinitionMember;
                    }

                    return overriddenByDefinitionMember.originalDefinition.SymbolAsMember(baseType);
                }
            }

            throw ExceptionUtilities.Unreachable();
        }

        return null;
    }

    internal Symbol GetOverriddenMember() {
        foreach (var overriddenMember in overriddenMembers) {
            if (overriddenMember.isAbstract || overriddenMember.isVirtual || overriddenMember.isOverride)
                return overriddenMember;
        }

        return null;
    }
}
