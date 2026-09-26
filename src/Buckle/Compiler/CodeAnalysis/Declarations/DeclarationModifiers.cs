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
    Internal = 1 << 7,
    InternalAndProtected = 1 << 8,
    InternalOrProtected = 1 << 9,
    Sealed = 1 << 10,
    Abstract = 1 << 11,
    Virtual = 1 << 12,
    Override = 1 << 13,
    New = 1 << 14,
    Ref = 1 << 15,
    ConstRef = 1 << 16,
    Extern = 1 << 17,
    Pinned = 1 << 18,
    Out = 1 << 19,
    Final = 1 << 20,
    FinalRef = 1 << 21,

    AccessibilityMask = Public | Private | Protected | Internal | InternalAndProtected | InternalOrProtected,
}
