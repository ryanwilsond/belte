using System;

namespace Buckle.CodeAnalysis.Display;

[Flags]
internal enum SymbolDisplayQualificationStyle : byte {
    None = 0,
    IncludeContainingTypes = 1 << 0,
    IncludeContainingNamespaces = 1 << 1,
    IncludeGlobalNamespace = 1 << 2,

    Everything = IncludeContainingTypes | IncludeContainingNamespaces | IncludeGlobalNamespace,
}
