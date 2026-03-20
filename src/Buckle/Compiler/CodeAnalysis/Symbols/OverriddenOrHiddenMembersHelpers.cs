using System.Collections.Immutable;
using Buckle.CodeAnalysis.Binding;
using Buckle.Utilities;
using Microsoft.CodeAnalysis.PooledObjects;

namespace Buckle.CodeAnalysis.Symbols;

internal static class OverriddenOrHiddenMembersHelpers {
    internal static OverriddenOrHiddenMembersResult MakeOverriddenOrHiddenMembers(this MethodSymbol member) {
        return MakeOverriddenOrHiddenMembersWorker(member);
    }

    private static OverriddenOrHiddenMembersResult MakeOverriddenOrHiddenMembersWorker(Symbol member) {
        if (!CanOverrideOrHide(member))
            return OverriddenOrHiddenMembersResult.Empty;

        var containingType = member.containingType;

        FindOverriddenOrHiddenMembers(
            member,
            containingType,
            member.declaringCompilation is not null,
            out var hiddenBuilder,
            out var overriddenMembers
        );

        var hiddenMembers = hiddenBuilder is null ? [] : hiddenBuilder.ToImmutableAndFree();
        return OverriddenOrHiddenMembersResult.Create(overriddenMembers, hiddenMembers);
    }

    private static void FindOverriddenOrHiddenMembers(
        Symbol member,
        NamedTypeSymbol containingType,
        bool memberIsFromSomeCompilation,
        out ArrayBuilder<Symbol> hiddenBuilder,
        out ImmutableArray<Symbol> overriddenMembers) {
        Symbol bestMatch = null;
        hiddenBuilder = null;

        for (var currentType = containingType.baseType;
            currentType is not null && bestMatch is null && hiddenBuilder is null;
            currentType = currentType.baseType) {
            FindOverriddenOrHiddenMembersInType(
                member,
                memberIsFromSomeCompilation,
                containingType,
                currentType,
                out bestMatch,
                out _,
                out hiddenBuilder);
        }

        FindRelatedMembers(
            member.isOverride,
            memberIsFromSomeCompilation,
            member.kind,
            bestMatch,
            out overriddenMembers,
            ref hiddenBuilder
        );
    }

    private static void FindOverriddenOrHiddenMembersInType(
        Symbol member,
        bool memberIsFromSomeCompilation,
        NamedTypeSymbol memberContainingType,
        NamedTypeSymbol currentType,
        out Symbol currentTypeBestMatch,
        out bool currentTypeHasSameKindNonMatch,
        out ArrayBuilder<Symbol> hiddenBuilder) {
        currentTypeBestMatch = null;
        currentTypeHasSameKindNonMatch = false;
        hiddenBuilder = null;

        var currentTypeHasExactMatch = false;
        var exactMatchComparer = MemberSignatureComparer.OverrideComparerWithReturn;
        var fallbackComparer = memberIsFromSomeCompilation
            ? MemberSignatureComparer.OverrideComparer
            : MemberSignatureComparer.IgnoreRefComparer;

        var memberKind = member.kind;
        var memberArity = member.GetMemberArity();

        foreach (var otherMember in currentType.GetMembers(member.name)) {
            if (!IsOverriddenSymbolAccessible(otherMember, memberContainingType)) {
            } else if (otherMember.kind != memberKind) {
                var otherMemberArity = otherMember.GetMemberArity();

                if (otherMemberArity == memberArity || (memberKind == SymbolKind.Method && otherMemberArity == 0))
                    AddHiddenMemberIfApplicable(ref hiddenBuilder, memberKind, otherMember);
            } else if (!currentTypeHasExactMatch) {
                switch (memberKind) {
                    case SymbolKind.Field:
                        currentTypeHasExactMatch = true;
                        currentTypeBestMatch = otherMember;
                        break;
                    case SymbolKind.NamedType:
                        if (otherMember.GetMemberArity() == memberArity) {
                            currentTypeHasExactMatch = true;
                            currentTypeBestMatch = otherMember;
                        }

                        break;
                    default:
                        if (exactMatchComparer.Equals(member, otherMember)) {
                            currentTypeHasExactMatch = true;
                            currentTypeBestMatch = otherMember;
                        } else if (fallbackComparer.Equals(member, otherMember)) {
                            currentTypeBestMatch = otherMember;
                        } else {
                            currentTypeHasSameKindNonMatch = true;
                        }

                        break;
                }
            }
        }
    }

    private static void FindRelatedMembers(
        bool isOverride,
        bool overridingMemberIsFromSomeCompilation,
        SymbolKind overridingMemberKind,
        Symbol representativeMember,
        out ImmutableArray<Symbol> overriddenMembers,
        ref ArrayBuilder<Symbol> hiddenBuilder) {
        overriddenMembers = ImmutableArray<Symbol>.Empty;

        if (representativeMember is not null) {
            var needToSearchForRelated = representativeMember.kind != SymbolKind.Field &&
                representativeMember.kind != SymbolKind.NamedType &&
                !representativeMember.containingType.isDefinition;

            if (isOverride) {
                if (needToSearchForRelated) {
                    var overriddenBuilder = ArrayBuilder<Symbol>.GetInstance();
                    overriddenBuilder.Add(representativeMember);

                    FindOtherOverriddenMethodsInContainingType(representativeMember, overridingMemberIsFromSomeCompilation, overriddenBuilder);

                    overriddenMembers = overriddenBuilder.ToImmutableAndFree();
                } else {
                    overriddenMembers = [representativeMember];
                }
            } else {
                AddHiddenMemberIfApplicable(ref hiddenBuilder, overridingMemberKind, representativeMember);

                if (needToSearchForRelated) {
                    FindOtherHiddenMembersInContainingType(
                        overridingMemberKind,
                        representativeMember,
                        ref hiddenBuilder
                    );
                }
            }
        }
    }

    private static void FindOtherOverriddenMethodsInContainingType(
        Symbol representativeMember,
        bool overridingMemberIsFromSomeCompilation,
        ArrayBuilder<Symbol> overriddenBuilder) {

        foreach (var otherMember in representativeMember.containingType.GetMembers(representativeMember.name)) {
            if (otherMember.kind == representativeMember.kind) {
                if (otherMember != representativeMember) {
                    if (overridingMemberIsFromSomeCompilation) {
                        if (MemberSignatureComparer.OverrideComparer.Equals(otherMember, representativeMember))
                            overriddenBuilder.Add(otherMember);
                    } else {
                        if (MemberSignatureComparer.OverrideComparerWithReturn.Equals(
                            otherMember,
                            representativeMember)) {
                            overriddenBuilder.Add(otherMember);
                        }
                    }
                }
            }
        }
    }

    private static void FindOtherHiddenMembersInContainingType(
        SymbolKind hidingMemberKind,
        Symbol representativeMember,
        ref ArrayBuilder<Symbol> hiddenBuilder) {
        foreach (var otherMember in representativeMember.containingType.GetMembers(representativeMember.name)) {
            if (otherMember.kind == representativeMember.kind) {
                if (otherMember != representativeMember &&
                    MemberSignatureComparer.OverrideComparerWithReturn.Equals(otherMember, representativeMember)) {
                    AddHiddenMemberIfApplicable(ref hiddenBuilder, hidingMemberKind, otherMember);
                }
            }
        }
    }

    private static void AddHiddenMemberIfApplicable(
        ref ArrayBuilder<Symbol> hiddenBuilder,
        SymbolKind hidingMemberKind,
        Symbol hiddenMember) {
        if (hiddenMember.kind != SymbolKind.Method ||
            ((MethodSymbol)hiddenMember).CanBeHiddenByMemberKind(hidingMemberKind)) {
            AccessOrGetInstance(ref hiddenBuilder).Add(hiddenMember);
        }
    }

    private static ArrayBuilder<T> AccessOrGetInstance<T>(ref ArrayBuilder<T> builder) {
        builder ??= ArrayBuilder<T>.GetInstance();
        return builder;
    }

    private static bool CanOverrideOrHide(Symbol member) {
        switch (member.kind) {
            case SymbolKind.Method:
                var methodSymbol = (MethodSymbol)member;
                return MethodSymbol.CanOverrideOrHide(methodSymbol.methodKind) &&
                    ReferenceEquals(methodSymbol, methodSymbol.constructedFrom);
            default:
                throw ExceptionUtilities.UnexpectedValue(member.kind);
        }
    }

    private static bool IsOverriddenSymbolAccessible(Symbol overridden, NamedTypeSymbol overridingContainingType) {
        return AccessCheck.IsSymbolAccessible(
            overridden.originalDefinition,
            overridingContainingType.originalDefinition
        );
    }
}
