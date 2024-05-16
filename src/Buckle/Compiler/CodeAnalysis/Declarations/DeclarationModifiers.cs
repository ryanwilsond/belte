using System;

namespace Buckle.CodeAnalysis;

/// <summary>
/// Represents all modifiers a <see cref="Symbol"> may have when declared.
/// </summary>
[Flags]
internal enum DeclarationModifiers : uint {
    None = 0,
    Static = 1 << 0,
    Const = 1 << 1,
    ConstExpr = 1 << 2,
    LowLevel = 1 << 3,
    Public = 1 << 4,
    Private = 1 << 5,
    Sealed = 1 << 6,
    Abstract = 1 << 7,
    Virtual = 1 << 8,
    Override = 1 << 9,
}
