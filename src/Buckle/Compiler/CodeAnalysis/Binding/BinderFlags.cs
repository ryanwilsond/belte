using System;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A specific location for binding.
/// </summary>
[Flags]
internal enum BinderFlags : byte {
    None,
    LocalFunction = 1 << 0,
    Method = 1 << 1,
    Class = 1 << 2,
    TemplateArgumentList = 1 << 3,
    Struct = 1 << 4,
}
