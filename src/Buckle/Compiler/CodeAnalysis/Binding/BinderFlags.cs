using System;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A specific location for binding.
/// </summary>
[Flags]
internal enum BinderFlags : uint {
    None,
    LocalFunction = 1 << 0,
    Method = 1 << 1,
    Class = 1 << 2,
}
