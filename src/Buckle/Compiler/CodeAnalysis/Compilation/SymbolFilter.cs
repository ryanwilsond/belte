using System;

namespace Buckle.CodeAnalysis;

[Flags]
internal enum SymbolFilter : byte {
    None = 0,
    Namespace = 1 << 0,
    Type = 1 << 1,
    Member = 1 << 2,

    TypeAndMember = Type | Member,
    All = Namespace | Type | Member
}
