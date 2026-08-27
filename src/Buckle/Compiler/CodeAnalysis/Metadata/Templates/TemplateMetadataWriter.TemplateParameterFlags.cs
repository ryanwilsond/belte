using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    [Flags]
    internal enum TemplateParameterFlags : byte {
        None = 0,
        CompileTime = 1,
    }
}
