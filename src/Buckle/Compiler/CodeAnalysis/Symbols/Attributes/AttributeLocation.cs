using System;

namespace Buckle.CodeAnalysis.Symbols;

[Flags]
internal enum AttributeLocation : short {
    None = 0,

    Assembly = 1 << 0,
    Module = 1 << 1,
    Type = 1 << 2,
    Method = 1 << 3,
    Field = 1 << 4,
    Property = 1 << 5,
    Parameter = 1 << 7,
    Return = 1 << 8,
    TemplateParameter = 1 << 9,

    Unknown = 1 << 11,
}
