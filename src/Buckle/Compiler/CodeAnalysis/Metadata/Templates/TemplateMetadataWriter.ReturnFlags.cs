using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    [Flags]
    internal enum ReturnFlags : byte {
        None = 0,
        ByRef = 1,
    }
}
