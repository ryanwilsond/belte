
namespace Buckle.CodeAnalysis.Syntax;

/// <summary>
/// Houses basic information for both SyntaxNodes and SyntaxTokens.
/// </summary>
internal sealed partial class GreenNode {
    /// <summary>
    /// Creates a <see cref="GreenNode" />.
    /// </summary>
    internal GreenNode(SyntaxKind kind, int fullWidth) {
        this.kind = kind;
        this.fullWidth = fullWidth;
    }

    /// <summary>
    /// Type of <see cref="GreenNode" /> (see <see cref="SyntaxKind" />).
    /// </summary>
    internal SyntaxKind kind { get; }

    /// <summary>
    /// The full width/length of the <see cref="GreenNode" />.
    /// Includes leading and trialing trivia.
    /// </summary>
    internal int fullWidth { get; }

    /// <summary>
    /// The malleable state of the <see cref="GreenNode" />.
    /// </summary>
    internal NodeFlags flags { get; private set; }

    /// <summary>
    /// If any diagnostics have spans that overlap with this node.
    /// Aka this node produced any diagnostics.
    /// </summary>
    internal bool containsDiagnostics => (flags & NodeFlags.ContainsDiagnostics) != 0;

    /// <summary>
    /// Enables given flags.
    /// </summary>
    internal void SetFlags(NodeFlags flags) {
        this.flags |= flags;
    }

    /// <summary>
    /// Disables given flags.
    /// </summary>
    internal void ClearFlags(NodeFlags flags) {
        this.flags &= ~flags;
    }
}
