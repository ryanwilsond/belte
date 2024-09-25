using System;

namespace Buckle.CodeAnalysis.Binding;

/// <summary>
/// A specific location for binding.
/// </summary>
[Flags]
internal enum BinderFlags : byte {
    None,
    LowLevelContext = 1 << 0,
    IgnoreAccessibility = 1 << 1,
}
