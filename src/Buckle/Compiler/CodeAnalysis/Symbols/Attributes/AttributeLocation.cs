using System;

namespace Buckle.CodeAnalysis.Symbols;

[Flags]
internal enum AttributeLocation : short {
    None = 0,

    Assembly = 1 << 0,
    Type = 1 << 1,
    Method = 1 << 2,
    Field = 1 << 3,
    Parameter = 1 << 4,
    Return = 1 << 5,
    TemplateParameter = 1 << 6,
    Module = 1 << 7,

    Unknown = 1 << 8,
}
