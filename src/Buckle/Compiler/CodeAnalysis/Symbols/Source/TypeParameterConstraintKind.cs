using System;

namespace Buckle.CodeAnalysis.Symbols;

[Flags]
internal enum TypeParameterConstraintKinds {
    None = 0,
    Primitive = 1 << 0,
}
