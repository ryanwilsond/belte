
namespace Buckle.CodeAnalysis.Binding;

internal enum ArgumentAnalysisResultKind : byte {
    Normal,
    NoCorrespondingParameter,
    FirstInvalid = NoCorrespondingParameter,
    NoCorrespondingNamedParameter,
    DuplicateNamedArgument,
    RequiredParameterMissing,
    NameUsedForPositional,
    BadNonTrailingNamedArgument,
}
