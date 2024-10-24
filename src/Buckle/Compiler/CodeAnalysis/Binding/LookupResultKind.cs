
namespace Buckle.CodeAnalysis.Binding;

internal enum LookupResultKind : byte {
    Empty,
    NotAType,
    WrongTemplate,
    NotCreatable,
    Inaccessible,
    NotReferencable,
    NotAValue,
    NotADataContainer,
    NotInvocable,
    OverloadResolutionFailure,
    Ambiguous,
    MemberGroup,
    Viable,
}
