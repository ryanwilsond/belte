using System.Collections.Immutable;

namespace Buckle.CodeAnalysis.Binding;

internal readonly struct ArgumentAnalysisResult {
    internal readonly ImmutableArray<int> argsToParams;
    internal readonly int argumentPosition;
    internal readonly int parameterPosition;
    internal readonly ArgumentAnalysisResultKind kind;

    internal int ParameterFromArgument(int arg) {
        if (argsToParams.IsDefault)
            return arg;

        return argsToParams[arg];
    }

    private ArgumentAnalysisResult(
        ArgumentAnalysisResultKind kind,
        int argumentPosition,
        int parameterPosition,
        ImmutableArray<int> argsToParams) {
        this.kind = kind;
        this.argumentPosition = argumentPosition;
        this.parameterPosition = parameterPosition;
        this.argsToParams = argsToParams;
    }

    internal bool isValid => kind < ArgumentAnalysisResultKind.FirstInvalid;

    internal static ArgumentAnalysisResult NameUsedForPositional(int argumentPosition) {
        return new ArgumentAnalysisResult(
            ArgumentAnalysisResultKind.NameUsedForPositional,
            argumentPosition,
            0,
            default
        );
    }

    internal static ArgumentAnalysisResult NoCorrespondingParameter(int argumentPosition) {
        return new ArgumentAnalysisResult(
            ArgumentAnalysisResultKind.NoCorrespondingParameter,
            argumentPosition,
            0,
            default
        );
    }

    internal static ArgumentAnalysisResult NoCorrespondingNamedParameter(int argumentPosition) {
        return new ArgumentAnalysisResult(
            ArgumentAnalysisResultKind.NoCorrespondingNamedParameter,
            argumentPosition,
            0,
            default
        );
    }

    internal static ArgumentAnalysisResult DuplicateNamedArgument(int argumentPosition) {
        return new ArgumentAnalysisResult(
            ArgumentAnalysisResultKind.DuplicateNamedArgument,
            argumentPosition,
            0,
            default
        );
    }

    internal static ArgumentAnalysisResult RequiredParameterMissing(int parameterPosition) {
        return new ArgumentAnalysisResult(
            ArgumentAnalysisResultKind.RequiredParameterMissing,
            0,
            parameterPosition,
            default
        );
    }

    internal static ArgumentAnalysisResult BadNonTrailingNamedArgument(int argumentPosition) {
        return new ArgumentAnalysisResult(
            ArgumentAnalysisResultKind.BadNonTrailingNamedArgument,
            argumentPosition,
            0,
            default
        );
    }

    internal static ArgumentAnalysisResult NormalForm(ImmutableArray<int> argsToParamsOpt) {
        return new ArgumentAnalysisResult(
            ArgumentAnalysisResultKind.Normal,
            0,
            0,
            argsToParamsOpt
        );
    }
}
