using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    [Flags]
    internal enum TemplateParameterFlags : byte {
        None = 0,
        CompileTime = 1 << 0,
        HasDefaultConstraint = 1 << 1,
        HasNotNullConstraint = 1 << 2,
        HasDefaultValue = 1 << 3,
    }
}
