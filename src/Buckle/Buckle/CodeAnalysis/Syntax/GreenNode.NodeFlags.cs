using System;

namespace Buckle.CodeAnalysis.Syntax;

internal abstract partial class GreenNode {
    /// <summary>
    /// Represents the possible states of the node. Even though the node is immutable, the <see cref="flags" /> of
    /// the node can be changed as the state represents the usage of the node in the compiler.
    /// </summary>
    [Flags]
    internal enum NodeFlags : byte {
        None = 0,
        ContainsDiagnostics = 1 << 0,
        ContainsSkippedText = 1 << 1,
        IsMissing = 1 << 2,
    }
}
