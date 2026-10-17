using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    [Flags]
    internal enum PropertyFlags : byte {
        None = 0,
        HasGetter = 1 << 0,
        HasSetter = 1 << 1,
    }
}
