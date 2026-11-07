using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class TemplateMetadataWriter {
    [Flags]
    internal enum ComplexTemplateParameterKind : byte {
        Method,
        Type,
        NestedType,
    }
}
