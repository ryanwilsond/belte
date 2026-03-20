using System;

namespace Buckle.CodeAnalysis.Binding;

[Flags]
internal enum BoundMethodGroupFlags : byte {
    None = 0,
    HasImplicitReceiver = 1 << 0,
}
