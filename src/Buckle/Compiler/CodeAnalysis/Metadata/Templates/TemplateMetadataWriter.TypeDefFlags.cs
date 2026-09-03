using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    [Flags]
    internal enum TypeDefFlags : byte {
        None = 0,
        IsForSpecializationOnly = 1,
    }
}
