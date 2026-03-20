using System;

namespace Buckle.CodeAnalysis.Display;

[Flags]
internal enum SymbolDisplayTemplateOptions : byte {
    None = 0,
    IncludeTemplateParameters = 1 << 0,
    IncludeTemplateConstraints = 1 << 1,

    Everything = IncludeTemplateParameters | IncludeTemplateConstraints,
}
