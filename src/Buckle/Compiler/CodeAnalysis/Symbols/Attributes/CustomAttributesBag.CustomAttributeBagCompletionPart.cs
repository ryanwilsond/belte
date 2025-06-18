using System;

namespace Buckle.CodeAnalysis.Symbols;

internal sealed partial class CustomAttributesBag<T> where T : AttributeData {
    [Flags]
    internal enum CustomAttributeBagCompletionPart : byte {
        None = 0,

        EarlyDecodedWellKnownAttributeData = 1 << 0,
        DecodedWellKnownAttributeData = 1 << 1,
        Attributes = 1 << 2,

        All = EarlyDecodedWellKnownAttributeData | DecodedWellKnownAttributeData | Attributes,
    }
}
