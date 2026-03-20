using System.Collections.Immutable;
using System.Linq;
using Buckle.Diagnostics;
using Buckle.Utilities;

namespace Buckle.CodeAnalysis.Binding;

internal struct MemberAnalysisResult {
    internal readonly ImmutableArray<Conversion> conversions;
    internal readonly BitVector badArguments;
    internal readonly ImmutableArray<int> argsToParams;
    internal readonly BelteDiagnosticQueue constraintFailureDiagnostics;

    internal readonly int badParameter;
    internal readonly MemberResolutionKind kind;
    internal readonly bool hasAnyRefOmittedArgument;

    private MemberAnalysisResult(
        MemberResolutionKind kind,
        BitVector badArguments = default,
        ImmutableArray<int> argsToParams = default,
        ImmutableArray<Conversion> conversions = default,
        int missingParameter = -1,
        bool hasAnyRefOmittedArgument = false,
        BelteDiagnosticQueue constraintFailureDiagnostics = default) {
        this.kind = kind;
        this.badArguments = badArguments;
        this.argsToParams = argsToParams;
        this.conversions = conversions;
        badParameter = missingParameter;
        this.hasAnyRefOmittedArgument = hasAnyRefOmittedArgument;
        this.constraintFailureDiagnostics = constraintFailureDiagnostics;
    }

    public override bool Equals(object obj) {
        throw ExceptionUtilities.Unreachable();
    }

    public override int GetHashCode() {
        throw ExceptionUtilities.Unreachable();
    }

    internal readonly Conversion ConversionForArg(int arg) {
        if (conversions.IsDefault)
            return Conversion.Identity;

        return conversions[arg];
    }

    internal readonly int ParameterFromArgument(int arg) {
        if (argsToParams.IsDefault)
            return arg;

        return argsToParams[arg];
    }

    internal readonly int firstBadArgument => badArguments.TrueBits().First();

    internal readonly bool isApplicable => kind switch {
        MemberResolutionKind.Applicable or MemberResolutionKind.Worse or MemberResolutionKind.Worst => true,
        _ => false,
    };

    internal readonly bool isValid => kind == MemberResolutionKind.Applicable;

    internal static MemberAnalysisResult ArgumentParameterMismatch(ArgumentAnalysisResult argAnalysis) {
        switch (argAnalysis.kind) {
            case ArgumentAnalysisResultKind.NoCorrespondingParameter:
                return NoCorrespondingParameter(argAnalysis.argumentPosition);
            case ArgumentAnalysisResultKind.NoCorrespondingNamedParameter:
                return NoCorrespondingNamedParameter(argAnalysis.argumentPosition);
            case ArgumentAnalysisResultKind.DuplicateNamedArgument:
                return DuplicateNamedArgument(argAnalysis.argumentPosition);
            case ArgumentAnalysisResultKind.RequiredParameterMissing:
                return RequiredParameterMissing(argAnalysis.parameterPosition);
            case ArgumentAnalysisResultKind.NameUsedForPositional:
                return NameUsedForPositional(argAnalysis.argumentPosition);
            case ArgumentAnalysisResultKind.BadNonTrailingNamedArgument:
                return BadNonTrailingNamedArgument(argAnalysis.argumentPosition);
            default:
                throw ExceptionUtilities.UnexpectedValue(argAnalysis.kind);
        }
    }

    internal static MemberAnalysisResult NameUsedForPositional(int argumentPosition) {
        return new MemberAnalysisResult(
            MemberResolutionKind.NameUsedForPositional,
            badArguments: CreateBadArgumentsWithPosition(argumentPosition)
        );
    }

    internal static MemberAnalysisResult BadNonTrailingNamedArgument(int argumentPosition) {
        return new MemberAnalysisResult(
            MemberResolutionKind.BadNonTrailingNamedArgument,
            badArguments: CreateBadArgumentsWithPosition(argumentPosition)
        );
    }

    internal static MemberAnalysisResult NoCorrespondingParameter(int argumentPosition) {
        return new MemberAnalysisResult(
            MemberResolutionKind.NoCorrespondingParameter,
            badArguments: CreateBadArgumentsWithPosition(argumentPosition)
        );
    }

    internal static MemberAnalysisResult NoCorrespondingNamedParameter(int argumentPosition) {
        return new MemberAnalysisResult(
            MemberResolutionKind.NoCorrespondingNamedParameter,
            badArguments: CreateBadArgumentsWithPosition(argumentPosition)
            );
    }

    internal static MemberAnalysisResult DuplicateNamedArgument(int argumentPosition) {
        return new MemberAnalysisResult(
            MemberResolutionKind.DuplicateNamedArgument,
            badArguments: CreateBadArgumentsWithPosition(argumentPosition)
        );
    }

    internal static BitVector CreateBadArgumentsWithPosition(int argumentPosition) {
        var badArguments = BitVector.Create(argumentPosition + 1);
        badArguments[argumentPosition] = true;
        return badArguments;
    }

    internal static MemberAnalysisResult RequiredParameterMissing(int parameterPosition) {
        return new MemberAnalysisResult(
            MemberResolutionKind.RequiredParameterMissing,
            missingParameter: parameterPosition
        );
    }

    internal static MemberAnalysisResult BadArgumentConversions(
        ImmutableArray<int> argsToParams,
        BitVector badArguments,
        ImmutableArray<Conversion> conversions) {
        return new MemberAnalysisResult(
            MemberResolutionKind.BadArgumentConversion,
            badArguments,
            argsToParams,
            conversions
        );
    }

    internal static MemberAnalysisResult InaccessibleTemplateArgument() {
        return new MemberAnalysisResult(MemberResolutionKind.InaccessibleTemplateArgument);
    }

    internal static MemberAnalysisResult TypeInferenceFailed() {
        return new MemberAnalysisResult(MemberResolutionKind.TypeInferenceFailed);
    }

    internal static MemberAnalysisResult StaticInstanceMismatch() {
        return new MemberAnalysisResult(MemberResolutionKind.StaticInstanceMismatch);
    }

    internal static MemberAnalysisResult ConstructedParameterFailedConstraintsCheck(int parameterPosition) {
        return new MemberAnalysisResult(
            MemberResolutionKind.ConstructedParameterFailedConstraintCheck,
            missingParameter: parameterPosition
        );
    }

    internal static MemberAnalysisResult WrongRefKind() {
        return new MemberAnalysisResult(MemberResolutionKind.WrongRefKind);
    }

    internal static MemberAnalysisResult WrongReturnType() {
        return new MemberAnalysisResult(MemberResolutionKind.WrongReturnType);
    }

    internal static MemberAnalysisResult LessDerived() {
        return new MemberAnalysisResult(MemberResolutionKind.LessDerived);
    }

    internal static MemberAnalysisResult Applicable(ImmutableArray<int> argsToParams, ImmutableArray<Conversion> conversions, bool hasAnyRefOmittedArgument) {
        return new MemberAnalysisResult(
            MemberResolutionKind.Applicable,
            BitVector.Null,
            argsToParams,
            conversions,
            hasAnyRefOmittedArgument: hasAnyRefOmittedArgument
        );
    }

    internal static MemberAnalysisResult Worse() {
        return new MemberAnalysisResult(MemberResolutionKind.Worse);
    }

    internal static MemberAnalysisResult Worst() {
        return new MemberAnalysisResult(MemberResolutionKind.Worst);
    }

    internal static MemberAnalysisResult ConstraintFailure(BelteDiagnosticQueue constraintFailureDiagnostics) {
        return new MemberAnalysisResult(
            MemberResolutionKind.ConstraintFailure,
            constraintFailureDiagnostics: constraintFailureDiagnostics
        );
    }
}
