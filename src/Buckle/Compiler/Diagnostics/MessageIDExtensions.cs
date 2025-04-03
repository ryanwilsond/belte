
namespace Buckle.Diagnostics;

internal static class MessageIDExtensions {
    internal static string Localize(this MessageID id) {
        // ? Localize refers to globalization
        // We only support English currently which is why this is a hard-coded switch
        return id switch {
            MessageID.IDS_SK_METHOD => "method",
            MessageID.IDS_SK_TYPE => "type",
            MessageID.IDS_SK_FIELD => "field",
            MessageID.IDS_SK_UNKNOWN => "<unknown>",
            MessageID.IDS_SK_VARIABLE => "variable",
            MessageID.IDS_SK_TEMPVAR => "template parameter",
            MessageID.IDS_SK_LABEL => "label",
            MessageID.IDS_SK_CONSTRUCTOR => "constructor",
            MessageID.IDS_SK_ARRAY => "array",
            MessageID.IDS_MethodGroup => "method group",
        };
    }
}
