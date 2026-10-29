using System;

namespace Buckle.CodeAnalysis.Syntax;

internal abstract partial class GreenNode {
    /// <summary>
    /// Represents the possible states of the node. Even though the node is immutable, the <see cref="_flags" /> of
    /// the node can be changed as the state represents the usage of the node in the compiler.
    /// </summary>
    [Flags]
    internal enum NodeFlags : byte {
        None = 0,
        ContainsDiagnostics = 1 << 0,
        ContainsStructuredTrivia = 1 << 1,
        ContainsDirectives = 1 << 2,
        ContainsSkippedText = 1 << 3,
        IsMissing = 1 << 4,
        HasAnnotationsDirectly = 1 << 5,
        // TODO The following flags are temporary before updating LanguageParser to scan instead of parse and reset
        ContainsDiagnosticsNoDirectives = 1 << 6,

        InheritMask = ContainsDiagnostics | ContainsStructuredTrivia | ContainsDirectives |
            ContainsSkippedText | IsMissing | ContainsDiagnosticsNoDirectives
    }
}
