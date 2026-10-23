using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    [Flags]
    internal enum TypeFlags : byte {
        None = 0,
        IsObject = 1 << 0,
        IsNullable = 1 << 1,
        IsInMemoryLibraryType = 1 << 2,
    }
}
