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
    Protected = 1 << 6,
    Sealed = 1 << 7,
    Abstract = 1 << 8,
    Virtual = 1 << 9,
    Override = 1 << 10,
    New = 1 << 11,
    Ref = 1 << 12,
    ConstRef = 1 << 13,

    AccessibilityMask = Public | Private | Protected,
}
