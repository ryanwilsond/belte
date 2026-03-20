
namespace Buckle.CodeAnalysis.Binding;

internal enum MemberResolutionKind : byte {
    None,
    Applicable,
    InaccessibleTemplateArgument,
    NoCorrespondingParameter,
    NoCorrespondingNamedParameter,
    DuplicateNamedArgument,
    RequiredParameterMissing,
    NameUsedForPositional,
    BadNonTrailingNamedArgument,
    BadArgumentConversion,
    TypeInferenceFailed,
    ConstructedParameterFailedConstraintCheck,
    ConstraintFailure,
    StaticInstanceMismatch,
    WrongRefKind,
    WrongReturnType,
    LessDerived,
    Worse,
    Worst,
}
