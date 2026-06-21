
namespace Buckle.CodeAnalysis.Binding;

internal enum LookupResultKind : byte {
    Empty,
    NotAnAttributeType,
    NotATypeOrNamespace,
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
    StaticInstanceMismatch,
    Viable,
}
