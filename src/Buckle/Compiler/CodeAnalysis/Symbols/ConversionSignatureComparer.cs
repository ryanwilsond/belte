using System.Collections.Generic;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed class ConversionSignatureComparer : IEqualityComparer<SourceUserDefinedConversionSymbol> {
    private ConversionSignatureComparer() { }

    internal static ConversionSignatureComparer Comparer { get; } = new ConversionSignatureComparer();

    public bool Equals(SourceUserDefinedConversionSymbol member1, SourceUserDefinedConversionSymbol member2) {
        if (ReferenceEquals(member1, member2))
            return true;

        if (member1 is null || member2 is null)
            return false;

        if (member1.parameterCount != 1 || member2.parameterCount != 1)
            return false;

        return member1.returnType.Equals(member2.returnType, TypeCompareKind.ConsiderEverything)
            && member1.parameterTypesWithAnnotations[0].Equals(
                member2.parameterTypesWithAnnotations[0],
                TypeCompareKind.ConsiderEverything)
            && (member1.name == WellKnownMemberNames.ImplicitConversionName ||
                member2.name == WellKnownMemberNames.ImplicitConversionName ||
                member1.name == member2.name);
    }

    public int GetHashCode(SourceUserDefinedConversionSymbol member) {
        if (member is null)
            return 0;

        var hash = 1;
        hash = Hash.Combine(member.returnType.GetHashCode(), hash);

        if (member.parameterCount != 1)
            return hash;

        hash = Hash.Combine(member.GetParameterType(0).GetHashCode(), hash);
        return hash;
    }
}
