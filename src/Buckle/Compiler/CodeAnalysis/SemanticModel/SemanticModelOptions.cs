using System;

namespace Buckle.CodeAnalysis;

[Flags]
public enum SemanticModelOptions {
    None = 0,
    IgnoreAccessibility = 1 << 0
}
