
namespace Buckle.CodeAnalysis;

public enum CandidateReason {
    None = 0,
    NotATypeOrNamespace = 1,
    NotAnEvent = 2,
    NotAWithEventsMember = 3,
    NotAnAttributeType = 4,
    WrongArity = 5,
    NotCreatable = 6,
    NotReferencable = 7,
    Inaccessible = 8,
    NotAValue = 9,
    NotAVariable = 10,
    NotInvocable = 11,
    StaticInstanceMismatch = 12,
    OverloadResolutionFailure = 13,
    LateBound = 14,
    Ambiguous = 15,
    MemberGroup = 16,
}
