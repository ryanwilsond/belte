using System;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Represents all modifiers a <see cref="Symbol"> may have when declared.
/// </summary>
[Flags]
internal enum DeclarationModifiers : byte {
    None = 0,
    Static = 1 << 0,
}
