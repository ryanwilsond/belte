using System;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A specific location for binding.
/// </summary>
[Flags]
internal enum BinderFlags : uint {
    None,
    LocalFunction = 1 << 0,
    LowLevelRegion = 1 << 1,
}
