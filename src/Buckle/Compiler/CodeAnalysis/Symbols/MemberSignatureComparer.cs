using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class MemberSignatureComparer : IEqualityComparer<Symbol> {
    // ? AKA CSharpOverrideComparer
    internal static readonly MemberSignatureComparer OverrideComparer = new MemberSignatureComparer(
        considerName: true,
        considerExplicitlyImplementedInterfaces: false,
        considerReturnType: false,
        considerTemplateConstraints: false,
        considerCallingConvention: false,
        refKindCompareMode: RefKindCompareMode.ConsiderDifferences,
        typeComparison: TypeCompareKind.AllIgnoreOptions
    );

    // ? AKA CSharpCustomModifierOverrideComparer
    internal static readonly MemberSignatureComparer OverrideComparerWithReturn = new MemberSignatureComparer(
        considerName: true,
        considerExplicitlyImplementedInterfaces: false,
        considerReturnType: true,
        considerTemplateConstraints: false,
        considerCallingConvention: false,
        refKindCompareMode: RefKindCompareMode.ConsiderDifferences,
        typeComparison: TypeCompareKind.IgnoreTupleNames
    );

    // ? AKA RuntimePlusRefOutSignatureComparer
    internal static readonly MemberSignatureComparer RuntimeOverrideComparerWithReturn = new MemberSignatureComparer(
        considerName: true,
        considerExplicitlyImplementedInterfaces: false,
        considerReturnType: true,
        considerTemplateConstraints: false,
        considerCallingConvention: false,
        refKindCompareMode: RefKindCompareMode.ConsiderDifferences,
        typeComparison: TypeCompareKind.IgnoreTupleNames
    );

    internal static readonly MemberSignatureComparer SloppyOverrideComparer = new MemberSignatureComparer(
        considerName: false,
        considerExplicitlyImplementedInterfaces: false,
        considerReturnType: false,
        considerTemplateConstraints: false,
        considerCallingConvention: false,
        refKindCompareMode: RefKindCompareMode.TreatAllRefAsEquivalent,
        typeComparison: TypeCompareKind.IgnoreArraySizesAndLowerBounds | TypeCompareKind.IgnoreTupleNames
    );

    // ? AKA RuntimeSignatureComparer
    internal static readonly MemberSignatureComparer RuntimeIgnoreRefComparer = new MemberSignatureComparer(
        considerName: true,
        considerExplicitlyImplementedInterfaces: false,
        considerReturnType: true,
        considerTemplateConstraints: false,
        considerCallingConvention: true,
        refKindCompareMode: RefKindCompareMode.TreatAllRefAsEquivalent,
        typeComparison: TypeCompareKind.IgnoreTupleNames
    );

    internal static readonly MemberSignatureComparer DuplicateSourceComparer = new MemberSignatureComparer(
        considerName: true,
        considerExplicitlyImplementedInterfaces: true,
        considerReturnType: false,
        considerTemplateConstraints: false,
        considerCallingConvention: false,
        refKindCompareMode: RefKindCompareMode.TreatAllRefAsEquivalent,
        typeComparison: TypeCompareKind.AllIgnoreOptions
    );

    internal static readonly MemberSignatureComparer ExplicitImplementationWithoutReturnTypeComparer = new MemberSignatureComparer(
        considerName: false,
        considerExplicitlyImplementedInterfaces: false,
        considerReturnType: false,
        considerTemplateConstraints: false,
        considerCallingConvention: true,
        refKindCompareMode: RefKindCompareMode.ConsiderDifferences,
        typeComparison: TypeCompareKind.AllIgnoreOptions
    );

    internal static readonly MemberSignatureComparer ExplicitImplementationComparer = new MemberSignatureComparer(
        considerName: false,
        considerExplicitlyImplementedInterfaces: false,
        considerTemplateConstraints: false,
        considerReturnType: true,
        considerCallingConvention: true,
        refKindCompareMode: RefKindCompareMode.ConsiderDifferences,
        typeComparison: TypeCompareKind.AllIgnoreOptions
    );

    private static readonly MemberSignatureComparer WithTupleNamesComparer = new MemberSignatureComparer(
        considerName: true,
        considerExplicitlyImplementedInterfaces: false,
        considerReturnType: true,
        considerTemplateConstraints: false,
        considerCallingConvention: false,
        refKindCompareMode: RefKindCompareMode.TreatAllRefAsEquivalent,
        typeComparison: TypeCompareKind.AllIgnoreOptions & ~TypeCompareKind.IgnoreTupleNames
    );

    private static readonly MemberSignatureComparer WithoutTupleNamesComparer = new MemberSignatureComparer(
        considerName: true,
        considerExplicitlyImplementedInterfaces: false,
        considerReturnType: true,
        considerTemplateConstraints: false,
        considerCallingConvention: false,
        refKindCompareMode: RefKindCompareMode.TreatAllRefAsEquivalent,
        typeComparison: TypeCompareKind.AllIgnoreOptions
    );

    internal static readonly MemberSignatureComparer RuntimeImplicitImplementationComparer = new MemberSignatureComparer(
        considerName: true,
        considerExplicitlyImplementedInterfaces: true,
        considerReturnType: true,
        considerTemplateConstraints: false,
        considerCallingConvention: true,
        refKindCompareMode: RefKindCompareMode.TreatAllRefAsEquivalent,
        typeComparison: TypeCompareKind.IgnoreTupleNames
    );

    internal static readonly MemberSignatureComparer RuntimeExplicitImplementationSignatureComparer = new MemberSignatureComparer(
        considerName: false,
        considerExplicitlyImplementedInterfaces: false,
        considerReturnType: true,
        considerTemplateConstraints: false,
        considerCallingConvention: true,
        refKindCompareMode: RefKindCompareMode.TreatAllRefAsEquivalent,
        typeComparison: TypeCompareKind.IgnoreTupleNames
    );

    internal static readonly MemberSignatureComparer ImplicitImplementationComparer = new MemberSignatureComparer(
        considerName: true,
        considerExplicitlyImplementedInterfaces: true,
        considerReturnType: true,
        considerCallingConvention: true,
        considerTemplateConstraints: false,
        refKindCompareMode: RefKindCompareMode.TreatAllRefAsEquivalent,
        typeComparison: TypeCompareKind.AllIgnoreOptions
    );

    internal static readonly MemberSignatureComparer CloseImplicitImplementationComparer = new MemberSignatureComparer(
        considerName: true,
        considerExplicitlyImplementedInterfaces: true,
        considerReturnType: false,
        considerTemplateConstraints: false,
        considerCallingConvention: false,
        refKindCompareMode: RefKindCompareMode.ConsiderDifferences,
        typeComparison: TypeCompareKind.AllIgnoreOptions
    );

    private readonly bool _considerName;
    private readonly bool _considerExplicitlyImplementedInterfaces;
    private readonly bool _considerReturnType;
    private readonly bool _considerTemplateConstraints;
    private readonly bool _considerCallingConvention;
    private readonly bool _considerArity;
    private readonly RefKindCompareMode _refKindCompareMode;
    private readonly TypeCompareKind _typeComparison;

    private MemberSignatureComparer(
        bool considerName,
        bool considerExplicitlyImplementedInterfaces,
        bool considerReturnType,
        bool considerTemplateConstraints,
        bool considerCallingConvention,
        RefKindCompareMode refKindCompareMode,
        bool considerArity = true,
        TypeCompareKind typeComparison = TypeCompareKind.ConsiderEverything) {
        _considerName = considerName;
        _considerExplicitlyImplementedInterfaces = considerExplicitlyImplementedInterfaces;
        _considerReturnType = considerReturnType;
        _considerTemplateConstraints = considerTemplateConstraints;
        _considerCallingConvention = considerCallingConvention;
        _considerArity = considerArity;
        _refKindCompareMode = refKindCompareMode;
        _typeComparison = typeComparison;
    }

    public bool Equals(Symbol member1, Symbol member2) {
        if (ReferenceEquals(member1, member2))
            return true;

        if (member1 is null || member2 is null || member1.kind != member2.kind)
            return false;

        var sawInterfaceInName1 = false;
        var sawInterfaceInName2 = false;

        if (_considerName) {
            var name1 = ExplicitInterfaceHelpers.GetMemberNameWithoutInterfaceName(member1.name);
            var name2 = ExplicitInterfaceHelpers.GetMemberNameWithoutInterfaceName(member2.name);

            sawInterfaceInName1 = name1 != member1.name;
            sawInterfaceInName2 = name2 != member2.name;

            if (name1 != name2)
                return false;
        }

        if (_considerArity && (member1.GetMemberArity() != member2.GetMemberArity()))
            return false;

        if (member1.GetParameterCount() != member2.GetParameterCount())
            return false;

        if (_considerCallingConvention && GetCallingConvention(member1) != GetCallingConvention(member2))
            return false;

        var templateMap1 = GetTemplateMap(member1);
        var templateMap2 = GetTemplateMap(member2);

        if (_considerReturnType && !HaveSameReturnTypes(member1, templateMap1, member2, templateMap2, _typeComparison))
            return false;

        if (member1.GetParameterCount() > 0 &&
            !HaveSameParameterTypes(
                member1.GetParameters().AsSpan(),
                templateMap1,
                member2.GetParameters().AsSpan(),
                templateMap2,
                _refKindCompareMode,
                _typeComparison
            )) {
            return false;
        }

        if (_considerExplicitlyImplementedInterfaces) {
            if (sawInterfaceInName1 != sawInterfaceInName2)
                return false;

            if (sawInterfaceInName1) {
                Debug.Assert(sawInterfaceInName2);

                if (member1.IsExplicitInterfaceImplementation() != member2.IsExplicitInterfaceImplementation()) {
                    return false;
                }

                var explicitInterfaceImplementations1 = member1.GetExplicitInterfaceImplementations();
                var explicitInterfaceImplementations2 = member2.GetExplicitInterfaceImplementations();

                if (!explicitInterfaceImplementations1
                    .SetEquals(explicitInterfaceImplementations2, SymbolEqualityComparer.ConsiderEverything)) {
                    return false;
                }
            }
        }

        return !_considerTemplateConstraints || HaveSameConstraints(member1, templateMap1, member2, templateMap2);
    }

    internal static bool HaveSameConstraints(
        TemplateParameterSymbol typeParameter1,
        TemplateMap typeMap1,
        TemplateParameterSymbol typeParameter2,
        TemplateMap typeMap2,
        TypeCompareKind typeComparison) {
        if ((typeParameter1.hasConstructorConstraint != typeParameter2.hasConstructorConstraint) ||
            (typeParameter1.hasReferenceTypeConstraint != typeParameter2.hasReferenceTypeConstraint) ||
            (typeParameter1.hasValueTypeConstraint != typeParameter2.hasValueTypeConstraint) ||
            (typeParameter1.allowsRefLikeType != typeParameter2.allowsRefLikeType) ||
            (typeParameter1.hasDefaultConstraint != typeParameter2.hasDefaultConstraint)) {
            return false;
        }

        return HaveSameTypeConstraints(
            typeParameter1,
            typeMap1,
            typeParameter2,
            typeMap2,
            new SymbolEqualityComparer(typeComparison)
        );
    }

    public int GetHashCode(Symbol member) {
        var hash = 1;

        if (member is not null) {
            hash = Hash.Combine((int)member.kind, hash);

            if (_considerName)
                hash = Hash.Combine(member.name, hash);

            if (_considerReturnType && member.GetMemberArity() == 0 &&
                (_typeComparison & TypeCompareKind.AllIgnoreOptions) == 0) {
                hash = Hash.Combine(member.GetTypeOrReturnType().GetHashCode(), hash);
            }

            // TODO modify hash for constraints?

            if (member.kind != SymbolKind.Field) {
                hash = Hash.Combine(member.GetMemberArity(), hash);
                hash = Hash.Combine(member.GetParameterCount(), hash);
            }
        }

        return hash;
    }

    internal static bool ConsideringTupleNamesCreatesDifference(Symbol member1, Symbol member2) {
        return !WithTupleNamesComparer.Equals(member1, member2) &&
            WithoutTupleNamesComparer.Equals(member1, member2);
    }

    internal static bool HaveSameReturnTypes(
        Symbol member1,
        TemplateMap templateMap1,
        Symbol member2,
        TemplateMap templateMap2,
        TypeCompareKind typeComparison) {
        member1.GetTypeOrReturnType(out var refKind1, out var unsubstitutedReturnType1);
        member2.GetTypeOrReturnType(out var refKind2, out var unsubstitutedReturnType2);

        if (refKind1 != refKind2)
            return false;

        var isVoid1 = unsubstitutedReturnType1.IsVoidType();
        var isVoid2 = unsubstitutedReturnType2.IsVoidType();

        if (isVoid1 != isVoid2)
            return false;

        if (isVoid1)
            return true;

        var returnType1 = SubstituteType(templateMap1, unsubstitutedReturnType1);
        var returnType2 = SubstituteType(templateMap2, unsubstitutedReturnType2);

        if (!returnType1.Equals(returnType2, typeComparison))
            return false;

        return true;
    }

    internal static bool HaveSameParameterTypes(
        ReadOnlySpan<ParameterSymbol> parameters1,
        TemplateMap templateMap1,
        ReadOnlySpan<ParameterSymbol> parameters2,
        TemplateMap templateMap2,
        RefKindCompareMode refKindCompareMode,
        TypeCompareKind typeComparison) {
        var parametersCount = parameters1.Length;

        for (var i = 0; i < parametersCount; i++) {
            var parameter1 = parameters1[i];
            var parameter2 = parameters2[i];

            var type1 = SubstituteType(templateMap1, parameter1.typeWithAnnotations);
            var type2 = SubstituteType(templateMap2, parameter2.typeWithAnnotations);

            if (!type1.Equals(type2, typeComparison))
                return false;

            var refKind1 = parameter1.refKind;
            var refKind2 = parameter2.refKind;

            if (refKindCompareMode != RefKindCompareMode.IgnoreRefKind) {
                if ((refKindCompareMode & RefKindCompareMode.ConsiderDifferences) != 0) {
                    if (refKind1 != refKind2)
                        return false;
                } else {
                    Debug.Assert(refKindCompareMode == RefKindCompareMode.TreatAllRefAsEquivalent);

                    if (refKind1 == RefKind.None != (refKind2 == RefKind.None))
                        return false;
                }
            }
        }

        return true;
    }

    internal static bool HaveSameConstraints(
        ImmutableArray<TemplateParameterSymbol> templateParameters1,
        TemplateMap templateMap1,
        ImmutableArray<TemplateParameterSymbol> templateParameters2,
        TemplateMap templateMap2) {
        var arity = templateParameters1.Length;

        for (var i = 0; i < arity; i++) {
            if (!HaveSameConstraints(templateParameters1[i], templateMap1, templateParameters2[i], templateMap2))
                return false;
        }

        return true;
    }

    internal static bool HaveSameConstraints(
        TemplateParameterSymbol templateParameters1,
        TemplateMap templateMap1,
        TemplateParameterSymbol templateParameters2,
        TemplateMap templateMap2) {
        if ((templateParameters1.hasReferenceTypeConstraint != templateParameters2.hasReferenceTypeConstraint) ||
            (templateParameters1.hasValueTypeConstraint != templateParameters2.hasValueTypeConstraint)) {
            return false;
        }

        return HaveSameTypeConstraints(
            templateParameters1,
            templateMap1,
            templateParameters2,
            templateMap2,
            SymbolEqualityComparer.IgnoreTupleNames
        );
    }

    private static bool HaveSameConstraints(
        Symbol member1,
        TemplateMap templateMap1,
        Symbol member2,
        TemplateMap templateMap2) {
        var arity = member1.GetMemberArity();

        if (arity == 0)
            return true;

        var typeParameters1 = member1.GetMemberTemplateParameters();
        var typeParameters2 = member2.GetMemberTemplateParameters();

        return HaveSameConstraints(typeParameters1, templateMap1, typeParameters2, templateMap2);
    }

    private static bool HaveSameTypeConstraints(
        TemplateParameterSymbol templateParameter1,
        TemplateMap templateMap1,
        TemplateParameterSymbol templateParameter2,
        TemplateMap templateMap2,
        IEqualityComparer<TypeSymbol> comparer) {
        var constraintTypes1 = templateParameter1.constraintTypes;
        var constraintTypes2 = templateParameter2.constraintTypes;

        if ((constraintTypes1.Length == 0) && (constraintTypes2.Length == 0))
            return true;

        var substitutedTypes1 = new HashSet<TypeSymbol>(comparer);
        var substitutedTypes2 = new HashSet<TypeSymbol>(comparer);

        SubstituteConstraintTypes(constraintTypes1, templateMap1, substitutedTypes1);
        SubstituteConstraintTypes(constraintTypes2, templateMap2, substitutedTypes2);

        return AreConstraintTypesSubset(substitutedTypes1, substitutedTypes2, templateParameter2) &&
            AreConstraintTypesSubset(substitutedTypes2, substitutedTypes1, templateParameter1);
    }

    private static bool AreConstraintTypesSubset(
        HashSet<TypeSymbol> constraintTypes1,
        HashSet<TypeSymbol> constraintTypes2,
        TemplateParameterSymbol templateParameter2) {
        foreach (var constraintType in constraintTypes1) {
            if (constraintType.specialType == SpecialType.Object)
                continue;

            if (constraintTypes2.Contains(constraintType))
                continue;

            if (constraintType.specialType == SpecialType.ValueType && templateParameter2.hasValueTypeConstraint)
                continue;

            return false;
        }

        return true;
    }

    private static void SubstituteConstraintTypes(
        ImmutableArray<TypeWithAnnotations> types,
        TemplateMap typeMap,
        HashSet<TypeSymbol> result) {
        foreach (var type in types)
            result.Add(SubstituteType(typeMap, type).type);
    }

    private static TypeWithAnnotations SubstituteType(TemplateMap templateMap, TypeWithAnnotations type) {
        return templateMap is null ? type : type.SubstituteType(templateMap).type;
    }

    internal static TemplateMap GetTemplateMap(Symbol member) {
        var templateParameters = member.GetMemberTemplateParameters();
        return templateParameters.IsEmpty
            ? null
            : new TemplateMap(templateParameters, IndexedTemplateParameterSymbol.Take(member.GetMemberArity()));
    }

    private static CallingConvention GetCallingConvention(Symbol member) {
        switch (member.kind) {
            case SymbolKind.Method:
                return ((MethodSymbol)member).callingConvention;
            default:
                throw ExceptionUtilities.UnexpectedValue(member.kind);
        }
    }
}
