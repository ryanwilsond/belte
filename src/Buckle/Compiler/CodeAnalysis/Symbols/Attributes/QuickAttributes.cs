using System;

namespace Buckle.CodeAnalysis.Symbols;

[Flags]
internal enum QuickAttributes : byte {
    None = 0,
    TypeIdentifier = 1 << 0,
    TypeForwardedTo = 1 << 1,
    AssemblyKeyName = 1 << 2,
    AssemblyKeyFile = 1 << 3,
    AssemblySignatureKey = 1 << 4,
    Last = AssemblySignatureKey,
}
