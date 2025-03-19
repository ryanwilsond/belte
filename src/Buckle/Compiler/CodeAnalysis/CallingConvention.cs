using System;

namespace Buckle.CodeAnalysis;

[Flags]
internal enum CallingConvention : byte {
    Default,
    Template,
    HasThis,
    ExplicitThis,
}
