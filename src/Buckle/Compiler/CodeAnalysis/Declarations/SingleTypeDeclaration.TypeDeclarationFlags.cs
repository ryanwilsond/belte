using System;

namespace Buckle.CodeAnalysis;

internal sealed partial class SingleTypeDeclaration {
    [Flags]
    internal enum TypeDeclarationFlags : ushort {
        None = 0,
        HasBaseDeclarations = 1 << 3,
        HasAnyNonTypeMembers = 1 << 5,
        HasReturnWithExpression = 1 << 8,
        IsSimpleProgram = 1 << 9,
        HasRequiredMembers = 1 << 10,
    }
}
