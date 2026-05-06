using System;

namespace Buckle.Building;

[Flags]
public enum RefOptions {
    None = 0,
    Copy = 1 << 0,
    Flat = 1 << 1,

    FlatAndCopy = Copy | Flat
}
