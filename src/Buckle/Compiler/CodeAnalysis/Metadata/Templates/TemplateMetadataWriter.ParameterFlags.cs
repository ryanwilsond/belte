using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    [Flags]
    internal enum ParameterFlags : byte {
        None = 0,
        ByRef = 1,
    }
}
