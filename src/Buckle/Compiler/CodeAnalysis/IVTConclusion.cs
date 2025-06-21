
namespace Buckle.CodeAnalysis;

internal enum IVTConclusion : byte {
    Match,
    OneSignedOneNot,
    PublicKeyDoesntMatch,
    NoRelationshipClaimed,
}
