using System;

namespace Buckle.CodeAnalysis;

[Flags]
internal enum CallingConvention : byte {
    Default,
    Winapi,
    Cdecl,
    StdCall,
    ThisCall,
    FastCall,
    Template,
    HasThis,
    ExplicitThis,
}
