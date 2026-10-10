using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    [Flags]
    internal enum LocalFlags : byte {
        None = 0,
        ByRef = 1,
        IsPinned = 2,
    }
}
