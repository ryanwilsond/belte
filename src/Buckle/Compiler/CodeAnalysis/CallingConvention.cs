using System;

namespace Buckle.CodeAnalysis;

[Flags]
internal enum CallingConvention : byte {
    Default = 1,
    Winapi = 1,
    Cdecl,
    StdCall,
    ThisCall,
    FastCall,
    Template,
    HasThis,
    ExplicitThis,
    Unmanaged,
}
