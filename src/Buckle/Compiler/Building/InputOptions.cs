using System;

namespace Buckle.Building;

[Flags]
public enum InputOptions {
    None = 0,
    Flat = 1 << 0,
}
