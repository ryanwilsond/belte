using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct UserDefinedConversionResult {
    internal readonly ImmutableArray<UserDefinedConversionAnalysis> results;
    internal readonly int best;
    internal readonly UserDefinedConversionResultKind kind;

    internal static UserDefinedConversionResult NoApplicableOperators(ImmutableArray<UserDefinedConversionAnalysis> results) {
        return new UserDefinedConversionResult(
            UserDefinedConversionResultKind.NoApplicableOperators,
            results,
            -1);
    }

    internal static UserDefinedConversionResult NoBestSourceType(ImmutableArray<UserDefinedConversionAnalysis> results) {
        return new UserDefinedConversionResult(
            UserDefinedConversionResultKind.NoBestSourceType,
            results,
            -1);
    }

    internal static UserDefinedConversionResult NoBestTargetType(ImmutableArray<UserDefinedConversionAnalysis> results) {
        return new UserDefinedConversionResult(
            UserDefinedConversionResultKind.NoBestTargetType,
            results,
            -1);
    }

    internal static UserDefinedConversionResult Ambiguous(ImmutableArray<UserDefinedConversionAnalysis> results) {
        return new UserDefinedConversionResult(
            UserDefinedConversionResultKind.Ambiguous,
            results,
            -1);
    }

    internal static UserDefinedConversionResult Valid(ImmutableArray<UserDefinedConversionAnalysis> results, int best) {
        return new UserDefinedConversionResult(
            UserDefinedConversionResultKind.Valid,
            results,
            best);
    }

    private UserDefinedConversionResult(
        UserDefinedConversionResultKind kind,
        ImmutableArray<UserDefinedConversionAnalysis> results,
        int best) {
        this.kind = kind;
        this.results = results;
        this.best = best;
    }
}
