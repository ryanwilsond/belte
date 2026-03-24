
namespace Buckle.Diagnostics;

internal enum MessageID : ushort {
    None = 0,

    IDS_SK_METHOD = 1,
    IDS_SK_TYPE = 2,
    IDS_SK_FIELD = 3,
    IDS_SK_UNKNOWN = 4,
    IDS_SK_VARIABLE = 5,
    IDS_SK_TEMPVAR = 6,
    IDS_SK_LABEL = 7,
    IDS_SK_CONSTRUCTOR = 8,
    IDS_SK_ARRAY = 9,
    IDS_SK_NAMESPACE = 10,
    IDS_SK_ALIAS = 11,
    IDS_SK_TYPE_OR_NAMESPACE = 12,

    IDS_MethodGroup = 13,
    IDS_ArrayAccess = 14,
    IDS_AddressOfMethodGroup = 15,
}
