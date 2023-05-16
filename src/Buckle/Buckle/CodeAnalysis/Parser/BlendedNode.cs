
namespace Buckle.CodeAnalysis.Syntax.InternalSyntax;

/// <summary>
/// Stores the current token and a node from the old tree to reuse.
/// Because tokens are technically types of nodes, this is really an artificial facade and the distinction does not need
/// to be made. It is designed this way for clarity in the parser.
/// </summary>
internal sealed class BlendedNode {
    /// <summary>
    /// Node to reuse.
    /// </summary>
    internal readonly SyntaxNode node;

    /// <summary>
    /// Current token.
    /// </summary>
    internal readonly SyntaxToken token;

    /// <summary>
    /// A <see cref="Blender" /> whose position starts directly after thjs node.
    /// </summary>
    internal readonly Blender blender;

    /// <summary>
    /// Creates an instance of <see cref="BlendedNode" />.
    /// </summary>
    /// <param name="node">Node to reuse.</param>
    /// <param name="token">Current token.</param>
    internal BlendedNode(SyntaxNode node, SyntaxToken token, Blender blender) {
        this.node = node;
        this.token = token;
        this.blender = blender;
    }
}
