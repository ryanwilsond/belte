using System;

namespace Buckle.CodeAnalysis.Binding;

internal sealed partial class OverloadResolution {
    [Flags]
    internal enum Options : ushort {
        None = 0,
        IsMethodGroupConversion = 1 << 0,
        IsDelegateConversion = 1 << 1,
    }
}
