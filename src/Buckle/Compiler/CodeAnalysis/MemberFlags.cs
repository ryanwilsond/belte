using System;

namespace Buckle.CodeAnalysis;

[Flags]
internal enum MemberFlags : byte {
    Method = 0x01,
    Field = 0x02,
    Constructor = 0x04,
    PropertyGet = 0x08,
    Property = 0x10,

    KindMask = 0x1F,

    Static = 0x20,
    Virtual = 0x40,
}
