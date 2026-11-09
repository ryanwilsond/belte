using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    [Flags]
    internal enum MethodFlags : ushort {
        None = 0,
        IsWellKnownMember = 0x0100,
    }
}
