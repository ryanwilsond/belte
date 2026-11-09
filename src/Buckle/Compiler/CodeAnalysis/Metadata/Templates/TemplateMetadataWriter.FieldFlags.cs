using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    [Flags]
    internal enum FieldFlags : byte {
        None = 0,
        ByRef = 1,
    }
}
