using System;

namespace Buckle.CodeAnalysis.Symbols;

[Flags]
internal enum TypeParameterConstraintKinds : byte {
    None = 0,
    ValueType = 1 << 0,
    ReferenceType = 1 << 1,
    NotNull = 1 << 2,
    AllowByRefLike = 1 << 3,
    Expression = 1 << 4,
    Default = 1 << 5,
    Constructor,
}
