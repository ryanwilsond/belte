using System;

namespace Buckle.CodeAnalysis.CodeGeneration;

[Flags]
internal enum LocalSlotConstraints : byte {
    None = 0,
    ByRef = 1,
}
